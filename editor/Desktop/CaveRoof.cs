using System.Numerics;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// A cave's roof of the game's boulders (the Path tool's Cave): each boulder upside down, turned, tilted
/// and sized at random, and hung by its real underside (the game's mesh, CaveRocks) so it clears the headroom
/// over the whole floor beneath it. Boulders are added until the floor and the foot of the walls are
/// covered, then the walls are raised into the rock wherever its underside is above them: no daylight
/// at the sides.
/// </summary>
public static partial class CaveRoof
{
    /// <summary>A boulder of the roof.</summary>
    /// <param name="Prefab">The boulder's prefab.</param>
    /// <param name="Position">World position, in metres.</param>
    /// <param name="Rotation">Unity Euler rotation, in degrees.</param>
    /// <param name="Scale">Its scale.</param>
    public sealed record Rock(string Prefab, Vector3 Position, Vector3 Rotation, float Scale);

    /// <summary>The roof's boulders, and the walls' new heights.</summary>
    /// <param name="Rocks">The boulders.</param>
    /// <param name="Seal">The walls' new heights: grid point → height it is raised to, at least.</param>
    /// <param name="Uncovered">How many points of the floor and the foot of the walls no boulder covers.</param>
    public sealed record Result(List<Rock> Rocks, Dictionary<int, float> Seal, int Uncovered);

    /// <summary>A boulder's shape; the rock4 boulders share one model (coast, forest, heath).</summary>
    private static CaveRocks.Shape ShapeOf(string prefab) => CaveRocks.Of(prefab.StartsWith("rock4_", StringComparison.Ordinal) ? "rock4_forest" : prefab);

    /// <summary>
    /// The underside of a boulder (its pivot at height 0) over the grid: the lowest point of the model
    /// above each grid point it covers.
    /// </summary>
    /// <param name="prefab">The boulder's prefab.</param>
    /// <param name="px">The pivot's x in grid coordinates.</param>
    /// <param name="pz">The pivot's z in grid coordinates.</param>
    /// <param name="rotation">Unity Euler rotation, in degrees.</param>
    /// <param name="scale">The boulder's scale.</param>
    /// <param name="w">The grid's width.</param>
    /// <param name="h">The grid's height.</param>
    /// <returns>Grid point (z * w + x) → the underside's height there, relative to the pivot.</returns>
    internal static Dictionary<int, float> Underside(string prefab, float px, float pz, Vector3 rotation, float scale, int w, int h)
    {
        var shape = ShapeOf(prefab);
        // Quaternion.Euler in Unity applies z, then x, then y: the same as yaw/pitch/roll here.
        var q = Quaternion.CreateFromYawPitchRoll(rotation.Y * MathF.PI / 180, rotation.X * MathF.PI / 180, rotation.Z * MathF.PI / 180);
        var v = shape.V.Select(p => Vector3.Transform(p * scale, q) + new Vector3(px, 0, pz)).ToArray();
        var low = new Dictionary<int, float>();
        for (int k = 0; k < shape.T.Length; k += 3)
        {
            Vector3 a = v[shape.T[k]], b = v[shape.T[k + 1]], c = v[shape.T[k + 2]];
            float d = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
            if (MathF.Abs(d) < 1e-6f)
            {
                continue;
            }
            int x0 = Math.Max(0, (int)MathF.Ceiling(MathF.Min(a.X, MathF.Min(b.X, c.X)))), x1 = Math.Min(w - 1, (int)MathF.Floor(MathF.Max(a.X, MathF.Max(b.X, c.X))));
            int z0 = Math.Max(0, (int)MathF.Ceiling(MathF.Min(a.Z, MathF.Min(b.Z, c.Z)))), z1 = Math.Min(h - 1, (int)MathF.Floor(MathF.Max(a.Z, MathF.Max(b.Z, c.Z))));
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float l1 = ((b.Z - c.Z) * (x - c.X) + (c.X - b.X) * (z - c.Z)) / d, l2 = ((c.Z - a.Z) * (x - c.X) + (a.X - c.X) * (z - c.Z)) / d, l3 = 1 - l1 - l2;
                    if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f)
                    {
                        continue;
                    }
                    float y = l1 * a.Y + l2 * b.Y + l3 * c.Y;
                    int p = z * w + x;
                    if (!low.TryGetValue(p, out float old) || y < old)
                    {
                        low[p] = y;
                    }
                }
            }
        }
        return low;
    }
}
