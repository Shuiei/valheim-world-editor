using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Hammer
{
    /// <summary>A piece standing in the world: its kind, where, turned how.</summary>
    /// <param name="Index">The caller's own index of it, given back in hits and snaps.</param>
    /// <param name="Prefab">Its prefab name.</param>
    /// <param name="Position">Where it stands.</param>
    /// <param name="Rotation">How it is turned.</param>
    public sealed record Placed(int Index, string Prefab, Vector3 Position, Quaternion Rotation);

    /// <summary>What the build ray hits first.</summary>
    /// <param name="Piece">The piece's index (Placed.Index), or null for the ground.</param>
    /// <param name="Point">Where it hits.</param>
    /// <param name="Normal">The surface's normal there (unit length; straight up on the ground).</param>
    public sealed record Hit(int? Piece, Vector3 Point, Vector3 Normal);

    /// <summary>PieceRayTest: the nearest of the pieces' colliders and the ground.</summary>
    /// <param name="o">The ray's origin.</param>
    /// <param name="d">Its direction (any length).</param>
    /// <param name="pieces">The pieces it may hit; those the scan does not know are passed through.</param>
    /// <param name="groundT">Where the ray meets the ground (distance along it), if it does.</param>
    /// <param name="maxY">Nothing above it counts (the Workshop's cut hides it).</param>
    /// <returns>The nearest hit, or null when it hits nothing.</returns>
    public static Hit? Ray(Vector3 o, Vector3 d, IEnumerable<Placed> pieces, float? groundT, float maxY = float.MaxValue)
    {
        d = Vector3.Normalize(d);
        float best = groundT ?? float.MaxValue;
        Hit? hit = groundT is float g ? new Hit(null, o + d * g, Vector3.UnitY) : null;
        foreach (var p in pieces)
        {
            if (Get(p.Prefab) is not { } data)
            {
                continue;
            }
            var inv = Quaternion.Inverse(p.Rotation);
            // In the piece's frame.
            var lo = Vector3.Transform(o - p.Position, inv);
            var ld = Vector3.Transform(d, inv);
            foreach (var shape in data.Colliders)
            {
                if (RayShape(lo, ld, shape) is (float t, Vector3 n) && t >= 0 && t < best && o.Y + d.Y * t <= maxY + 1e-3f)
                {
                    best = t;
                    hit = new Hit(p.Index, o + d * t, Vector3.Normalize(Vector3.Transform(n, p.Rotation)));
                }
            }
        }
        return hit;
    }
}
