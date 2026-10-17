using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>The most points ground_at answers for at once.</summary>
    private const int MostPoints = 500;

    /// <summary>ground_at: the ground at world points of the open area: its height now, as generated, its biome.</summary>
    [McpServerTool(Name = "ground_at", ReadOnly = true, Title = "The ground at points")]
    [Description("The ground at points of the open area (or the plot), exactly: its height now (between the 1 m grid points, as the game draws it), its height as the game generated it (the difference is what edits changed; the game keeps ground within 8 m of it, ground discs aside), its biome, and how deep water stands there (0: dry). Up to 500 points at once. Use it to set pieces' heights, check slopes and levels before building, and read a place exactly.")]
    public Task<string> GroundAtPoints(
        [Description("The points: [[x, z], [x, z], …].")] float[][] points) => OnUi(() =>
    {
        if (_w.Session is not { } s)
        {
            return "No area is open (open_area, or the Workshop).";
        }
        if (points.Length == 0 || points.Length > MostPoints || points.Any(p => p.Length < 2))
        {
            return $"Give 1 to {MostPoints} points, each [x, z].";
        }
        var scene = s.Scene;
        var (minX, minZ, maxX, maxZ) = Bounds(scene);
        float ox = scene.OriginX, oz = scene.OriginZ;
        return ToJson(points.Select(p =>
        {
            float x = p[0], z = p[1];
            if (x < minX || x > maxX || z < minZ || z > maxZ)
            {
                return (object)new { x, z, error = "outside the area" };
            }
            int gx = Math.Clamp((int)MathF.Round(x - ox), 0, scene.W - 1), gz = Math.Clamp((int)MathF.Round(z - oz), 0, scene.H - 1), i = gz * scene.W + gx;
            float h = Picking.HeightAt(scene, x - scene.Cx, -(z - scene.Cz));
            return new
            {
                x,
                z,
                height = MathF.Round(h, 3),
                original = MathF.Round(s.Ground.Original(i), 3),
                biome = ((ValheimGen.Heightmap.Biome)scene.Biomes[i]).ToString(),
                water = MathF.Round(MathF.Max(0, scene.Water - h), 2),
            };
        }));
    });
}
