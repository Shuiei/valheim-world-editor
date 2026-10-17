// The placement scan of the build pieces: their walkable, clipping and water flags, and their solid meshes (see GameCatalogues).

using AssetsTools.NET;
using Dict = TerrainEditor.App.CatalogueJson.Dict;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The build ray's layers (Default, static_solid, Default_small, piece, piece_nonsolid, terrain, vehicle).
    /// </summary>
    private static readonly HashSet<long> RayLayers = [0, 15, 20, 10, 16, 11, 28];

    /// <summary>
    /// How the game's hammer places each piece, read at the editor's start (GameData), for the Workshop's Build: its
    /// placement flags, its snap points (its direct children tagged "snappoint") and the colliders the build ray and
    /// the ghost use (enabled, not triggers, on an active object, on a layer of the build ray), in the piece's own
    /// frame (its root at the origin, its root scale applied). Colliders: ["b", centre, half size, rotation] a box;
    /// ["s", centre, r] a sphere; ["c", a, b, r] a capsule (its segment); ["m", convex, [x,y,z...], [i,j,k...]] a mesh
    /// (triangles).
    /// </summary>
    private sealed class PlaceScan : IStartScan
    {
        /// <summary>
        /// The placement flags written ("f": key -&gt; 1 when the Piece field is set).
        /// </summary>
        private static readonly (string Key, string Field)[] Flags = [("g", "m_groundPiece"), ("cg", "m_clipGround"), ("ce", "m_clipEverything"), ("ro", "m_allowRotatedOverlap"), ("w", "m_waterPiece"), ("nw", "m_noInWater")];

        /// <summary>
        /// The entries, by piece prefab name.
        /// </summary>
        private readonly Dict _entries = new();
        /// <summary>
        /// The bundle's mesh colliders' meshes, by path id: vertices and triangles (each read once, not
        /// kept as fields).
        /// </summary>
        private readonly Dictionary<long, (float[] Vertices, List<int[]> Triangles)> _meshes = [];

        /// <summary>
        /// The "piece-place" JSON.
        /// </summary>
        public string Json() => CatalogueJson.Write(_entries, new CatalogueJson.Style(",", ":"));

        /// <summary>
        /// Reads the bundle's pieces: their mesh colliders' meshes first, then each piece's flags, snap points and
        /// colliders.
        /// </summary>
        public void Read(Bundle b, Game game)
        {
            var pieces = PieceRoots(b, game);
            PieceTrees(b, game);
            // The meshes first, in the order of the file.
            _meshes.Clear();
            var meshes = new List<long>();
            foreach (var (_, pid, go, _) in pieces)
            {
                if (RootTransform(b, go) is { } root)
                {
                    Walk(b, pid, default, Quat.Root, Vec(root["m_LocalScale"]), true, null, meshes);
                }
            }
            b.ReadInOrder(meshes, (pid, m) =>
            {
                if (MeshData(b, m) is { } mesh)
                {
                    _meshes[pid] = mesh;
                }
            });
            foreach (var (_, pid, go, piece) in pieces)
            {
                if (RootTransform(b, go) is not { } root)
                {
                    continue;
                }
                var rs = Vec(root["m_LocalScale"]);
                var snaps = new List<object?>();
                foreach (var ch in root["m_Children.Array"].Children)
                {
                    if (b.Field(ch["m_PathID"].AsLong) is not { } ct)
                    {
                        continue;
                    }
                    if (b.Field(ct["m_GameObject.m_PathID"].AsLong) is { } g && Get(g, "m_Tag", null) is long tag && tag == SnapTag)
                    {
                        var (x, y, z) = Vec(ct["m_LocalPosition"]);
                        snaps.Add(new List<object?> { R(x * rs.X, 4), R(y * rs.Y, 4), R(z * rs.Z, 4) });
                    }
                }
                var cols = new List<object?>();
                Walk(b, pid, default, Quat.Root, rs, true, cols, null);
                var flags = new Dict();
                foreach (var (key, field) in Flags)
                {
                    if (Truthy(Get(piece, field, 0L)))
                    {
                        flags[key] = 1L;
                    }
                }
                // The first of a name, as every scan takes it.
                _entries.TryAdd(go["m_Name"].AsString, new Dict { ["f"] = flags, ["s"] = snaps, ["c"] = cols });
            }
        }

        /// <summary>
        /// A GameObject's Transform, read whole.
        /// </summary>
        private static AssetTypeValueField? RootTransform(Bundle b, AssetTypeValueField go) =>
            b.ComponentIds(go).Where(c => c.Type == TransformType).Select(c => b.Field(c.Pid)).FirstOrDefault(f => f != null);

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
                V3 P(V3 v) => Add(q.Rotate(Scale(v, s)), pp);
                foreach (var (t, c) in colliders)
                {
                    if (t == BoxColliderType)
                    {
                        var (cx, cy, cz) = P(Vec(c["m_Center"]));
                        var (sizeX, sizeY, sizeZ) = Vec(c["m_Size"]);
                        cols?.Add(new List<object?> { "b", R(cx, 4), R(cy, 4), R(cz, 4), R(Math.Abs(sizeX * s.X) / 2, 4), R(Math.Abs(sizeY * s.Y) / 2, 4), R(Math.Abs(sizeZ * s.Z) / 2, 4) }.Concat(q.Rounded(4)).ToList());
                    }
                    else if (t == SphereColliderType)
                    {
                        var (x, y, z) = P(Vec(c["m_Center"]));
                        cols?.Add(new List<object?> { "s", R(x, 4), R(y, 4), R(z, 4), R(c["m_Radius"].AsFloat * Max([Math.Abs(s.X), Math.Abs(s.Y), Math.Abs(s.Z)]), 4) });
                    }
                    else if (t == CapsuleColliderType)
                    {
                        var (x, y, z) = Vec(c["m_Center"]);
                        int d = (int)Int(Get(c, "m_Direction", 1L));
                        double[] sa = [s.X, s.Y, s.Z];
                        double rr = c["m_Radius"].AsFloat * Max(Enumerable.Range(0, 3).Where(i => i != d).Select(i => Math.Abs(sa[i])));
                        double half = Max(c["m_Height"].AsFloat * Math.Abs(sa[d]) / 2 - rr, 0);
                        double[] ax = new double[3];
                        ax[d] = sa[d] != 0 ? half / Math.Abs(sa[d]) : 0;
                        var (fromX, fromY, fromZ) = P((x + ax[0], y + ax[1], z + ax[2]));
                        var (toX, toY, toZ) = P((x - ax[0], y - ax[1], z - ax[2]));
                        cols?.Add(new List<object?> { "c", R(fromX, 4), R(fromY, 4), R(fromZ, 4), R(toX, 4), R(toY, 4), R(toZ, 4), R(rr, 4) });
                    }
                    else if (c["m_Mesh"] is { IsDummy: false } mesh && mesh["m_FileID"].AsInt == 0 && b.TypeOf(mesh["m_PathID"].AsLong) == MeshType)
                    {
                        long id = mesh["m_PathID"].AsLong;
                        meshes?.Add(id);
                        if (cols == null || !_meshes.TryGetValue(id, out var m))
                        {
                            continue;
                        }
                        var (vertices, triangles) = m;
                        var coords = new List<object?>(vertices.Length);
                        for (int i = 0; i + 2 < vertices.Length; i += 3)
                        {
                            var (x, y, z) = P((vertices[i], vertices[i + 1], vertices[i + 2]));
                            coords.Add(R(x, 4));
                            coords.Add(R(y, 4));
                            coords.Add(R(z, 4));
                        }
                        cols.Add(new List<object?> { "m", Truthy(Get(c, "m_Convex", 0L)) ? 1L : 0L, coords, triangles.SelectMany(x => x).Select(i => (object?)(long)i).ToList() });
                    }
                }
            }
            foreach (long child in ChildObjects(b, tr))
            {
                Walk(b, child, pp, q, s, false, cols, meshes);
            }
        }
    }
}
