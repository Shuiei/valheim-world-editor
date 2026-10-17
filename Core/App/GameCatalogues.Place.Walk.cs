// The placement scan's walk down a piece: the colliders the build ray meets, each in the piece's own frame (see GameCatalogues).

using AssetsTools.NET;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The placement scan of the build pieces; this part walks a piece's objects and writes their colliders.
    /// </summary>
    internal sealed partial class PlaceScan
    {
        /// <summary>
        /// A piece's colliders; or (meshes) the meshes its mesh colliders need, to read first.
        /// </summary>
        private void Walk(Bundle b, long goPid, V3 pos, Quat rot, V3 scale, bool root, List<object?>? cols, List<long>? meshes)
        {
            if (b.Field(goPid) is not { } g || !Truthy(Get(g, "m_IsActive", 1L)))
            {
                return;
            }
            // Only the Transform and colliders read (not the renderers, scripts...).
            var (tr, colliders) = TransformAndColliders(b, g);
            if (tr == null)
            {
                return;
            }
            var (pp, q, s) = root ? (default(V3), Quat.Root, scale) : ChildFrame(tr, pos, rot, scale);
            if (RayLayers.Contains(Int(Get(g, "m_Layer", 0L))))
            {
                AddColliders(b, colliders, new ColliderFrame(pp, q, s), cols, meshes);
            }
            foreach (long child in ChildObjects(b, tr))
            {
                Walk(b, child, pp, q, s, false, cols, meshes);
            }
        }

        /// <summary>
        /// Writes the colliders of one object, each by its kind; a mesh collider's mesh is noted in
        /// <paramref name="meshes"/> so that it is read first.
        /// </summary>
        /// <param name="b">The bundle the object is in.</param>
        /// <param name="colliders">The object's colliders: the component type and the component.</param>
        /// <param name="frame">The frame the object sits in, within the piece.</param>
        /// <param name="cols">The colliders written so far, or null when only the meshes are wanted.</param>
        /// <param name="meshes">The mesh ids to read first, or null.</param>
        internal void AddColliders(Bundle b, List<(int Type, AssetTypeValueField F)> colliders, ColliderFrame frame, List<object?>? cols, List<long>? meshes)
        {
            foreach (var (t, c) in colliders)
            {
                if (t == BoxColliderType)
                {
                    AddBox(c, frame, cols);
                }
                else if (t == SphereColliderType)
                {
                    AddSphere(c, frame, cols);
                }
                else if (t == CapsuleColliderType)
                {
                    AddCapsule(c, frame, cols);
                }
                else if (c["m_Mesh"] is { IsDummy: false } mesh && mesh["m_FileID"].AsInt == 0 && b.TypeOf(mesh["m_PathID"].AsLong) == MeshType)
                {
                    AddMesh(c, mesh, frame, cols, meshes);
                }
            }
        }
    }
}
