// The support scan of the build pieces: their support boxes and meshes, and the material and support type of each (see GameCatalogues).

using Dict = TerrainEditor.App.CatalogueJson.Dict;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// What the game's support check needs of each piece, read at the editor's start (GameData), for the
    /// editor's stability check: its material, centre of mass offset, whether it supports other pieces,
    /// whether the game checks its support at all ("f": never), and its colliders as boxes in its own
    /// frame: [centre, half size, rotation, aabb, layer, body] (aabb 1: the bounds of a collider that is not a
    /// box, a mesh collider's as its mesh's box; body 1: a collider on a Rigidbody, which the game's support
    /// check leaves out). Colliders of every child count, inactive ones too;
    /// triggers and snap points do not. The pieces with a WearNTear (told by its class) on their root.
    /// </summary>
    internal sealed partial class SupportScan : IStartScan
    {
        /// <summary>
        /// The entries, by piece prefab name.
        /// </summary>
        private readonly Dict _entries = new();
        /// <summary>
        /// The bounds of the meshes of the bundle's mesh colliders, by path id.
        /// </summary>
        private readonly Dictionary<long, (V3 C, V3 H)> _meshes = [];

        /// <summary>
        /// The "piece-support" JSON.
        /// </summary>
        public string Json() => CatalogueJson.Write(_entries, new CatalogueJson.Style(",", ":"));

        /// <summary>
        /// Reads the bundle's pieces with a WearNTear: their mesh colliders' bounds first, then each piece's entry.
        /// </summary>
        public void Read(Bundle b, Game game)
        {
            var pieces = PieceRoots(b, game).Select(p => (p.Pid, p.Go, Wnt: b.ComponentIds(p.Go).FirstOrDefault(c => c.Type == MonoBehaviourType && game.ScriptClass(b, c.Pid) == "WearNTear").Pid))
                .Where(p => p.Wnt != 0).ToList();
            b.ReadInOrder(pieces.Select(p => p.Wnt));
            pieces.RemoveAll(p => b.Field(p.Wnt) is not { } w || !Has(w, "m_materialType") || !Has(w, "m_supports"));
            PieceTrees(b, game);
            // The mesh colliders' meshes, only their bounds kept (their vertices are not needed).
            _meshes.Clear();
            var meshes = new List<long>();
            foreach (var (pid, _, _) in pieces)
            {
                Walk(b, pid, default, Quat.Root, (1, 1, 1), true, false, null, meshes);
            }
            b.ReadInOrder(meshes, (pid, m) => _meshes[pid] = (Vec(m["m_LocalAABB.m_Center"]), Vec(m["m_LocalAABB.m_Extent"])));
            foreach (var (pid, go, wntPid) in pieces)
            {
                var wnt = b.Field(wntPid)!;
                var boxes = new List<object?>();
                Walk(b, pid, default, Quat.Root, (1, 1, 1), true, false, boxes, null);
                var (x, y, z) = Has(wnt, "m_comOffset") ? Vec(wnt["m_comOffset"]) : default;
                var entry = new Dict
                {
                    ["m"] = Py(wnt["m_materialType"]),
                    ["s"] = Int(Get(wnt, "m_supports", 1L)),
                    ["c"] = Has(wnt, "m_comOffset") ? [R(x, 4), R(y, 4), R(z, 4)] : Origin(),
                    ["b"] = boxes,
                };
                // The game never checks its support (m_noSupportWear off): it never falls, and keeps its
                // full support for what rests on it (black marble's large floor, some dungeon pieces).
                if (!Truthy(Get(wnt, "m_noSupportWear", 1L)))
                {
                    entry["f"] = 1L;
                }
                // The first of a name, as every scan takes it.
                _entries.TryAdd(go["m_Name"].AsString, entry);
            }
        }
    }
}
