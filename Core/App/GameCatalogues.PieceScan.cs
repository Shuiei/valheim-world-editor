// What the editor reads of the build pieces: the menus, the categories and the snap points (see GameCatalogues).

using Dict = TerrainEditor.App.CatalogueJson.Dict;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The build pieces, read at the editor's start (GameData): each piece's category, comfort, snap
    /// points (children tagged "snappoint", inactive ones too) and footprint (the box around its active
    /// box colliders), in its own frame; and which build tool's menu has it ("tool": the game's
    /// PieceTables), for the builder the editor writes on new pieces of these kinds, as the game does when
    /// a player places one. Menu entries that are not networked prefabs (repair, remove, the hoe's ground
    /// tools) are left out.
    /// </summary>
    internal sealed partial class PieceScan : IStartScan
    {
        /// <summary>
        /// The build tools' PieceTable objects, by name, and the tool each stands for.
        /// </summary>
        private static readonly Dictionary<string, string> Tables = new() { ["_HammerPieceTable"] = "hammer", ["_HoePieceTable"] = "hoe", ["_CultivatorPieceTable"] = "cultivator", ["_FeasterPieceTable"] = "feaster" };

        /// <summary>
        /// The entries, by piece prefab name.
        /// </summary>
        private readonly Dict _entries = new();
        /// <summary>
        /// Each piece's prefab by where it is (what the menus name).
        /// </summary>
        private readonly Dictionary<Ref, string> _names = [];
        /// <summary>
        /// The pieces with a ZNetView (only these get a "tool").
        /// </summary>
        private readonly HashSet<string> _networked = new(StringComparer.Ordinal);
        /// <summary>
        /// Each tool's menu: the pieces it lists.
        /// </summary>
        private readonly Dictionary<string, List<Ref>> _tables = [];

        /// <summary>
        /// Reads the bundle's pieces (category, comfort, snap points, box) and the build tools' menus in it.
        /// </summary>
        public void Read(Bundle b, Game game)
        {
            var pieces = PieceRoots(b, game);
            PieceTrees(b, game);
            foreach (var (f, pid, go, piece) in pieces)
            {
                string name = go["m_Name"].AsString;
                var pts = new List<V3>();
                var snaps = new List<object?>();
                Walk(b, pid, default, Quat.Root, (1, 1, 1), true, pts, snaps);
                var entry = new Dict { ["cat"] = Py(piece["m_category"]), ["comfort"] = Get(piece, "m_comfort", 0L) };
                if (snaps.Count > 0)
                {
                    entry["snap"] = snaps;
                }
                if (pts.Count > 0)
                {
                    entry["box"] = new List<object?> { R(Min(pts.Select(q => q.X)), 3), R(Max(pts.Select(q => q.X)), 3), R(Min(pts.Select(q => q.Z)), 3), R(Max(pts.Select(q => q.Z)), 3), R(Min(pts.Select(q => q.Y)), 3), R(Max(pts.Select(q => q.Y)), 3) };
                }
                // The first of a name, as every scan takes it (PrefabScan too): "prefabs" and "pieces" describe one object.
                _entries.TryAdd(name, entry);
                _names[new Ref(f.Cab, pid)] = name;
                if (b.ComponentIds(go).Any(c => c.Type == MonoBehaviourType && game.ScriptClass(b, c.Pid) == "ZNetView"))
                {
                    _networked.Add(name);
                }
            }
            // The menus: PieceTables (told by their class) on the build tools' table objects.
            foreach (var (f, info) in b.Objects)
            {
                if (info.TypeId != MonoBehaviourType || game.ScriptClass(b, f, info) != "PieceTable" || b.Field(f, info.PathId) is not { } d || !Has(d, "m_pieces"))
                {
                    continue;
                }
                if (b.Field(f, d["m_GameObject.m_PathID"].AsLong) is { } owner && Tables.TryGetValue(owner["m_Name"].AsString, out string? tool))
                {
                    _tables[tool] = [.. d["m_pieces.Array"].Children.Select(p => new Ref(f.FileOf(p["m_FileID"].AsInt) ?? "", p["m_PathID"].AsLong))];
                }
            }
        }

        /// <summary>
        /// The "pieces" JSON, each networked piece with the tool whose menu has it (the hammer's when several do).
        /// </summary>
        public string Json()
        {
            // A piece in several menus: the hammer's.
            foreach (string tool in new[] { "feaster", "cultivator", "hoe", "hammer" })
            {
                foreach (var r in _tables.GetValueOrDefault(tool) ?? [])
                {
                    if (_names.TryGetValue(r, out string? n) && _networked.Contains(n))
                    {
                        ((Dict)_entries[n]!)["tool"] = tool;
                    }
                }
            }
            return CatalogueJson.Write(_entries, new CatalogueJson.Style());
        }
    }
}
