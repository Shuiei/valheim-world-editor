using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Dict = TerrainEditor.App.CatalogueJson.Dict;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

// The build pieces' catalogues: every prefab whose root has a Piece component; and what the editor
// reads of them at its start (GameData): their costs.
public static partial class GameCatalogues
{
	private const int BoxColliderType = (int)AssetClassID.BoxCollider;
	private const int MeshColliderType = (int)AssetClassID.MeshCollider;
	private const int SphereColliderType = (int)AssetClassID.SphereCollider;
	private const int CapsuleColliderType = (int)AssetClassID.CapsuleCollider;
	private const int MeshType = (int)AssetClassID.Mesh;

	private static bool IsCollider(int type) => type is BoxColliderType or MeshColliderType or SphereColliderType or CapsuleColliderType;

	// A collider that counts: enabled, not a trigger.
	private static bool Solid(AssetTypeValueField c) => Num(Get(c, "m_Enabled", 1L)) == 1 && !Truthy(Get(c, "m_IsTrigger", 0L));

	private static bool IsSnapPoint(AssetTypeValueField go) => Get(go, "m_Tag", null) is long tag && tag == SnapTag || go["m_Name"].AsString.ToLowerInvariant().Contains("snappoint", StringComparison.Ordinal);

	private static bool Active(AssetTypeValueField go) => Num(Get(go, "m_IsActive", 1L)) == 1;

	// Each prefab with a Piece on its root: its fields and the root's components.
	private static IEnumerable<(string Name, long Pid, AssetTypeValueField Go, List<(int Type, long Pid, AssetTypeValueField F)> Comps, AssetTypeValueField Piece)> Pieces(Bundle b)
	{
		foreach (var (f, info) in b.Objects)
		{
			if (info.TypeId != GameObjectType || b.Field(f, info.PathId) is not { } go || RootComponents(b, go) is not { } comps)
			{
				continue;
			}
			var piece = comps.FirstOrDefault(c => c.Type == MonoBehaviourType && IsPiece(c.F)).F;
			if (piece != null)
			{
				yield return (go["m_Name"].AsString, info.PathId, go, comps, piece);
			}
		}
	}

	// A child's frame from its parent's: (position, rotation, scale), all in the prefab's frame.
	private static (V3 P, Quat Q, V3 S) ChildFrame(AssetTypeValueField tr, V3 pos, Quat rot, V3 scale)
	{
		var p = Add(rot.Rotate(Scale(Vec(tr["m_LocalPosition"]), scale)), pos);
		return (p, rot.Mul(Quat.Of(tr["m_LocalRotation"])), Scale(scale, Vec(tr["m_LocalScale"])));
	}

	// A prefab root's position as the files have it: whole numbers.
	private static List<object?> Origin() => new() { 0L, 0L, 0L };

	// The first smallest / largest (as Python's min and max).
	private static double Min(IEnumerable<double> xs) => xs.Aggregate((a, x) => x < a ? x : a);

	private static double Max(IEnumerable<double> xs) => xs.Aggregate((a, x) => x > a ? x : a);

	// pieces.json: each piece's category, comfort, snap points (children tagged "snappoint", inactive
	// ones too) and footprint (the box around its active box colliders), in its own frame. "tool" is
	// added by ToolScan.
	private sealed class PieceScan : IScan
	{
		public Dict Entries { get; } = new();

