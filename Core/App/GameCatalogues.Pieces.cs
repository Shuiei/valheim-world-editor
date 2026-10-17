using System.Buffers.Binary;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Dict = TerrainEditor.App.CatalogueJson.Dict;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

// What the editor reads of the build pieces (every prefab whose root has a Piece component) at its
// start (GameData): the pieces, their support, their placement and their costs.
public static partial class GameCatalogues
{
    /// <summary>
    /// Unity's class id of a BoxCollider.
    /// </summary>
    private const int BoxColliderType = (int)AssetClassID.BoxCollider;
    /// <summary>
    /// Unity's class id of a MeshCollider.
    /// </summary>
    private const int MeshColliderType = (int)AssetClassID.MeshCollider;
    /// <summary>
    /// Unity's class id of a SphereCollider.
    /// </summary>
    private const int SphereColliderType = (int)AssetClassID.SphereCollider;
    /// <summary>
    /// Unity's class id of a CapsuleCollider.
    /// </summary>
    private const int CapsuleColliderType = (int)AssetClassID.CapsuleCollider;
    /// <summary>
    /// Unity's class id of a Mesh.
    /// </summary>
    private const int MeshType = (int)AssetClassID.Mesh;

    /// <summary>
    /// Whether a Unity class id is one of the colliders read here (box, mesh, sphere, capsule).
    /// </summary>
    private static bool IsCollider(int type) => type is BoxColliderType or MeshColliderType or SphereColliderType or CapsuleColliderType;

    /// <summary>
    /// A collider that counts: enabled, not a trigger.
    /// </summary>
    private static bool Solid(AssetTypeValueField c) => Num(Get(c, "m_Enabled", 1L)) == 1 && !Truthy(Get(c, "m_IsTrigger", 0L));

    /// <summary>
    /// A snap point: tagged "snappoint" (<c>SnapTag</c>), or with "snappoint" in its name.
    /// </summary>
    private static bool IsSnapPoint(AssetTypeValueField go) => Get(go, "m_Tag", null) is long tag && tag == SnapTag || go["m_Name"].AsString.ToLowerInvariant().Contains("snappoint", StringComparison.Ordinal);

    /// <summary>
    /// Whether a GameObject is active (m_IsActive, true when missing).
    /// </summary>
    private static bool Active(AssetTypeValueField go) => Num(Get(go, "m_IsActive", 1L)) == 1;

    /// <summary>
    /// Each prefab with a Piece on its root, in the bundle's order: its file, path id, fields and Piece,
    /// for the readers at the editor's start. Found from the Piece scripts (told by their class) up to
    /// their GameObjects, kept to the prefab roots (the locations have many pieces below theirs); only
    /// these are read whole.
    /// </summary>
    private static List<(Bundle.Inner File, long Pid, AssetTypeValueField Go, AssetTypeValueField Piece)> PieceRoots(Bundle b, Game game) =>
        PieceRootsOf.GetValue(b, _ => FindPieceRoots(b, game));

    /// <summary>
    /// Found once for the readers that need them (pieces, piece-support, piece-place, piece-cost).
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Bundle, List<(Bundle.Inner, long, AssetTypeValueField, AssetTypeValueField)>> PieceRootsOf = [];

    /// <summary>
    /// Finds a bundle's piece roots: see <c>PieceRoots</c>.
    /// </summary>
    private static List<(Bundle.Inner, long, AssetTypeValueField, AssetTypeValueField)> FindPieceRoots(Bundle b, Game game)
    {
        var res = new List<(Bundle.Inner, long, AssetTypeValueField, AssetTypeValueField)>();
        var scripts = new HashSet<long>();
        var owners = new List<(Bundle.Inner, long)>();
        foreach (var (f, info) in b.Objects)
        {
            if (info.TypeId == MonoBehaviourType && game.ScriptClass(b, f, info) == "Piece" && Layout.First(f, info) is (0, var go))
            {
                scripts.Add(info.PathId);
                owners.Add((f, go));
            }
        }
        if (owners.Count == 0)
        {
            return res;
        }
        var roots = Layout.Roots(owners);
        Layout.Check(b, owners[0].Item2, roots);
        foreach (var (f, info) in b.Objects)
        {
            if (info.TypeId != GameObjectType || !roots.Contains(info.PathId) || b.Field(f, info.PathId) is not { } go)
            {
                continue;
            }
            var piece = b.ComponentIds(go).Where(c => scripts.Contains(c.Pid)).Select(c => b.Field(c.Pid)).FirstOrDefault(p => p != null && IsPiece(p));
            if (piece != null)
            {
                res.Add((f, info.PathId, go, piece));
            }
        }
        return res;
    }

