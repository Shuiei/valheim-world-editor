using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>The ground actions shape_ground takes, by name.</summary>
    internal static readonly Dictionary<string, AreaTool.GroundAction> GroundActions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["smooth"] = AreaTool.GroundAction.Smooth,
        ["naturalize"] = AreaTool.GroundAction.Natural,
        ["erode"] = AreaTool.GroundAction.Erode,
        ["restore"] = AreaTool.GroundAction.Restore,
        ["raise"] = AreaTool.GroundAction.Raise,
        ["lower"] = AreaTool.GroundAction.Lower,
        ["paint"] = AreaTool.GroundAction.Paint,
    };

    /// <summary>The paints, by name, as the paint brushes put them down.</summary>
    internal static readonly Dictionary<string, BrushTool> Paints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dirt"] = BrushTool.PaintDirt,
        ["cultivated"] = BrushTool.PaintCultivated,
        ["paved"] = BrushTool.PaintPaved,
        ["clear"] = BrushTool.PaintClear,
    };

    /// <summary>shape_ground: an Area tool ground action over a rectangle, as one pending step.</summary>
    [McpServerTool(Name = "shape_ground", Title = "Smooth, naturalize, erode or restore ground")]
    [Description("Changes the ground of a rectangle of the open area as the Area tool does, fading in over a soft edge going inwards: smooth (evens out bumps and steps), naturalize (natural-looking bumps, bumps metres high and bumpSize metres wide: what made-up ground needs to look real), erode (weathers it: slopes settle to restAngle degrees and rain cuts gullies), restore (back to the ground the game generated, paint taken away), raise or lower (by amount metres), paint (with paint: dirt, cultivated, paved or clear). A mask limits it to some ground, as the Mask panel does (heights, slopes, biomes, paint): paint only the gentle slopes, smooth only the steep ones, raise only what is below a height. One pending step. The game's ±8 m limit from the original ground holds.")]
    public Task<string> ShapeGround(
        [Description("smooth, naturalize, erode, restore, raise, lower or paint.")] string action,
        [Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
        [Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
        [Description("The soft edge, metres inwards from the rectangle's sides (default 3; 0: hard).")] float edge = 3,
        [Description("raise and lower: metres (default 1).")] float amount = 1,
        [Description("naturalize: how high the bumps are, metres (default 1.2).")] float bumps = 1.2f,
        [Description("naturalize: how wide the bumps are, metres (default 16).")] float bumpSize = 16,
        [Description("erode: the slope ground settles to, degrees (default 33).")] float restAngle = 33,
        [Description("naturalize and erode: any number, another pattern with the same choices.")] int seed = 1,
        [Description("paint: dirt, cultivated, paved or clear.")] string paint = "dirt",
        [Description("Which ground may change (optional: all of it).")] MaskSpec? mask = null) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        if (!GroundActions.TryGetValue(action, out var act))
        {
            return "The action is smooth, naturalize, erode, restore, raise, lower or paint.";
        }
        if (!Paints.TryGetValue(paint, out var paintTool))
        {
            return "The paint is dirt, cultivated, paved or clear.";
        }
        var s = _w.Session!;
        var (only, wrong) = MaskOf(mask, s);
        if (wrong != null)
        {
            return wrong;
        }
        if (OutsideArea(s.Scene, x0, z0, x1, z1) is string outside)
        {
            return outside;
        }
        var box = BoxOver(s.Scene, x0, z0, x1, z1, edge);
        var brush = SmoothBrush(bumps, bumpSize, restAngle, seed);
        var name = GroundStep(act, paintTool);
        bool clamped = false;
        var touched = s.EditGround(ClaudesStep(name.Shown), g =>
        {
            var (t, rect, c) = box.Apply(g, brush, act, 0, MathF.Max(0, amount), Brush.PaintOf(paintTool)!, only);
            clamped = c;
            return (t, rect);
        });
        _w.AfterClaudeEdit(Said.Of("claude.msg.ground", name, touched.Count));
        return GroundAnswer(act, touched.Count, clamped);
    });

    /// <summary>The brush of a smoothing action (shape_ground): its bumps, edge and seed, as the Area tool's brush has them.</summary>
    /// <param name="bumps">How high the bumps are, metres.</param>
    /// <param name="bumpSize">How wide the bumps are, metres.</param>
    /// <param name="restAngle">The slope ground settles to, degrees.</param>
    /// <param name="seed">The pattern's seed.</param>
    private static Brush SmoothBrush(float bumps, float bumpSize, float restAngle, int seed) => new()
    {
        NoiseAmp = Math.Clamp(bumps, 0, 20),
        NoiseSize = Math.Clamp(bumpSize, 2, 200),
        Noise = new Noise(seed),
        RestAngle = Math.Clamp(restAngle, 5, 80),
        Random = new Random(seed),
    };
}
