using System.Numerics;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>The Path tool's actions dig_path takes, by name.</summary>
    internal static readonly Dictionary<string, PathTool.Action> PathActions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["river"] = PathTool.Action.River,
        ["cave"] = PathTool.Action.Cave,
        ["ramp"] = PathTool.Action.Ramp,
        ["flatten"] = PathTool.Action.Flatten,
        ["raise"] = PathTool.Action.Raise,
        ["lower"] = PathTool.Action.Lower,
        ["smooth"] = PathTool.Action.Smooth,
    };

    /// <summary>
    /// The Path tool on a line of world points of the scene (in grid points), with the values given; ramp's ends and
    /// flatten's height default to the ground at the line's ends.
    /// </summary>
    internal static PathTool LineOver(WorldScene scene, float[][] points, PathTool.Action act, float width, float soft, float? depth, float headroom,
        float? height, float amount, bool natural, bool curved, PathTool.CaveRock rock, int seed)
    {
        float ox = scene.OriginX, oz = scene.OriginZ;
        var path = new PathTool
        {
            Act = act,
            Width = Math.Clamp(width, 1, 64),
            Soft = Math.Clamp(soft, 0, 32),
            Amount = MathF.Max(0, amount),
            Natural = natural,
            Curved = curved,
            Rock = rock,
            Seed = seed,
            Headroom = Math.Clamp(headroom, 2, 30),
            RampEdited = true,
        };
        path.Points.AddRange(points.Select(p => new Vector2(p[0] - ox, p[1] - oz)));
        float first = GroundAt(scene, points[0][0], points[0][1]), last = GroundAt(scene, points[^1][0], points[^1][1]);
        (path.Start, path.End) = (height ?? first, height is null ? last : height.Value);
        path.Height = height ?? first;
        if (act == PathTool.Action.Cave)
        {
            path.CaveDepth = Math.Clamp(depth ?? 7.5f, 2, 40);
        }
        else
        {
            path.Depth = Math.Clamp(depth ?? 2, 0, 30);
        }
        return path;
    }

    /// <summary>Why the points of a line are not one (fewer than 2, or a point without x and z), or null when they are one.</summary>
    /// <param name="points">The line's points, [x, z] each.</param>
    internal static string? LinePointsProblem(float[][] points) =>
        points.Length < 2 || points.Any(p => p.Length < 2) ? "A line needs at least 2 points, each [x, z]." : null;

    /// <summary>Which point of a line lies outside the open area (dig_path), with the area's bounds, or null when none does.</summary>
    /// <param name="scene">The open area.</param>
    /// <param name="points">The line's points, [x, z] each.</param>
    internal static string? LineOutside(WorldScene scene, float[][] points)
    {
        var (minX, minZ, maxX, maxZ) = Bounds(scene);
        return points.FirstOrDefault(p => p[0] < minX || p[0] > maxX || p[1] < minZ || p[1] > maxZ) is { } outside
            ? $"({outside[0]}, {outside[1]}) is outside the open area (x {minX} to {maxX}, z {minZ} to {maxZ})."
            : null;
    }
}