    /// <summary>
    /// What the pieces' readers walk below the prefabs (pieces, piece-support, piece-place), read once for
    /// all, first, each time in the order of the file (the bundle is read in blocks, and a prefab's objects are far
    /// apart): a level's Transforms, the GameObjects of the level above and the colliders of the one above
    /// that, as many times as there are levels. Below snap points nothing is read; a level is read before
    /// its parent's GameObject tells whether to go there.
    /// </summary>
    private static void PieceTrees(Bundle b, Game game) => TreesRead.GetValue(b, _ =>
    {
        IEnumerable<long> Of(long go, Func<int, bool> type) => b.Field(go) is { } g ? b.ComponentIds(g).Where(c => type(c.Type)).Select(c => c.Pid) : [];
        var gos = PieceRoots(b, game).Select(p => p.Pid).ToList();
        // The Transforms to read, with their parent's GameObject (0 for a prefab's root).
        var transforms = gos.SelectMany(g => Of(g, t => t == TransformType)).Select(t => (Pid: t, Parent: 0L)).ToList();
        var cols = new List<long>();
        while (transforms.Count + gos.Count + cols.Count > 0)
        {
            b.ReadInOrder(transforms.Select(t => t.Pid).Concat(gos).Concat(cols));
            cols = [.. gos.SelectMany(g => Of(g, IsCollider))];
            gos = [];
            var next = new List<(long Pid, long Parent)>();
            foreach (var (t, parent) in transforms)
            {
                if ((parent == 0 || b.Field(parent) is { } p && !IsSnapPoint(p)) && b.Field(t) is { } tr)
                {
                    long go = tr["m_GameObject.m_PathID"].AsLong;
                    gos.Add(go);
                    next.AddRange(tr["m_Children.Array"].Children.Select(c => (c["m_PathID"].AsLong, go)));
                }
            }
            transforms = next;
        }
        return new object();
    });

    /// <summary>
    /// The bundles <c>PieceTrees</c> has read already.
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Bundle, object> TreesRead = [];

    /// <summary>
    /// A child's frame from its parent's: (position, rotation, scale), all in the prefab's frame.
    /// </summary>
    private static (V3 P, Quat Q, V3 S) ChildFrame(AssetTypeValueField tr, V3 pos, Quat rot, V3 scale)
    {
        var p = Add(rot.Rotate(Scale(Vec(tr["m_LocalPosition"]), scale)), pos);
        return (p, rot.Mul(Quat.Of(tr["m_LocalRotation"])), Scale(scale, Vec(tr["m_LocalScale"])));
    }

    /// <summary>
    /// A prefab root's position as the files have it: whole numbers.
    /// </summary>
    private static List<object?> Origin() => [0L, 0L, 0L];

    /// <summary>
    /// The first smallest (as Python's min).
    /// </summary>
    private static double Min(IEnumerable<double> xs) => xs.Aggregate((a, x) => x < a ? x : a);

    /// <summary>
    /// The first largest (as Python's max).
    /// </summary>
    private static double Max(IEnumerable<double> xs) => xs.Aggregate((a, x) => x > a ? x : a);

    /// <summary>
    /// The build pieces, read at the editor's start (GameData): each piece's category, comfort, snap
    /// points (children tagged "snappoint", inactive ones too) and footprint (the box around its active
    /// box colliders), in its own frame; and which build tool's menu has it ("tool": the game's
    /// PieceTables), for the builder the editor writes on new pieces of these kinds, as the game does when
    /// a player places one. Menu entries that are not networked prefabs (repair, remove, the hoe's ground
    /// tools) are left out.
    /// </summary>
    private sealed class PieceScan : IStartScan
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

