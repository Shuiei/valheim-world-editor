
namespace TerrainEditor.Desktop;

/// <summary>The ground a copy of buildings carries (CopyData.Ground.cs): its footprint and the box it goes over.</summary>
public sealed partial class CopyData
{
    /// <summary>
    /// How much of the ground at each point of a w × h grid (world x0, z0 at its first point) goes with the building
    /// pieces among the things: 1 under a piece and Margin around it, fading to 0 over Blend. Pieces hidden far below
    /// the ground (PasteTool.HiddenDepth) take none.
    /// </summary>
    private static float[] Footprint(IEnumerable<WorldScene.Thing> things, int w, int h, float x0, float z0, Func<float, float, float> groundAt)
    {
        float[] foot = new float[w * h];
        foreach (var t in things)
        {
            if (Extent(t.Prefab, t.Position, t.Rotation, t.Scale) is not var (ax, az, bx, bz, bottom)
                || bottom < groundAt(t.Position.X, t.Position.Z) - PasteTool.HiddenDepth)
            {
                continue;
            }
            (ax, az, bx, bz) = (ax - Margin - x0, az - Margin - z0, bx + Margin - x0, bz + Margin - z0);
            for (int gz = Math.Max(0, (int)MathF.Floor(az - Blend)); gz <= Math.Min(h - 1, (int)MathF.Ceiling(bz + Blend)); gz++)
            {
                for (int gx = Math.Max(0, (int)MathF.Floor(ax - Blend)); gx <= Math.Min(w - 1, (int)MathF.Ceiling(bx + Blend)); gx++)
                {
                    float dx = MathF.Max(0, MathF.Max(ax - gx, gx - bx)), dz = MathF.Max(0, MathF.Max(az - gz, gz - bz));
                    float k = 1 - MathF.Sqrt(dx * dx + dz * dz) / Blend;
                    if (k > foot[gz * w + gx])
                    {
                        foot[gz * w + gx] = k;
                    }
                }
            }
        }
        return foot;
    }

    /// <summary>
    /// The whole-metre rectangle a copy's ground goes over: around its pieces' extents, reach metres out on each side
    /// (all zeros without pieces).
    /// </summary>
    /// <param name="ext">The pieces' extents (see Extent).</param>
    /// <param name="reach">How far past them the ground goes, in metres.</param>
    /// <returns>The rectangle's west, east, south and north edges, in grid metres.</returns>
    internal static (int X0, int X1, int Z0, int Z1) GroundBox(List<(float X0, float Z0, float X1, float Z1, float Bottom)> ext, float reach) =>
        ext.Count == 0 ? (0, 0, 0, 0)
        : ((int)MathF.Floor(ext.Min(e => e.X0) - reach), (int)MathF.Ceiling(ext.Max(e => e.X1) + reach),
            (int)MathF.Floor(ext.Min(e => e.Z0) - reach), (int)MathF.Ceiling(ext.Max(e => e.Z1) + reach));
}
