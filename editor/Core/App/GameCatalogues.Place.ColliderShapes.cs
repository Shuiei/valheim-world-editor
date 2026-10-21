// The placement scan's collider kinds: box, sphere, capsule and mesh, each written as its entry in a piece's colliders (see GameCatalogues).

using AssetsTools.NET;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The placement scan of the build pieces; this part writes each collider kind into a piece's colliders.
    /// </summary>
    internal sealed partial class PlaceScan
    {
        /// <summary>
        /// Writes a box collider: its centre and half size in the piece's frame, and the rotation.
        /// </summary>
        /// <param name="c">The BoxCollider component.</param>
        /// <param name="frame">The frame of the object the collider sits on.</param>
        /// <param name="cols">The colliders written so far, or null.</param>
        internal static void AddBox(AssetTypeValueField c, ColliderFrame frame, List<object?>? cols)
        {
            var (cx, cy, cz) = frame.Point(Vec(c["m_Center"]));
            var (sizeX, sizeY, sizeZ) = Vec(c["m_Size"]);
            var (sx, sy, sz) = frame.Scaling;
            cols?.Add(new List<object?> { "b", R(cx, 4), R(cy, 4), R(cz, 4), R(Math.Abs(sizeX * sx) / 2, 4), R(Math.Abs(sizeY * sy) / 2, 4), R(Math.Abs(sizeZ * sz) / 2, 4) }.Concat(frame.Rotation.Rounded(4)).ToList());
        }

        /// <summary>
        /// Writes a sphere collider: its centre in the piece's frame and its radius, scaled by the largest axis.
        /// </summary>
        /// <param name="c">The SphereCollider component.</param>
        /// <param name="frame">The frame of the object the collider sits on.</param>
        /// <param name="cols">The colliders written so far, or null.</param>
        internal static void AddSphere(AssetTypeValueField c, ColliderFrame frame, List<object?>? cols)
        {
            var (x, y, z) = frame.Point(Vec(c["m_Center"]));
            var (sx, sy, sz) = frame.Scaling;
            cols?.Add(new List<object?> { "s", R(x, 4), R(y, 4), R(z, 4), R(c["m_Radius"].AsFloat * Max([Math.Abs(sx), Math.Abs(sy), Math.Abs(sz)]), 4) });
        }

        /// <summary>
        /// Writes a capsule collider as its segment (the two ends of its axis) and its radius, in the piece's frame.
        /// </summary>
        /// <param name="c">The CapsuleCollider component.</param>
        /// <param name="frame">The frame of the object the collider sits on.</param>
        /// <param name="cols">The colliders written so far, or null.</param>
        internal static void AddCapsule(AssetTypeValueField c, ColliderFrame frame, List<object?>? cols)
        {
            var (x, y, z) = Vec(c["m_Center"]);
            int d = (int)Int(Get(c, "m_Direction", 1L));
            var (sx, sy, sz) = frame.Scaling;
            double[] sa = [sx, sy, sz];
            double rr = c["m_Radius"].AsFloat * Max(Enumerable.Range(0, 3).Where(i => i != d).Select(i => Math.Abs(sa[i])));
            double half = Max(c["m_Height"].AsFloat * Math.Abs(sa[d]) / 2 - rr, 0);
            double[] ax = new double[3];
            ax[d] = sa[d] != 0 ? half / Math.Abs(sa[d]) : 0;
            var (fromX, fromY, fromZ) = frame.Point((x + ax[0], y + ax[1], z + ax[2]));
            var (toX, toY, toZ) = frame.Point((x - ax[0], y - ax[1], z - ax[2]));
            cols?.Add(new List<object?> { "c", R(fromX, 4), R(fromY, 4), R(fromZ, 4), R(toX, 4), R(toY, 4), R(toZ, 4), R(rr, 4) });
        }

        /// <summary>
        /// Writes a mesh collider: its mesh's vertices and triangles in the piece's frame, once the mesh is read. Its mesh
        /// is always noted for the first read.
        /// </summary>
        /// <param name="c">The MeshCollider component.</param>
        /// <param name="mesh">The mesh the collider refers to.</param>
        /// <param name="frame">The frame of the object the collider sits on.</param>
        /// <param name="cols">The colliders written so far, or null when only the meshes are wanted.</param>
        /// <param name="meshes">The mesh ids to read first, or null.</param>
        internal void AddMesh(AssetTypeValueField c, AssetTypeValueField mesh, ColliderFrame frame, List<object?>? cols, List<long>? meshes)
        {
            long id = mesh["m_PathID"].AsLong;
            meshes?.Add(id);
            if (cols == null || !_meshes.TryGetValue(id, out var m))
            {
                return;
            }
            var (vertices, triangles) = m;
            var coords = new List<object?>(vertices.Length);
            for (int i = 0; i + 2 < vertices.Length; i += 3)
            {
                var (x, y, z) = frame.Point((vertices[i], vertices[i + 1], vertices[i + 2]));
                coords.Add(R(x, 4));
                coords.Add(R(y, 4));
                coords.Add(R(z, 4));
            }
            cols.Add(new List<object?> { "m", Truthy(Get(c, "m_Convex", 0L)) ? 1L : 0L, coords, triangles.SelectMany(x => x).Select(i => (object?)(long)i).ToList() });
        }
    }
}
