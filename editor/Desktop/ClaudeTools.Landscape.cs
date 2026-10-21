using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's shaping of the ground and planting (flatten, road, forest), each run as a script as one pending step.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>A number as C# source (invariant culture, up to three decimals).</summary>
    private static string N(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>flatten: levels a rectangle to a height (default: its average), with a cosine soft edge outside it.</summary>
    [McpServerTool(Name = "flatten", Title = "Flatten a rectangle")]
    [Description("Levels the ground of a rectangle of the open area to one height (default: its average), blending into the ground around over a soft edge. One pending step. The game's ±8 m limit from the original ground holds.")]
    public Task<string> Flatten(
        [Description("West edge (smallest x).")] float x0, [Description("South edge (smallest z).")] float z0,
        [Description("East edge (largest x).")] float x1, [Description("North edge (largest z).")] float z1,
        [Description("The height to level to (optional: the rectangle's average).")] float? height = null,
        [Description("Width of the soft edge outside the rectangle, metres (0: a hard edge).")] float edge = 4) =>
        RunGenerated($$"""
			float x0 = {{N(MathF.Min(x0, x1))}}f, x1 = {{N(MathF.Max(x0, x1))}}f, z0 = {{N(MathF.Min(z0, z1))}}f, z1 = {{N(MathF.Max(z0, z1))}}f, edge = {{N(MathF.Max(0, edge))}}f;
			float target = {{(height is float h ? N(h) + "f" : "float.NaN")}};
			if (float.IsNaN(target))
			{
				float sum = 0; int n = 0;
				foreach (var (x, z) in Area.Points()) if (x >= x0 && x <= x1 && z >= z0 && z <= z1) { sum += Ground.Height(x, z); n++; }
				target = n > 0 ? sum / n : 0;
			}
			foreach (var (x, z) in Area.Points())
			{
				float dx = MathF.Max(0, MathF.Max(x0 - x, x - x1)), dz = MathF.Max(0, MathF.Max(z0 - z, z - z1));
				float d = MathF.Sqrt(dx * dx + dz * dz);
				if (d > edge) continue;
				float w = edge <= 0 || d <= 0 ? 1 : 0.5f + 0.5f * MathF.Cos(MathF.PI * d / edge);
				float h = Ground.Height(x, z);
				Ground.Set(x, z, h + (target - h) * w);
			}
			Print($"levelled to {target:0.0} m");
			""", Lang.T("claude.undo.flatten"));

    /// <summary>road: levels the ground along a line of points to a smooth slope, blends its edges (3 m) and paints it.</summary>
    [McpServerTool(Name = "road", Title = "Lay a road")]
    [Description("Lays a road along points of the open area: the ground along it levelled to a smooth slope from point to point (its heights there, or given), the edges blended, and painted (paved by default). One pending step.")]
    public Task<string> Road(
        [Description("The road's points, in order: [[x, z], [x, z], …] (at least 2).")] float[][] points,
        [Description("Width, metres.")] float width = 4,
        [Description("Paint: paved, dirt, or none.")] string paint = "paved",
        [Description("Heights of the points, in order (optional: the ground's there).")] float[]? heights = null)
    {
        if (points.Length < 2 || points.Any(p => p.Length < 2))
        {
            return Task.FromResult("A road needs at least 2 points, each [x, z].");
        }
        if (paint != "none" && !PaintKinds.Contains(paint))
        {
            return Task.FromResult("The paint is paved, dirt or none.");
        }
        string px = string.Join(", ", points.Select(p => N(p[0]) + "f")), pz = string.Join(", ", points.Select(p => N(p[1]) + "f"));
        string ph = heights is { } hs && hs.Length == points.Length ? string.Join(", ", hs.Select(h => N(h) + "f")) : "";
        return RunGenerated($$"""
			float[] px = { {{px}} }, pz = { {{pz}} };
			float[] ph = { {{ph}} };
			if (ph.Length != px.Length) { ph = new float[px.Length]; for (int i = 0; i < px.Length; i++) ph[i] = Ground.Height(px[i], pz[i]); }
			float half = {{N(MathF.Max(1, width) / 2)}}f, edge = 3;
			foreach (var (x, z) in Area.Points())
			{
				float best = float.MaxValue, at = 0;
				for (int i = 0; i + 1 < px.Length; i++)
				{
					float ax = px[i], az = pz[i], bx = px[i + 1] - ax, bz = pz[i + 1] - az;
					float len2 = bx * bx + bz * bz, t = len2 > 0 ? Math.Clamp(((x - ax) * bx + (z - az) * bz) / len2, 0, 1) : 0;
					float dx = x - (ax + bx * t), dz = z - (az + bz * t), d = MathF.Sqrt(dx * dx + dz * dz);
					if (d < best) { best = d; at = ph[i] + (ph[i + 1] - ph[i]) * t; }
				}
				if (best > half + edge) continue;
				float w = best <= half ? 1 : 0.5f + 0.5f * MathF.Cos(MathF.PI * (best - half) / edge);
				float h = Ground.Height(x, z);
				Ground.Set(x, z, h + (at - h) * w);
				if ({{(paint == "none" ? "false" : "true")}} && best <= half) Ground.Paint(x, z, "{{paint.ToLowerInvariant()}}");
			}
			Print($"road of {px.Length - 1} segment(s)");
			""", Lang.T("claude.undo.road"));
    }

    /// <summary>forest: plants a mix of prefabs on a jittered grid, off water and away from buildings, trees and rocks.</summary>
    [McpServerTool(Name = "forest", Title = "Plant trees or rocks")]
    [Description("Plants objects (trees, bushes, rocks: prefab names from find_prefabs) over a rectangle of the open area, about spacing metres apart with some randomness, turned at random, kept off water, off buildings and away from what is already there. One pending step.")]
    public Task<string> Forest(
        [Description("West edge.")] float x0, [Description("South edge.")] float z0,
        [Description("East edge.")] float x1, [Description("North edge.")] float z1,
        [Description("Prefab names to mix, e.g. [\"Beech1\", \"Birch1\", \"Oak1\"].")] string[] kinds,
        [Description("About how far apart, metres.")] float spacing = 6,
        [Description("Any number: another layout with the same choices.")] int seed = 1)
    {
        if (kinds.Length == 0)
        {
            return Task.FromResult("Give at least one prefab name (find_prefabs).");
        }
        // As C# string literals, escaped (a name ending in a backslash broke the script).
        string list = string.Join(", ", kinds.Select(k => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(k, quote: true)));
        return RunGenerated($$"""
			string[] kinds = { {{list}} };
			foreach (var k in kinds) if (!Objects.CanPlace(k)) { Print($"{k}: not an object this world can have"); return; }
			Rnd.Seed = {{seed}};
			float x0 = {{N(MathF.Min(x0, x1))}}f, x1 = {{N(MathF.Max(x0, x1))}}f, z0 = {{N(MathF.Min(z0, z1))}}f, z1 = {{N(MathF.Max(z0, z1))}}f, step = {{N(MathF.Max(1, spacing))}}f;
			int n = 0;
			for (float z = z0 + step / 2; z < z1; z += step)
				for (float x = x0 + step / 2; x < x1; x += step)
				{
					float px = x + Rnd.Range(-0.4f, 0.4f) * step, pz = z + Rnd.Range(-0.4f, 0.4f) * step;
					if (!Area.Inside(px, pz) || Ground.Height(px, pz) < Area.Water + 0.5f) continue;
					if (Objects.Near(px, pz, step * 0.5f).Any(o => o.Building || o.Kind is "Trees" or "Rocks")) continue;
					Objects.Place(Rnd.Pick(kinds), px, pz, yaw: Rnd.Range(0, 360));
					n++;
				}
			Print($"placed {n}");
			""", Lang.T("claude.undo.forest"));
    }
}
