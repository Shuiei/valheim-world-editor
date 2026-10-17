using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Hammer
{
    /// <summary>
    /// The points of a piece's colliders (box corners, mesh vertices, the bottoms and sides of spheres
    /// and capsules), turned and scaled, from its origin; none for a piece the game gives no collider.
    /// </summary>
    public static IEnumerable<Vector3> Outline(string prefab, Quaternion rotation, float scale = 1)
    {
        if (Get(prefab) is not { } d)
        {
            yield break;
        }
        foreach (var c in d.Colliders)
        {
            IEnumerable<Vector3> pts = c switch
            {
                Box b => Enumerable.Range(0, 8).Select(i => b.C + Vector3.Transform(new Vector3((i & 1) == 0 ? -b.H.X : b.H.X, (i & 2) == 0 ? -b.H.Y : b.H.Y, (i & 4) == 0 ? -b.H.Z : b.H.Z), b.Q)),
                Sphere sp => Round(sp.C, sp.R),
                Capsule cp => Round(cp.A, cp.R).Concat(Round(cp.B, cp.R)),
                Mesh m => m.V,
                _ => [],
            };
            foreach (var v in pts)
            {
                yield return Vector3.Transform(v * scale, rotation);
            }
        }

        static IEnumerable<Vector3> Round(Vector3 c, float r) => new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ }.Select(u => c + u * r);
    }

    /// <summary>
    /// How far below its origin a piece reaches (its colliders' lowest point), turned and scaled; 0 for a
    /// piece the game gives no collider.
    /// </summary>
    public static float Bottom(string prefab, Quaternion rotation, float scale = 1) =>
        Outline(prefab, rotation, scale).Select(v => v.Y).DefaultIfEmpty(0).Min();
}