		public void Read(Bundle b)
		{
			foreach (var (name, pid, _, _, piece) in Pieces(b))
			{
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
				Entries[name] = entry;
			}
		}

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
			AssetTypeValueField? tr = null;
			var boxes = new List<AssetTypeValueField>();
			foreach (var (t, _, f) in b.Components(g))
			{
				if (t == TransformType)
				{
					tr = f;
				}
				else if (t == BoxColliderType && Solid(f))
				{
					boxes.Add(f);
				}
			}
			if (tr == null)
			{
				return;
			}
			var (p, r, s) = root ? (default(V3), Quat.Root, (V3)(1.0, 1.0, 1.0)) : ChildFrame(tr, pos, rot, scale);
			if (snap)
			{
				snaps.Add(root ? Origin() : new List<object?> { R(p.X, 3), R(p.Y, 3), R(p.Z, 3) });
				return;
			}
			foreach (var box in boxes)
			{
				var c = Vec(box["m_Center"]);
				var z = Vec(box["m_Size"]);
				foreach (double dx in new[] { -0.5, 0.5 })
				{
					foreach (double dy in new[] { -0.5, 0.5 })
					{
						foreach (double dz in new[] { -0.5, 0.5 })
						{
							var v = r.Rotate((c.X + dx * z.X) * s.X, (c.Y + dy * z.Y) * s.Y, (c.Z + dz * z.Z) * s.Z);
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

	// pieces.json's "tool": which build tool's menu has each piece (the game's PieceTables). The
	// editor writes a builder on new pieces of these kinds, as the game does when a player places one.
	// Entries that are not prefabs (repair, remove, the hoe's ground tools) are left out.
	private sealed class ToolScan : IScan
	{
		private static readonly Dictionary<string, string> Tables = new() { ["_HammerPieceTable"] = "hammer", ["_HoePieceTable"] = "hoe", ["_CultivatorPieceTable"] = "cultivator", ["_FeasterPieceTable"] = "feaster" };

		private readonly Dictionary<string, List<Ref>> _tables = new();

		public void Read(Bundle b)
		{
			foreach (var f in b.Files)
			{
				foreach (var info in f.Assets.file.AssetInfos)
				{
					if (info.TypeId != MonoBehaviourType || b.Field(f, info.PathId) is not { } d || !Has(d, "m_pieces") || !Has(d, "m_canRemovePieces"))
					{
						continue;
					}
					if (b.Field(f, d["m_GameObject.m_PathID"].AsLong) is not { } owner || !Tables.TryGetValue(owner["m_Name"].AsString, out string? tool))
					{
						continue;
					}
					_tables[tool] = d["m_pieces.Array"].Children.Select(p => new Ref(f.FileOf(p["m_FileID"].AsInt) ?? "", p["m_PathID"].AsLong)).ToList();
				}
			}
		}

		public void Finish(Game game, Dict pieces, IReadOnlySet<string> prefabs)
		{
			var names = new Dictionary<Ref, string>();
			game.Visit(_tables.Values.SelectMany(t => t), (b, f, pid) =>
			{
				if (b.Field(f, pid) is { } go && Has(go, "m_Name"))
				{
					names[new Ref(f.Cab, pid)] = go["m_Name"].AsString;
				}
			});
			// A piece in several menus: the hammer's.
			foreach (string tool in new[] { "feaster", "cultivator", "hoe", "hammer" })
			{
				foreach (var r in _tables.GetValueOrDefault(tool) ?? new())
				{
					if (names.TryGetValue(r, out string? n) && pieces.TryGetValue(n, out object? entry) && prefabs.Contains(n))
					{
						((Dict)entry!)["tool"] = tool;
					}
				}
			}
		}
	}

	// Each piece's English name, the crafting station it needs nearby and its resources (item and amount,
	// and whether it is given back), items by prefab name with their English names, for the blueprint
	// library's material cost.
	// What build pieces cost, read at the editor's start (GameData): the prefabs are found from the Piece
	// scripts (told by their class, nothing else read), not by reading every GameObject.
	private sealed class CostScan : IStartScan
	{
		private readonly Dictionary<string, string> _english;
		private readonly Dict _pieces = new(), _items = new();

		public CostScan(Dictionary<string, string> english) => _english = english;

		public string Json() => CatalogueJson.Write(new Dict { ["pieces"] = _pieces, ["items"] = _items }, new CatalogueJson.Style(",", ":", SortKeys: true, Ascii: false));

		private string En(string? token)
		{
			string t = Localization.Strip(token ?? "");
			return t.StartsWith('$') ? _english.GetValueOrDefault(t[1..], t) : t;
		}

		public void Read(Bundle b, Game game)
		{
			// The Piece scripts (told by their class) and their GameObjects, kept to the prefab roots (the
			// locations have many pieces below theirs), in the bundle's order; only these are read whole.
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
				return;
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
					Add(b, go["m_Name"].AsString, piece);
				}
			}
		}

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

		// The GameObject a component reference (same file only) is on: its name, and the component.
		private static (string? Name, AssetTypeValueField? Component) Owner(Bundle b, AssetTypeValueField pptr)
		{
			if (pptr["m_FileID"].AsInt != 0 || b.Field(pptr["m_PathID"].AsLong) is not { } c)
			{
				return (null, null);
			}
			return (b.Field(c["m_GameObject.m_PathID"].AsLong)?["m_Name"].AsString, c);
		}
	}

	// Objects read as laid out, a few numbers each rather than their whole fields: for the readers at
	// the editor's start. The bundle is read in blocks, so they are read in the order of their file.
	private static class Layout
	{
		// An object's first reference (a component's m_GameObject): its file id and path id.
		public static (int FileId, long PathId) First(Bundle.Inner f, AssetFileInfo info)
		{
			var r = At(f, info);
			return (r.ReadInt32(), r.ReadInt64());
		}

		// The prefab roots among these GameObjects (each in its file): those whose Transform has no
		// parent. A GameObject has its components first (a count, then each a file id and a path id); a
		// Transform has its parent after its GameObject, rotation, position, scale and children.
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

		// The layout read as the type tree says, for one GameObject: whether it is a root agrees with
		// Roots. A game made with another Unity could lay objects out otherwise: said, rather than read
		// wrong.
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

		// By file, then by place in it (those that are not there left out); with what they stand for.
		private static IEnumerable<(Bundle.Inner File, AssetFileInfo Info, long Of)> InOrder(IEnumerable<(Bundle.Inner File, AssetFileInfo? Info, long Of)> objects) =>
			objects.Where(o => o.Info != null).Select(o => (o.File, Info: o.Info!, o.Of))
				.OrderBy(o => o.File.Cab, StringComparer.Ordinal).ThenBy(o => o.Info.GetAbsoluteByteOffset(o.File.Assets.file));

		private static AssetsFileReader At(Bundle.Inner f, AssetFileInfo info)
		{
			var file = f.Assets.file;
			file.Reader.Position = info.GetAbsoluteByteOffset(file);
			return file.Reader;
		}
	}

	// The build ray's layers (Default, static_solid, Default_small, piece, piece_nonsolid, terrain, vehicle).
	private static readonly HashSet<long> RayLayers = new() { 0, 15, 20, 10, 16, 11, 28 };

	// piece-place.json.gz: how the game's hammer places each piece, for the Workshop's Build: its
	// placement flags, its snap points (its direct children tagged "snappoint") and the colliders the
	// build ray and the ghost use (enabled, not triggers, on an active object, on a layer of the build
	// ray), in the piece's own frame (its root at the origin, its root scale applied). Colliders:
	// ["b", centre, half size, rotation] a box; ["s", centre, r] a sphere; ["c", a, b, r] a capsule (its
	// segment); ["m", convex, [x,y,z...], [i,j,k...]] a mesh (triangles).
	private sealed class PlaceScan : IScan
	{
		private static readonly (string Key, string Field)[] Flags = { ("g", "m_groundPiece"), ("cg", "m_clipGround"), ("ce", "m_clipEverything"), ("ro", "m_allowRotatedOverlap"), ("w", "m_waterPiece"), ("nw", "m_noInWater") };

		public Dict Entries { get; } = new();

		// The bundle's meshes read so far.
		private readonly Dictionary<long, (float[] Vertices, List<int[]> Triangles)?> _meshes = new();

		public void Read(Bundle b)
		{
			_meshes.Clear();
			foreach (var (name, pid, _, comps, piece) in Pieces(b))
			{
				var root = comps.First(c => c.Type == TransformType).F;
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
						var lp = Vec(ct["m_LocalPosition"]);
						snaps.Add(new List<object?> { R(lp.X * rs.X, 4), R(lp.Y * rs.Y, 4), R(lp.Z * rs.Z, 4) });
					}
				}
				var cols = new List<object?>();
				Walk(b, pid, default, Quat.Root, rs, true, rs, cols);
				var flags = new Dict();
				foreach (var (key, field) in Flags)
				{
					if (Truthy(Get(piece, field, 0L)))
					{
						flags[key] = 1L;
					}
				}
				Entries[name] = new Dict { ["f"] = flags, ["s"] = snaps, ["c"] = cols };
			}
		}

		private void Walk(Bundle b, long goPid, V3 pos, Quat rot, V3 scale, bool root, V3 rootScale, List<object?> cols)
		{
			if (b.Field(goPid) is not { } g || !Truthy(Get(g, "m_IsActive", 1L)))
			{
				return;
			}
			AssetTypeValueField? tr = null;
			var colliders = new List<(int Type, AssetTypeValueField F)>();
			foreach (var (t, _, f) in b.Components(g))
			{
				if (t == TransformType)
				{
					tr = f;
				}
				else if (IsCollider(t) && f.Children.Count > 0 && Solid(f))
				{
					colliders.Add((t, f));
				}
			}
			if (tr == null)
			{
				return;
			}
			var (pp, q, s) = root ? (default(V3), Quat.Root, rootScale) : ChildFrame(tr, pos, rot, scale);
			if (RayLayers.Contains(Int(Get(g, "m_Layer", 0L))))
			{
				V3 P(V3 v) => Add(q.Rotate(Scale(v, s)), pp);
				foreach (var (t, c) in colliders)
				{
					if (t == BoxColliderType)
					{
						var cc = P(Vec(c["m_Center"]));
						var z = Vec(c["m_Size"]);
						cols.Add(new List<object?> { "b", R(cc.X, 4), R(cc.Y, 4), R(cc.Z, 4), R(Math.Abs(z.X * s.X) / 2, 4), R(Math.Abs(z.Y * s.Y) / 2, 4), R(Math.Abs(z.Z * s.Z) / 2, 4) }.Concat(q.Rounded(4)).ToList());
					}
					else if (t == SphereColliderType)
					{
						var cc = P(Vec(c["m_Center"]));
						cols.Add(new List<object?> { "s", R(cc.X, 4), R(cc.Y, 4), R(cc.Z, 4), R(c["m_Radius"].AsFloat * Max(new[] { Math.Abs(s.X), Math.Abs(s.Y), Math.Abs(s.Z) }), 4) });
					}
					else if (t == CapsuleColliderType)
					{
						var cc = Vec(c["m_Center"]);
						int d = (int)Int(Get(c, "m_Direction", 1L));
						double[] sa = { s.X, s.Y, s.Z };
						double rr = c["m_Radius"].AsFloat * Max(Enumerable.Range(0, 3).Where(i => i != d).Select(i => Math.Abs(sa[i])));
						double half = Max(c["m_Height"].AsFloat * Math.Abs(sa[d]) / 2 - rr, 0);
						var ax = new double[3];
						ax[d] = sa[d] != 0 ? half / Math.Abs(sa[d]) : 0;
						var a = P((cc.X + ax[0], cc.Y + ax[1], cc.Z + ax[2]));
						var e = P((cc.X - ax[0], cc.Y - ax[1], cc.Z - ax[2]));
						cols.Add(new List<object?> { "c", R(a.X, 4), R(a.Y, 4), R(a.Z, 4), R(e.X, 4), R(e.Y, 4), R(e.Z, 4), R(rr, 4) });
					}
					else if (Mesh(b, c["m_Mesh"]) is var (vertices, triangles))
					{
						var coords = new List<object?>(vertices.Length);
						for (int i = 0; i + 2 < vertices.Length; i += 3)
						{
							var w = P((vertices[i], vertices[i + 1], vertices[i + 2]));
							coords.Add(R(w.X, 4));
							coords.Add(R(w.Y, 4));
							coords.Add(R(w.Z, 4));
						}
						cols.Add(new List<object?> { "m", Truthy(Get(c, "m_Convex", 0L)) ? 1L : 0L, coords, triangles.SelectMany(x => x).Select(i => (object?)(long)i).ToList() });
					}
				}
			}
			foreach (long child in ChildObjects(b, tr))
			{
				Walk(b, child, pp, q, s, false, rootScale, cols);
			}
		}

		// A mesh collider's mesh (in the same file), or null.
		private (float[] Vertices, List<int[]> Triangles)? Mesh(Bundle b, AssetTypeValueField pptr)
		{
			if (pptr.IsDummy || pptr["m_FileID"].AsInt != 0)
			{
				return null;
			}
			long pid = pptr["m_PathID"].AsLong;
			if (!_meshes.TryGetValue(pid, out var mesh))
			{
				mesh = b.TypeOf(pid) == MeshType && b.Field(pid) is { } m ? MeshData(b, m) : null;
				_meshes[pid] = mesh;
			}
			return mesh;
		}
	}

