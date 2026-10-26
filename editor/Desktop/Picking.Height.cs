using System.Numerics;

namespace TerrainEditor.Desktop;

/// <summary>The area test and the ground height of the picking, under a view-space point.</summary>
public static partial class Picking
{
    /// <summary>Whether a view-space point (x, z) is over the area's ground.</summary>
    public static bool Inside(WorldScene s, float x, float z)
    {
        float gx = x + (s.W - 1) / 2f, gz = -z + (s.H - 1) / 2f;
        return gx >= 0 && gz >= 0 && gx <= s.W - 1 && gz <= s.H - 1;
    }

    /// <summary>
    /// How deep under the ground (metres) an object may be and still be picked by a zone, by kind or as a building's
    /// piece: a slope sinks a building's lowest pieces a few metres at most.
    /// </summary>
    public const float BuriedDepth = 10;

    /// <summary>
    /// Whether an object at a world point is buried deep under the ground there, more than <see cref="BuriedDepth"/>:
    /// put there by a mod (crafting stations hidden 10 km under a house), not part of what is built above. Past the
    /// area's edge, where there is no ground, nothing is.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="p">The object's place, world coordinates.</param>
    public static bool Buried(WorldScene s, Vector3 p)
    {
        float x = p.X - s.Cx, z = -(p.Z - s.Cz);
        return Inside(s, x, z) && p.Y < HeightAt(s, x, z) - BuriedDepth;
    }

    /// <summary>
    /// The ground's height under a view-space point (between the grid points), or -1000 outside the area.
    /// </summary>
    public static float HeightAt(WorldScene s, float x, float z)
    {
        if (CellAt(s, x, z) is not var (x0, z0, tx, tz))
        {
            return -1000;
        }
        float H(int a, int b) => s.Heights[b * s.W + a];
        return (H(x0, z0) * (1 - tx) + H(x0 + 1, z0) * tx) * (1 - tz) + (H(x0, z0 + 1) * (1 - tx) + H(x0 + 1, z0 + 1) * tx) * tz;
    }
}
