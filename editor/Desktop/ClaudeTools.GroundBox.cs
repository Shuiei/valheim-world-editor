using System.Numerics;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>
    /// The Area tool's box over a world rectangle of the scene (its corners in grid points), with a soft edge of
    /// edge metres going inwards.
    /// </summary>
    internal static AreaTool BoxOver(WorldScene scene, float x0, float z0, float x1, float z1, float edge)
    {
        float ox = scene.OriginX, oz = scene.OriginZ;
        var box = new AreaTool { Box = true, Soft = MathF.Max(0, edge) };
        box.Points.Add(new Vector2(MathF.Min(x0, x1) - ox, MathF.Min(z0, z1) - oz));
        box.Points.Add(new Vector2(MathF.Max(x0, x1) - ox, MathF.Max(z0, z1) - oz));
        return box;
    }

    /// <summary>Why a rectangle of world metres is not wholly within the open area (shape_ground), or null when it is.</summary>
    /// <param name="scene">The open area.</param>
    /// <param name="x0">One corner, x.</param>
    /// <param name="z0">One corner, z.</param>
    /// <param name="x1">The opposite corner, x.</param>
    /// <param name="z1">The opposite corner, z.</param>
    internal static string? OutsideArea(WorldScene scene, float x0, float z0, float x1, float z1)
    {
        var (minX, minZ, maxX, maxZ) = Bounds(scene);
        if (MathF.Max(x0, x1) < minX || MathF.Min(x0, x1) > maxX || MathF.Max(z0, z1) < minZ || MathF.Min(z0, z1) > maxZ)
        {
            return $"The rectangle is outside the open area (x {minX} to {maxX}, z {minZ} to {maxZ}).";
        }
        return null;
    }

    /// <summary>shape_ground's answer once the change is made: what changed, and whether part of it reached the game's limit.</summary>
    /// <param name="act">The action.</param>
    /// <param name="count">The ground points changed.</param>
    /// <param name="clamped">Whether part of the change reached the game's ±8 m limit.</param>
    internal string GroundAnswer(AreaTool.GroundAction act, int count, bool clamped) =>
        count == 0 ? "Nothing changed." : $"{AreaTool.Label(act, english: true)}: {count} ground point(s) changed.{(clamped ? " Part of it reached the game's ±8 m limit." : "")} Now pending: {Pending()}";
}
