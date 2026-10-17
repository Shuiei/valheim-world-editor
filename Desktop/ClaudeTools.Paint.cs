using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's painting of the ground (paint_area): the paints it takes, and the history name of each paint.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>The paints paint_area and road take.</summary>
    private static readonly HashSet<string> PaintKinds = new(StringComparer.OrdinalIgnoreCase) { "dirt", "cultivated", "paved", "clear" };

    /// <summary>A paint's name in the history (paint_area, shape_ground's paint), in the user's language.</summary>
    /// <param name="paint">The paint brush whose colour is put down.</param>
    private static Said PaintStep(BrushTool paint) => paint switch
    {
        BrushTool.PaintDirt => Said.Of("claude.undo.paintDirt"),
        BrushTool.PaintCultivated => Said.Of("claude.undo.paintCultivated"),
        BrushTool.PaintPaved => Said.Of("claude.undo.paintPaved"),
        _ => Said.Of("claude.undo.paintClear"),
    };

    /// <summary>paint_area: paints a rectangle or a circle.</summary>
    [McpServerTool(Name = "paint_area", Title = "Paint the ground")]
    [Description("Paints the ground of a rectangle (x0, z0, x1, z1) or a circle (x, z, radius) of the open area: dirt, cultivated, paved, or clear (back to the biome's own). One pending step.")]
    public Task<string> PaintArea(
        [Description("dirt, cultivated, paved or clear.")] string kind,
        [Description("Rectangle: west edge.")] float? x0 = null, [Description("Rectangle: south edge.")] float? z0 = null,
        [Description("Rectangle: east edge.")] float? x1 = null, [Description("Rectangle: north edge.")] float? z1 = null,
        [Description("Circle: middle x.")] float? x = null, [Description("Circle: middle z.")] float? z = null,
        [Description("Circle: radius, metres.")] float? radius = null)
    {
        if (!PaintKinds.Contains(kind))
        {
            return Task.FromResult("The paint is dirt, cultivated, paved or clear.");
        }
        string inside = x0 is float a && z0 is float b && x1 is float c && z1 is float d
            ? $"x >= {N(MathF.Min(a, c))}f && x <= {N(MathF.Max(a, c))}f && z >= {N(MathF.Min(b, d))}f && z <= {N(MathF.Max(b, d))}f"
            : x is float cx && z is float cz && radius is float r
                ? $"(x - {N(cx)}f) * (x - {N(cx)}f) + (z - {N(cz)}f) * (z - {N(cz)}f) <= {N(r * r)}f"
                : null!;
        if (inside == null)
        {
            return Task.FromResult("Give a rectangle (x0, z0, x1, z1) or a circle (x, z, radius).");
        }
        return RunGenerated($$"""
			int n = 0;
			foreach (var (x, z) in Area.Points()) if ({{inside}}) { Ground.Paint(x, z, "{{kind.ToLowerInvariant()}}"); n++; }
			Print($"painted {n} points");
			""", PaintStep(Paints[kind]).Shown);
    }
}