	// piece-support.json: what the game's support check needs of every piece, for the editor's
	// stability check: its material, centre of mass offset, whether it supports other pieces, whether
	// the game checks its support at all ("f": never), and its colliders as boxes in its own frame:
	// [centre, half size, rotation, aabb, layer] (aabb 1: the bounds of a collider that is not a box, a
	// mesh collider's as its mesh's box). Colliders of every child count, inactive ones too; triggers
	// and snap points do not.
	private sealed class SupportScan : IScan
	{
		public Dict Entries { get; } = new();

		public void Read(Bundle b)
		{
			foreach (var (f, info) in b.Objects)
			{
				if (info.TypeId != GameObjectType || b.Field(f, info.PathId) is not { } go || RootComponents(b, go) is not { } comps)
				{
					continue;
				}
				if (!comps.Any(c => c.Type == MonoBehaviourType && Has(c.F, "m_category") && Has(c.F, "m_resources")))
				{
					continue;
				}
				var wnt = comps.FirstOrDefault(c => c.Type == MonoBehaviourType && Has(c.F, "m_materialType") && Has(c.F, "m_supports")).F;
				if (wnt == null)
				{
					continue;
				}
				var boxes = new List<object?>();
				Walk(b, info.PathId, default, Quat.Root, (1, 1, 1), true, boxes);
				var com = Has(wnt, "m_comOffset") ? Vec(wnt["m_comOffset"]) : default;
				var entry = new Dict
				{
					["m"] = Py(wnt["m_materialType"]),
					["s"] = Int(Get(wnt, "m_supports", 1L)),
					["c"] = Has(wnt, "m_comOffset") ? new List<object?> { R(com.X, 4), R(com.Y, 4), R(com.Z, 4) } : Origin(),
					["b"] = boxes,
				};
				// The game never checks its support (m_noSupportWear off): it never falls, and keeps its
				// full support for what rests on it (black marble's large floor, some dungeon pieces).
				if (!Truthy(Get(wnt, "m_noSupportWear", 1L)))
				{
					entry["f"] = 1L;
				}
				Entries[go["m_Name"].AsString] = entry;
			}
		}

