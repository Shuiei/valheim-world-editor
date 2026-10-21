using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.Desktop;

public static partial class SceneLayers
{
    /// <summary>
    /// The pickable meshes' triangles in the view's frame, each mesh with its bounds so a ray skips the meshes it
    /// misses: what clicks land on (Picking.HitGround).
    /// </summary>
    public sealed class PickTriangles
    {
        private readonly List<(Vector3 Min, Vector3 Max, Vector3[] Corners)> _meshes;

        private PickTriangles(List<(Vector3 Min, Vector3 Max, Vector3[] Corners)> meshes) => _meshes = meshes;

        /// <summary>No triangles.</summary>
        public static PickTriangles None { get; } = new([]);

        /// <summary>The pickable triangles of the layers (three corners each), in the view's frame.</summary>
        internal static PickTriangles Of(WorldScene s, IReadOnlyList<SceneLayer> layers)
        {
            var meshes = new List<(Vector3, Vector3, Vector3[])>();
            foreach (var m in layers.SelectMany(l => l.Meshes).Where(m => m.Pickable && m.Triangles.Length > 0))
            {
                var corners = new Vector3[m.Triangles.Length];
                var min = new Vector3(float.MaxValue);
                var max = new Vector3(float.MinValue);
                for (int i = 0; i < corners.Length; i++)
                {
                    corners[i] = ToView(s, m.Positions[m.Triangles[i]]);
                    min = Vector3.Min(min, corners[i]);
                    max = Vector3.Max(max, corners[i]);
                }
                meshes.Add((min, max, corners));
            }
            return new PickTriangles(meshes);
        }

        /// <summary>Whether there is any triangle.</summary>
        public bool Any => _meshes.Count > 0;

        /// <summary>The nearest triangle a ray meets within a distance (either side), or null.</summary>
        /// <param name="o">The ray's origin, in the view's frame.</param>
        /// <param name="d">Its unit direction.</param>
        /// <param name="maxDist">How far to look.</param>
        public float? Hit(Vector3 o, Vector3 d, float maxDist) => HitFace(o, d, maxDist)?.T;

        /// <summary>The nearest triangle a ray meets within a distance, with the triangle's normal facing the ray (view frame); or null.</summary>
        /// <param name="o">The ray's origin, in the view's frame.</param>
        /// <param name="d">Its unit direction.</param>
        /// <param name="maxDist">How far to look.</param>
        public (float T, Vector3 Normal)? HitFace(Vector3 o, Vector3 d, float maxDist)
        {
            float? best = null;
            var normal = Vector3.UnitY;
            foreach (var (min, max, corners) in _meshes)
            {
                if (!RayMeetsBox(o, d, min, max, best ?? maxDist))
                {
                    continue;
                }
                for (int i = 0; i < corners.Length; i += 3)
                {
                    if (Picking.HitTriangle(o, d, corners[i], corners[i + 1], corners[i + 2]) is float t && t <= (best ?? maxDist))
                    {
                        best = t;
                        var n = Vector3.Normalize(Vector3.Cross(corners[i + 1] - corners[i], corners[i + 2] - corners[i]));
                        normal = Vector3.Dot(n, d) > 0 ? -n : n;
                    }
                }
            }
            return best is float b ? (b, normal) : null;
        }

        /// <summary>Whether a ray meets a box within a distance (slabs).</summary>
        private static bool RayMeetsBox(Vector3 o, Vector3 d, Vector3 min, Vector3 max, float maxDist)
        {
            float lo = 0, hi = maxDist;
            for (int a = 0; a < 3; a++)
            {
                float oa = a == 0 ? o.X : a == 1 ? o.Y : o.Z, da = a == 0 ? d.X : a == 1 ? d.Y : d.Z;
                float mn = a == 0 ? min.X : a == 1 ? min.Y : min.Z, mx = a == 0 ? max.X : a == 1 ? max.Y : max.Z;
                if (MathF.Abs(da) < 1e-9f)
                {
                    if (oa < mn || oa > mx)
                    {
                        return false;
                    }
                    continue;
                }
                float t1 = (mn - oa) / da, t2 = (mx - oa) / da;
                lo = MathF.Max(lo, MathF.Min(t1, t2));
                hi = MathF.Min(hi, MathF.Max(t1, t2));
                if (lo > hi)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