        /// <summary>
        /// Walks a piece's tree: the corners of its active, solid box colliders into <c>pts</c> and its snap points
        /// (active or not) into <c>snaps</c>, in the piece's frame; nothing below a snap point.
        /// </summary>
        private static void Walk(Bundle b, long goPid, V3 pos, Quat rot, V3 scale, bool root, List<V3> pts, List<object?> snaps)
        {
            if (b.Field(goPid) is not { } g)
            {
                return;
            }
            // Snap points are inactive: looked at before inactive branches are left out.
            bool snap = IsSnapPoint(g);
            if (!Active(g) && !snap)
            {
                return;
            }
            // Only the Transform and box colliders read (not the renderers, scripts...).
            AssetTypeValueField? tr = null;
            var boxes = new List<AssetTypeValueField>();
            foreach (var (t, pid) in b.ComponentIds(g))
            {
                if (t == TransformType && b.Field(pid) is { } f)
                {
                    tr = f;
                }
                else if (t == BoxColliderType && b.Field(pid) is { } c && Solid(c))
                {
                    boxes.Add(c);
                }
            }
            if (tr == null)
            {
                return;
            }
            var (p, r, s) = root ? (default(V3), Quat.Root, (V3)(1.0, 1.0, 1.0)) : ChildFrame(tr, pos, rot, scale);
            if (snap)
            {
                snaps.Add(root ? Origin() : [R(p.X, 3), R(p.Y, 3), R(p.Z, 3)]);
                return;
            }
            foreach (var box in boxes)
            {
                var (cx, cy, cz) = Vec(box["m_Center"]);
                var (sizeX, sizeY, sizeZ) = Vec(box["m_Size"]);
                foreach (double dx in new[] { -0.5, 0.5 })
                {
                    foreach (double dy in new[] { -0.5, 0.5 })
                    {
                        foreach (double dz in new[] { -0.5, 0.5 })
                        {
                            var v = r.Rotate((cx + dx * sizeX) * s.X, (cy + dy * sizeY) * s.Y, (cz + dz * sizeZ) * s.Z);
                            pts.Add(Add(v, p));
                        }
                    }
                }
            }
            foreach (long child in ChildObjects(b, tr))
            {
                Walk(b, child, p, r, s, false, pts, snaps);
            }
        }
    }

    /// <summary>
    /// What build pieces cost, read at the editor's start (GameData): each piece's English name, the
    /// crafting station it needs nearby and its resources (item and amount, and whether it is given back),
    /// items by prefab name with their English names, for the blueprint library's material cost.
    /// </summary>
    private sealed class CostScan : IStartScan
    {
        /// <summary>
        /// The game's English texts, by token without "$".
        /// </summary>
        private readonly Dictionary<string, string> _english;
        /// <summary>
        /// The pieces' entries and the items' English names, by prefab name.
        /// </summary>
        private readonly Dict _pieces = new(), _items = new();

        /// <summary>
        /// A reader that names things from these English texts.
        /// </summary>
        public CostScan(Dictionary<string, string> english) => _english = english;

        /// <summary>
        /// The "piece-cost" JSON, non-ASCII written as it is.
        /// </summary>
        public string Json() => CatalogueJson.Write(new Dict { ["pieces"] = _pieces, ["items"] = _items }, new CatalogueJson.Style(",", ":", SortKeys: true, Ascii: false));

        /// <summary>
        /// A localisation token ("$item_wood") as its English text; the token itself when it has none, a plain text as
        /// it is.
        /// </summary>
        private string En(string? token)
        {
            string t = Localization.Strip(token ?? "");
            return t.StartsWith('$') ? _english.GetValueOrDefault(t[1..], t) : t;
        }

        /// <summary>
        /// Reads the cost of each piece in the bundle.
        /// </summary>
        public void Read(Bundle b, Game game)
        {
            foreach (var (_, _, go, piece) in PieceRoots(b, game))
            {
                Add(b, go["m_Name"].AsString, piece);
            }
        }

        /// <summary>
        /// Adds one piece's entry: English name "n", resources "r" ([item, amount, recover]) and crafting station
        /// "st" when it has one; each resource item's English name goes into the items.
        /// </summary>
        private void Add(Bundle b, string name, AssetTypeValueField piece)
        {
            var res = new List<object?>();
            foreach (var r in piece["m_resources.Array"].Children)
            {
                var (item, drop) = Owner(b, r["m_resItem"]);
                if (string.IsNullOrEmpty(item) || Num(Get(r, "m_amount", 0L)) <= 0)
                {
                    continue;
                }
                if (!_items.ContainsKey(item))
                {
                    var shared = drop!["m_itemData"]["m_shared"];
                    _items[item] = En(Has(shared, "m_name") ? shared["m_name"].AsString : item);
                }
                res.Add(new List<object?> { item, Py(r["m_amount"]), Get(r, "m_recover", 1L) });
            }
            var entry = new Dict { ["n"] = En(Has(piece, "m_name") ? piece["m_name"].AsString : name), ["r"] = res };
            if (Owner(b, piece["m_craftingStation"]).Component is { } station && station.Children.Count > 0)
            {
                entry["st"] = En(Has(station, "m_name") ? station["m_name"].AsString : "");
            }
            _pieces[name] = entry;
        }

        /// <summary>
        /// The GameObject a component reference (same file only) is on: its name, and the component.
        /// </summary>
        private static (string? Name, AssetTypeValueField? Component) Owner(Bundle b, AssetTypeValueField pptr)
        {
            if (pptr["m_FileID"].AsInt != 0 || b.Field(pptr["m_PathID"].AsLong) is not { } c)
            {
                return (null, null);
            }
            return (b.Field(c["m_GameObject.m_PathID"].AsLong)?["m_Name"].AsString, c);
        }
    }

    /// <summary>
    /// Objects read as laid out, a few numbers each rather than their whole fields: for the readers at
    /// the editor's start. The bundle is read in blocks, so they are read in the order of their file.
    /// </summary>
    private static class Layout
    {
        /// <summary>
        /// An object's first reference (a component's m_GameObject): its file id and path id.
        /// </summary>
        public static (int FileId, long PathId) First(Bundle.Inner f, AssetFileInfo info)
        {
            var r = At(f, info);
            return (r.ReadInt32(), r.ReadInt64());
        }

        /// <summary>
        /// The prefab roots among these GameObjects (each in its file): those whose Transform has no
        /// parent. A GameObject has its components first (a count, then each a file id and a path id); a
        /// Transform has its parent after its GameObject, rotation, position, scale and children.
        /// </summary>
        public static HashSet<long> Roots(IEnumerable<(Bundle.Inner File, long Go)> gos)
        {
            var transforms = new List<(Bundle.Inner, AssetFileInfo?, long)>();
            foreach (var (f, info, go) in InOrder(gos.Distinct().Select(g => (g.File, (AssetFileInfo?)g.File.Assets.file.GetAssetInfo(g.Go), g.Go))))
            {
                var r = At(f, info);
                for (int i = r.ReadInt32(); i > 0; i--)
                {
                    int file = r.ReadInt32();
                    long pid = r.ReadInt64();
                    if (file == 0 && f.Assets.file.GetAssetInfo(pid) is { TypeId: TransformType } t)
                    {
                        transforms.Add((f, t, go));
                        break;
                    }
                }
            }
            var roots = new HashSet<long>();
            foreach (var (f, info, go) in InOrder(transforms))
            {
                var r = At(f, info);
                r.Position += 12 + 40;
                int children = r.ReadInt32();
                r.Position += 12L * children;
                if (r.ReadInt32() == 0 && r.ReadInt64() == 0)
                {
                    roots.Add(go);
                }
            }
            return roots;
        }

        /// <summary>
        /// The layout read as the type tree says, for one GameObject: whether it is a root agrees with
        /// Roots. A game made with another Unity could lay objects out otherwise: said, rather than read
        /// wrong. Throws InvalidDataException when it does not.
        /// </summary>
        public static void Check(Bundle b, long go, HashSet<long> roots)
        {
            if (b.Field(go) is not { } g || b.ComponentIds(g).FirstOrDefault(c => c.Type == TransformType) is not (_, long tr and not 0) || b.Field(tr) is not { } t)
            {
                return;
            }
            if ((t["m_Father.m_PathID"].AsLong == 0) != roots.Contains(go))
            {
                throw new InvalidDataException("The game's objects are not laid out as the editor expects (another Unity version?).");
            }
        }

        /// <summary>
        /// A bundle's tree, read raw: each GameObject's name and components (file id, path id) and each
        /// Transform's GameObject, whether it is a root (no parent) and its children, by file and path id.
        /// </summary>
        /// <param name="GameObjects">Each GameObject's name and components, by file and path id.</param>
        /// <param name="Transforms">Each Transform's GameObject, whether it is a root and its children, by file and
        /// path id.</param>
        public sealed record Tree(Dictionary<(Bundle.Inner, long), (string Name, (int File, long Pid)[] Components)> GameObjects, Dictionary<(Bundle.Inner, long), (long Go, bool Root, long[] Children)> Transforms)
        {
            /// <summary>
            /// An empty tree.
            /// </summary>
            public Tree()
                : this([], [])
            {
            }

            /// <summary>
            /// A GameObject's or Transform's numbers, from its bytes. A GameObject has its components first
            /// (a count, then each a file id and a path id), then its layer and name; a Transform its
            /// GameObject, rotation, position and scale, then its children and its parent.
            /// </summary>
            public void Add(Bundle.Inner f, long pid, int type, Bytes d)
            {
                if (type == GameObjectType)
                {
                    var comps = new (int, long)[d.I32(0)];
                    for (int i = 0; i < comps.Length; i++)
                    {
                        comps[i] = (d.I32(4 + 12 * i), d.I64(8 + 12 * i));
                    }
                    int name = 8 + 12 * comps.Length;
                    GameObjects[(f, pid)] = (d.Text(name), comps);
                }
                else if (type == TransformType)
                {
                    long[] children = new long[d.I32(52)];
                    for (int i = 0; i < children.Length; i++)
                    {
                        children[i] = d.I64(60 + 12 * i);
                    }
                    int father = 56 + 12 * children.Length;
                    Transforms[(f, pid)] = (d.I64(4), d.I32(father) == 0 && d.I64(father + 4) == 0, children);
                }
            }
        }

        /// <summary>
        /// An object's bytes, with the file's byte order.
        /// </summary>
        /// <param name="data">The object's bytes.</param>
        /// <param name="bigEndian">Whether the file is big-endian.</param>
        public readonly ref struct Bytes(ReadOnlySpan<byte> data, bool bigEndian)
        {
            /// <summary>
            /// The bytes.
            /// </summary>
            private readonly ReadOnlySpan<byte> _data = data;

            /// <summary>
            /// A 32-bit integer at a byte offset.
            /// </summary>
            public int I32(int at) => bigEndian ? BinaryPrimitives.ReadInt32BigEndian(_data[at..]) : BinaryPrimitives.ReadInt32LittleEndian(_data[at..]);

            /// <summary>
            /// A 64-bit integer at a byte offset.
            /// </summary>
            public long I64(int at) => bigEndian ? BinaryPrimitives.ReadInt64BigEndian(_data[at..]) : BinaryPrimitives.ReadInt64LittleEndian(_data[at..]);

            /// <summary>
            /// A 32-bit float at a byte offset.
            /// </summary>
            public float F32(int at) => BitConverter.Int32BitsToSingle(I32(at));

            /// <summary>
            /// A byte at an offset.
            /// </summary>
            public byte U8(int at) => _data[at];

            /// <summary>
            /// A string: its length, then its UTF-8 bytes.
            /// </summary>
            public string Text(int at) => System.Text.Encoding.UTF8.GetString(_data.Slice(at + 4, I32(at)));
        }

        /// <summary>
        /// What <c>Sweep</c> gives each object read raw: its file, path id, Unity class id and bytes.
        /// </summary>
        public delegate void Parse(Bundle.Inner f, long pid, int type, Bytes data);

        /// <summary>
        /// These objects (each in its file) read raw in one sweep of the bundle, in the order of its files,
        /// each given whole to parse; with: others read whole (Field) in the same sweep.
        /// </summary>
        public static void Sweep(Bundle b, IEnumerable<(Bundle.Inner File, long Pid)> raw, Parse parse, IEnumerable<(Bundle.Inner File, long Pid)>? with = null)
        {
            var rawIn = raw.ToLookup(o => o.File, o => o.Pid);
            var wholeIn = (with ?? []).ToLookup(o => o.File, o => o.Pid);
            foreach (var f in b.Files)
            {
                var file = f.Assets.file;
                // Where each starts; read whole: -1 as its size.
                var objects = rawIn[f].Distinct().Select(p => (Info: file.GetAssetInfo(p), Whole: false)).Concat(wholeIn[f].Distinct().Select(p => (Info: file.GetAssetInfo(p), Whole: true)))
                    .Where(o => o.Info != null).Select(o => (o.Info!.PathId, At: o.Info.GetAbsoluteByteOffset(file), Size: o.Whole ? -1 : (int)o.Info.ByteSize, o.Info.TypeId)).ToArray();
                Array.Sort(objects, (x, y) => x.At.CompareTo(y.At));
                foreach (var (pid, at, size, type) in objects)
                {
                    if (size < 0)
                    {
                        b.Field(f, pid);
                        continue;
                    }
                    file.Reader.Position = at;
                    parse(f, pid, type, new Bytes(file.Reader.ReadBytes(size), file.Reader.BigEndian));
                }
            }
        }

        /// <summary>
        /// The tree as the type tree says, for one GameObject and its Transform: said, rather than read
        /// wrong (as Check above). Throws InvalidDataException when they differ.
        /// </summary>
        public static void Check(Bundle b, Bundle.Inner f, long go, Tree tree)
        {
            if (b.Field(f, go) is not { } g || !tree.GameObjects.TryGetValue((f, go), out var raw))
            {
                return;
            }
            var comps = raw.Components;
            bool same = g["m_Name"].AsString == raw.Name && g["m_Component.Array"].Children.Select(c => (c["component.m_FileID"].AsInt, c["component.m_PathID"].AsLong)).SequenceEqual(comps);
            if (comps.FirstOrDefault(c => Bundle.TypeOf(f, c.Pid) == TransformType) is (_, long tr and not 0) && b.Field(f, tr) is { } t && tree.Transforms.TryGetValue((f, tr), out var node))
            {
                same &= t["m_GameObject.m_PathID"].AsLong == node.Go && (t["m_Father.m_FileID"].AsInt == 0 && t["m_Father.m_PathID"].AsLong == 0) == node.Root
                    && t["m_Children.Array"].Children.Select(c => c["m_PathID"].AsLong).SequenceEqual(node.Children);
            }
            if (!same)
            {
                throw new InvalidDataException("The game's objects are not laid out as the editor expects (another Unity version?).");
            }
        }

        /// <summary>
        /// By file, then by place in it (those that are not there left out); with what they stand for.
        /// </summary>
        private static IEnumerable<(Bundle.Inner File, AssetFileInfo Info, long Of)> InOrder(IEnumerable<(Bundle.Inner File, AssetFileInfo? Info, long Of)> objects) =>
            objects.Where(o => o.Info != null).Select(o => (o.File, Info: o.Info!, o.Of))
                .OrderBy(o => o.File.Cab, StringComparer.Ordinal).ThenBy(o => o.Info.GetAbsoluteByteOffset(o.File.Assets.file));

        /// <summary>
        /// The file's reader, at the start of the object's bytes.
        /// </summary>
        private static AssetsFileReader At(Bundle.Inner f, AssetFileInfo info)
        {
            var file = f.Assets.file;
            file.Reader.Position = info.GetAbsoluteByteOffset(file);
            return file.Reader;
        }
    }

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
            AssetTypeValueField? tr = null;
            var colliders = new List<(int Type, AssetTypeValueField F)>();
            foreach (var (t, pid) in b.ComponentIds(g))
            {
                if (t == TransformType && b.Field(pid) is { } f)
                {
                    tr = f;
                }
                else if (IsCollider(t) && b.Field(pid) is { } c && c.Children.Count > 0 && Solid(c))
                {
                    colliders.Add((t, c));
                }
            }
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

    /// <summary>
    /// What the game's support check needs of each piece, read at the editor's start (GameData), for the
    /// editor's stability check: its material, centre of mass offset, whether it supports other pieces,
    /// whether the game checks its support at all ("f": never), and its colliders as boxes in its own
    /// frame: [centre, half size, rotation, aabb, layer] (aabb 1: the bounds of a collider that is not a
    /// box, a mesh collider's as its mesh's box). Colliders of every child count, inactive ones too;
    /// triggers and snap points do not. The pieces with a WearNTear (told by its class) on their root.
    /// </summary>
    private sealed class SupportScan : IStartScan
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
                Walk(b, pid, default, Quat.Root, (1, 1, 1), true, null, meshes);
            }
            b.ReadInOrder(meshes, (pid, m) => _meshes[pid] = (Vec(m["m_LocalAABB.m_Center"]), Vec(m["m_LocalAABB.m_Extent"])));
            foreach (var (pid, go, wntPid) in pieces)
            {
                var wnt = b.Field(wntPid)!;
                var boxes = new List<object?>();
                Walk(b, pid, default, Quat.Root, (1, 1, 1), true, boxes, null);
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

        /// <summary>
        /// A piece's colliders as boxes; or (meshes) the meshes its mesh colliders need, to read first.
        /// </summary>
        private void Walk(Bundle b, long goPid, V3 pos, Quat rot, V3 scale, bool root, List<object?>? boxes, List<long>? meshes)
        {
            if (b.Field(goPid) is not { } g || IsSnapPoint(g))
            {
                return;
            }
            object? layer = Get(g, "m_Layer", 0L);
            // Only the Transform and colliders read (not the renderers, scripts...).
            AssetTypeValueField? tr = null;
            var cols = new List<(int Type, AssetTypeValueField F)>();
            foreach (var (t, pid) in b.ComponentIds(g))
            {
                if (t == TransformType && b.Field(pid) is { } f)
                {
                    tr = f;
                }
                else if (IsCollider(t) && b.Field(pid) is { } c && c.Children.Count > 0 && Solid(c))
                {
                    cols.Add((t, c));
                }
            }
            if (tr == null)
            {
                return;
            }
            var (p, q, s) = root ? (default(V3), Quat.Root, (V3)(1.0, 1.0, 1.0)) : ChildFrame(tr, pos, rot, scale);
            foreach (var (t, cm) in cols)
            {
                V3 c, h;
                long aabb = 1;
                if (t == BoxColliderType)
                {
                    c = Vec(cm["m_Center"]);
                    var (x, y, z) = Vec(cm["m_Size"]);
                    h = (x / 2, y / 2, z / 2);
                    aabb = 0;
                }
                else if (t == MeshColliderType)
                {
                    var mesh = cm["m_Mesh"];
                    if (mesh.IsDummy || mesh["m_FileID"].AsInt != 0)
                    {
                        continue;
                    }
                    meshes?.Add(mesh["m_PathID"].AsLong);
                    if (!_meshes.TryGetValue(mesh["m_PathID"].AsLong, out var bounds))
                    {
                        continue;
                    }
                    (c, h) = bounds;
                }
                else if (t == SphereColliderType)
                {
                    c = Vec(cm["m_Center"]);
                    double rr = cm["m_Radius"].AsFloat;
                    h = (rr, rr, rr);
                }
                else
                {
                    c = Vec(cm["m_Center"]);
                    double rr = cm["m_Radius"].AsFloat;
                    double hh = Max(cm["m_Height"].AsFloat / 2.0, rr);
                    long d = Int(Get(cm, "m_Direction", 1L));
                    h = (d == 0 ? hh : rr, d == 1 ? hh : rr, d == 2 ? hh : rr);
                }
                var (cx, cy, cz) = q.Rotate(Scale(c, s));
                boxes?.Add(new List<object?> { R(cx + p.X, 4), R(cy + p.Y, 4), R(cz + p.Z, 4), R(Math.Abs(h.X * s.X), 4), R(Math.Abs(h.Y * s.Y), 4), R(Math.Abs(h.Z * s.Z), 4) }.Concat(q.Rounded(4)).Append(aabb).Append(layer).ToList());
            }
            foreach (long child in ChildObjects(b, tr))
            {
                Walk(b, child, p, q, s, false, boxes, meshes);
            }
        }
    }

    /// <summary>
    /// The game's English texts (its localization tables, in resources.assets): token -&gt; text.
    /// </summary>
    private static class Localization
    {
        /// <summary>
        /// Unity's class id of a TextAsset.
        /// </summary>
        private const int TextAssetType = (int)AssetClassID.TextAsset;

        /// <summary>
        /// The English texts of the localisation tables in resources.assets (three folders up from the bundles); the
        /// first text of a token wins.
        /// </summary>
        public static Dictionary<string, string> English(string bundlesDir)
        {
            string data = Path.GetFullPath(Path.Combine(bundlesDir, "..", "..", ".."));
            var am = new AssetsManager();
            try
            {
                var a = am.LoadAssetsFile(Path.Combine(data, "resources.assets"), false);
                var english = new Dictionary<string, string>();
                foreach (var info in a.file.AssetInfos.Where(i => i.TypeId == TextAssetType))
                {
                    var (name, script) = TextAsset(am, a, info);
                    if (!name.StartsWith("localization", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    foreach (var row in Csv(script.TrimStart('﻿')))
                    {
                        if (row.Count > 1 && row[0].Length > 0 && row[1].Length > 0)
                        {
                            english.TryAdd(Strip(row[0]), Strip(row[1]));
                        }
                    }
                }
                return english;
            }
            finally
            {
                am.UnloadAll();
            }
        }

        /// <summary>
        /// A TextAsset's name and text: by its fields when the file describes them, else read as laid out
        /// (a name, then the text, each a length and UTF-8 bytes, 4-byte aligned).
        /// </summary>
        private static (string Name, string Script) TextAsset(AssetsManager am, AssetsFileInstance a, AssetFileInfo info)
        {
            if (a.file.Metadata.TypeTreeEnabled)
            {
                var f = am.GetBaseField(a, info);
                return (f["m_Name"].AsString, f["m_Script"].AsString);
            }
            var r = a.file.Reader;
            long start = info.GetAbsoluteByteOffset(a.file);
            r.Position = start;
            string Next()
            {
                int n = r.ReadInt32();
                string s = System.Text.Encoding.UTF8.GetString(r.ReadBytes(n));
                r.Position = start + ((r.Position - start + 3) & ~3L);
                return s;
            }
            string name = Next();
            return (name, Next());
        }

        /// <summary>
        /// Whitespace off both ends, as Python's str.strip (which also takes the separators \x1c-\x1f).
        /// </summary>
        public static string Strip(string s)
        {
            static bool Space(char c) => char.IsWhiteSpace(c) || c is >= '\x1c' and <= '\x1f';
            int a = 0, b = s.Length;
            while (a < b && Space(s[a]))
            {
                a++;
            }
            while (b > a && Space(s[b - 1]))
            {
                b--;
            }
            return s[a..b];
        }

        /// <summary>
        /// Rows of comma-separated text as Python's csv module reads them (quoted fields may hold commas,
        /// line breaks and doubled quotes; a quote inside an unquoted field is kept as it is). A lone carriage return
        /// followed by more text on the line ends the row, as a line break does (Python's csv refuses it, and one odd
        /// line of a localisation table then stopped every world opening).
        /// </summary>
        public static IEnumerable<List<string>> Csv(string text)
        {
            var row = new List<string>();
            var field = new System.Text.StringBuilder();
            int state = 0; // 0 start of a field, 1 in a field, 2 in quotes, 3 a quote in quotes, 4 after a line break
            bool rowStarted = false;
            foreach (string line in Lines(text))
            {
                for (int k = 0; k < line.Length; k++)
                {
                    char c = line[k];
                    if (state == 4 && c is not '\n' and not '\r')
                    {
                        // Text after a lone carriage return: the next row, this character its first.
                        yield return row;
                        row = [];
                        state = 0;
                        rowStarted = false;
                    }
                    switch (state)
                    {
                        case 0 when !rowStarted && (c == '\n' || c == '\r'):
                            state = 4;
                            break;
                        case 0:
                            rowStarted = true;
                            if (c is '\n' or '\r')
                            {
                                row.Add("");
                                state = 4;
                            }
                            else if (c == '"')
                            {
                                state = 2;
                            }
                            else if (c == ',')
                            {
                                row.Add("");
                            }
                            else
                            {
                                field.Append(c);
                                state = 1;
                            }
                            break;
                        case 1:
                            if (c is '\n' or '\r')
                            {
                                row.Add(field.ToString());
                                field.Clear();
                                state = 4;
                            }
                            else if (c == ',')
                            {
                                row.Add(field.ToString());
                                field.Clear();
                                state = 0;
                            }
                            else
                            {
                                field.Append(c);
                            }
                            break;
                        case 2:
                            if (c == '"')
                            {
                                state = 3;
                            }
                            else
                            {
                                field.Append(c);
                            }
                            break;
                        case 3:
                            if (c == '"')
                            {
                                field.Append('"');
                                state = 2;
                            }
                            else if (c == ',')
                            {
                                row.Add(field.ToString());
                                field.Clear();
                                state = 0;
                            }
                            else if (c is '\n' or '\r')
                            {
                                row.Add(field.ToString());
                                field.Clear();
                                state = 4;
                            }
                            else
                            {
                                field.Append(c);
                                state = 1;
                            }
                            break;
                        case 4:
                            break;
                        default:
                            break;
                    }
                }
                // The end of a line: the row ends unless a quoted field goes on.
                switch (state)
                {
                    case 1 or 3:
                        row.Add(field.ToString());
                        field.Clear();
                        break;
                    case 0 when rowStarted:
                        row.Add("");
                        break;
                    default:
                        break;
                }
                if (state != 2)
                {
                    yield return row;
                    row = [];
                    state = 0;
                    rowStarted = false;
                }
            }
            // Quotes not closed at the end: the row as it is.
            if (state == 2)
            {
                row.Add(field.ToString());
                yield return row;
            }
        }

        /// <summary>
        /// Lines with their line breaks, split at \n only (as Python's io.StringIO does).
        /// </summary>
        private static IEnumerable<string> Lines(string text)
        {
            int start = 0;
            while (start < text.Length)
            {
                int nl = text.IndexOf('\n', start);
                int end = nl < 0 ? text.Length : nl + 1;
                yield return text[start..end];
                start = end;
            }
        }
    }
}