		private static void Walk(Bundle b, long goPid, V3 pos, Quat rot, V3 scale, bool root, List<object?> boxes)
		{
			if (b.Field(goPid) is not { } g || IsSnapPoint(g))
			{
				return;
			}
			object? layer = Get(g, "m_Layer", 0L);
			AssetTypeValueField? tr = null;
			var cols = new List<(int Type, AssetTypeValueField F)>();
			foreach (var (t, _, f) in b.Components(g))
			{
				if (t == TransformType)
				{
					tr = f;
				}
				else if (IsCollider(t) && f.Children.Count > 0 && Solid(f))
				{
					cols.Add((t, f));
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
					var z = Vec(cm["m_Size"]);
					h = (z.X / 2, z.Y / 2, z.Z / 2);
					aabb = 0;
				}
				else if (t == MeshColliderType)
				{
					var mesh = cm["m_Mesh"];
					var mf = !mesh.IsDummy && mesh["m_FileID"].AsInt == 0 ? b.Field(mesh["m_PathID"].AsLong) : null;
					if (mf == null)
					{
						continue;
					}
					c = Vec(mf["m_LocalAABB.m_Center"]);
					h = Vec(mf["m_LocalAABB.m_Extent"]);
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
				var cs = q.Rotate(Scale(c, s));
				boxes.Add(new List<object?> { R(cs.X + p.X, 4), R(cs.Y + p.Y, 4), R(cs.Z + p.Z, 4), R(Math.Abs(h.X * s.X), 4), R(Math.Abs(h.Y * s.Y), 4), R(Math.Abs(h.Z * s.Z), 4) }.Concat(q.Rounded(4)).Append(aabb).Append(layer).ToList());
			}
			foreach (long child in ChildObjects(b, tr))
			{
				Walk(b, child, p, q, s, false, boxes);
			}
		}
	}

