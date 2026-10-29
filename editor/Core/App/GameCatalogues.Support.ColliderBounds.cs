// The support scan's collider bounds: the centre and half size of each collider kind, in its own frame (see GameCatalogues).

using AssetsTools.NET;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The support scan of the build pieces; this part gives each collider kind its bounds.
    /// </summary>
    internal sealed partial class SupportScan
    {
        /// <summary>
        /// The bounds of one collider: its centre, its half size and the aabb flag (0 for a box, 1 for a collider whose
        /// bounds come from its shape or its mesh), in its own frame. Null for a mesh collider with no mesh to use.
        /// </summary>
        /// <param name="t">The collider's component type.</param>
        /// <param name="cm">The collider component.</param>
        /// <param name="meshes">The mesh ids to read first, or null.</param>
        internal (V3 C, V3 H, long Aabb)? ColliderBounds(int t, AssetTypeValueField cm, List<long>? meshes)
        {
            if (t == BoxColliderType)
            {
                return BoxBounds(cm);
            }
            if (t == MeshColliderType)
            {
                return MeshBounds(cm, meshes);
            }
            if (t == SphereColliderType)
            {
                double rr = cm["m_Radius"].AsFloat;
                return (Vec(cm["m_Center"]), (rr, rr, rr), 1L);
            }
            return CapsuleBounds(cm);
        }

        /// <summary>
        /// A box collider's bounds: its centre and half its size, as the aabb flag 0.
        /// </summary>
        /// <param name="cm">The BoxCollider component.</param>
        internal static (V3 C, V3 H, long Aabb) BoxBounds(AssetTypeValueField cm)
        {
            var (x, y, z) = Vec(cm["m_Size"]);
            return (Vec(cm["m_Center"]), (x / 2, y / 2, z / 2), 0L);
        }

        /// <summary>
        /// A mesh collider's bounds: those of its mesh, read once. The mesh is noted for the first read even when its
        /// bounds are not there yet; a mesh from another file, or none, has no bounds.
        /// </summary>
        /// <param name="cm">The MeshCollider component.</param>
        /// <param name="meshes">The mesh ids to read first, or null.</param>
        internal (V3 C, V3 H, long Aabb)? MeshBounds(AssetTypeValueField cm, List<long>? meshes)
        {
            var mesh = cm["m_Mesh"];
            if (mesh.IsDummy || mesh["m_FileID"].AsInt != 0)
            {
                return null;
            }
            meshes?.Add(mesh["m_PathID"].AsLong);
            if (!_meshes.TryGetValue(mesh["m_PathID"].AsLong, out var bounds))
            {
                return null;
            }
            return (bounds.C, bounds.H, 1L);
        }

        /// <summary>
        /// A capsule collider's bounds: its centre, and half its height along its direction (at least its radius), as the
        /// aabb flag 1.
        /// </summary>
        /// <param name="cm">The CapsuleCollider component.</param>
        internal static (V3 C, V3 H, long Aabb) CapsuleBounds(AssetTypeValueField cm)
        {
            double rr = cm["m_Radius"].AsFloat;
            double hh = Max(cm["m_Height"].AsFloat / 2.0, rr);
            long d = Int(Get(cm, "m_Direction", 1L));
            return (Vec(cm["m_Center"]), (d == 0 ? hh : rr, d == 1 ? hh : rr, d == 2 ? hh : rr), 1L);
        }
    }
}
