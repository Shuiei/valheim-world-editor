// The placement scan of the build pieces: their walkable, clipping and water flags, and their solid meshes (see GameCatalogues).

using AssetsTools.NET;
using Dict = TerrainEditor.App.CatalogueJson.Dict;

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
    private sealed partial class PlaceScan : IStartScan
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
    }
}