	// The game's English texts (its localization tables, in resources.assets): token -> text.
	private static class Localization
	{
		private const int TextAssetType = (int)AssetClassID.TextAsset;

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

		// A TextAsset's name and text: by its fields when the file describes them, else read as laid out
		// (a name, then the text, each a length and UTF-8 bytes, 4-byte aligned).
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

		// Whitespace off both ends, as Python's str.strip (which also takes the separators \x1c-\x1f).
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

		// Rows of comma-separated text as Python's csv module reads them (quoted fields may hold commas,
		// line breaks and doubled quotes; a quote inside an unquoted field is kept as it is).
		public static IEnumerable<List<string>> Csv(string text)
		{
			var row = new List<string>();
			var field = new System.Text.StringBuilder();
			int state = 0; // 0 start of a field, 1 in a field, 2 in quotes, 3 a quote in quotes, 4 after a line break
			bool rowStarted = false;
			foreach (string line in Lines(text))
			{
				foreach (char c in line)
				{
					switch (state)
					{
						case 0 when !rowStarted && (c == '\n' || c == '\r'):
							state = 4;
							break;
						case 0:
							rowStarted = true;
							if (c == '\n' || c == '\r')
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
							if (c == '\n' || c == '\r')
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
							else if (c == '\n' || c == '\r')
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
							if (c != '\n' && c != '\r')
							{
								throw new InvalidDataException("new-line character seen in unquoted field");
							}
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
				}
				if (state != 2)
				{
					yield return row;
					row = new List<string>();
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

		// Lines with their line breaks, split at \n only (as Python's io.StringIO does).
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
