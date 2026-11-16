using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>A Path tool action dug into the ground (dig_path): what it changed, as the answer says it.</summary>
    /// <param name="s">The session the change is made in.</param>
    /// <param name="path">The Path tool, set up.</param>
    /// <param name="act">The action.</param>
    /// <param name="brush">The brush the tool paints with.</param>
    /// <param name="only">The mask over the ground, or null for all of it.</param>
    /// <param name="label">The history step's name.</param>
    private static Said ApplyDugPath(EditSession s, PathTool path, PathTool.Action act, Brush brush, Func<int, float>? only, string label)
    {
        bool clamped = false;
        var touched = s.EditGround(label, g =>
        {
            var (t, rect, c) = path.Apply(g, brush, s.Scene.Water, only);
            clamped = c;
            return (t, rect);
        });
        return touched.Count == 0 ? Said.Of("path.msg.nothing")
            : Said.Of("claude.msg.path", PathTool.Label(act), PathTool.Length(path.Curve()), touched.Count)
                + (clamped ? Said.Of("claude.msg.limitReached") : Said.Empty);
    }

    /// <summary>dig_path: a Path tool action along a line of points, as one pending step.</summary>
    [McpServerTool(Name = "dig_path", Title = "A river, ramp or other ground change along a line")]
    [Description("Changes the ground along a line of points of the open area as the Path tool does, a smooth curve through them (curved: false for straight stretches): river (digs a bed depth metres below the water level, so the sea fills it: a river, canal or moat), ramp (a slope from the ground at the first point to the ground at the last, or height for both), flatten (to height, default: the ground at the first point), raise or lower (by amount), smooth, naturalize (natural-looking bumps, bumps metres high and bumpSize metres wide), erode (slopes settle to restAngle and rain cuts gullies), restore (back to the ground the game generated, paint removed). width metres wide, fading out over soft metres beyond. natural: true gives a varying width, ragged edges and bumps. One pending step.")]
    public Task<string> DigPath(
        [Description("river, ramp, flatten, raise, lower, smooth, naturalize, erode or restore.")] string action,
        [Description("The line's points, in order: [[x, z], [x, z], …] (at least 2).")] float[][] points,
        [Description("Width, metres (default 8).")] float width = 8,
        [Description("Soft edge beyond the width, metres (default 4).")] float soft = 4,
        [Description("river: metres below the water level (default 2).")] float? depth = null,
        [Description("flatten: the height; ramp: one height for both ends (optional: the ground at the ends).")] float? height = null,
        [Description("raise and lower: metres (default 2).")] float amount = 2,
        [Description("A varying width, ragged edges and bumps (default false).")] bool natural = false,
        [Description("A smooth curve through the points (default true).")] bool curved = true,
        [Description("Any number: another natural pattern with the same choices.")] int seed = 1,
        [Description("naturalize: how high the bumps are, metres (default 1.2).")] float bumps = 1.2f,
        [Description("naturalize: how wide the bumps are, metres (default 16).")] float bumpSize = 16,
        [Description("erode: the slope ground settles to, degrees (default 33).")] float restAngle = 33,
        [Description("Which ground may change (optional: all of it).")] MaskSpec? mask = null) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        if (!PathActions.TryGetValue(action, out var act))
        {
            return "The action is river, ramp, flatten, raise, lower, smooth, naturalize, erode or restore.";
        }
        if (LinePointsProblem(points) is string badLine)
        {
            return badLine;
        }
        var s = _w.Session!;
        if (LineOutside(s.Scene, points) is string outside)
        {
            return outside;
        }
        var (only, wrong) = MaskOf(mask, s);
        if (wrong != null)
        {
            return wrong;
        }
        var path = LineOver(s.Scene, points, act, width, soft, depth, height, amount, natural, curved);
        var brush = new Brush { Noise = new Noise(seed), NoiseAmp = Math.Clamp(bumps, 0, 20), NoiseSize = Math.Clamp(bumpSize, 2, 200), RestAngle = Math.Clamp(restAngle, 5, 80) };
        string label = ClaudesStep(PathStep(act));
        var said = ApplyDugPath(s, path, act, brush, only, label);
        _w.AfterClaudeEdit(said);
        return said.English + " Now pending: " + Pending();
    });
}
