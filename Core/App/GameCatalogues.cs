using System.IO.Compression;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Style = TerrainEditor.App.CatalogueJson.Style;

namespace TerrainEditor.App;

// The catalogues in Core/WorldGen (in git, embedded in the app) made again from the game's files, for
// developers after a game update: --export-catalogues <folder> [name ...]. Each catalogue is what the
// editor needs of one kind of game component (pieces, prefabs, vegetation, dungeons...), read from
// every asset bundle. The bundles are read once, in the order of their file names, each in the order
// it lists its files and objects: the order entries come in, and which one wins when a name is found
// twice. What needs names from other bundles (what saplings grow into, the build tools' pieces, the
// vegetation's prefabs) reads them after. The files are written as they always were (CatalogueJson),
// so making them again shows in git only what the game changed.
public static partial class GameCatalogues
{
	public static readonly string[] All = { "prefabs", "pieces", "piece-cost", "piece-place", "piece-support", "vegetation", "dungeon-rooms" };

	// Written gzipped (the largest).
	private static readonly string[] Gzipped = { "piece-place", "dungeon-rooms" };

	// --export-catalogues <folder> [name ...]: all catalogues, or those named (with or without .json).
	public static int Export(string[] args)
	{
		if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
		{
			Console.Error.WriteLine($"--export-catalogues needs the folder to write to, then optionally the catalogues to make ({string.Join(", ", All)}).");
			return 2;
		}
		var names = args.Skip(1).Select(n => n.Replace(".gz", "", StringComparison.Ordinal).Replace(".json", "", StringComparison.Ordinal)).ToHashSet();
		if (names.FirstOrDefault(n => Array.IndexOf(All, n) < 0) is string unknown)
		{
			Console.Error.WriteLine($"No catalogue \"{unknown}\" (there are {string.Join(", ", All)}).");
			return 2;
		}
		if (names.Count == 0)
		{
			names = All.ToHashSet();
		}
		string? valheim = GameLook.FindValheim(AppSettings.Load().ValheimPath);
		var bundles = valheim == null ? null : GameBundles.ForGame(valheim, null);
		if (valheim == null || bundles == null)
		{
			Console.Error.WriteLine("Valheim was not found.");
			return 1;
		}
		var watch = System.Diagnostics.Stopwatch.StartNew();
		var made = Make(valheim, bundles, names);
		Directory.CreateDirectory(args[0]);
		foreach (string name in All.Where(made.ContainsKey))
		{
			byte[] json = new UTF8Encoding(false).GetBytes(made[name]);
			string path = Path.Combine(args[0], name + (Gzipped.Contains(name) ? ".json.gz" : ".json"));
			if (Gzipped.Contains(name))
			{
				using var gz = new GZipStream(File.Create(path), CompressionLevel.SmallestSize);
				gz.Write(json);
			}
			else
			{
				File.WriteAllBytes(path, json);
			}
			Console.WriteLine($"{path}: {json.Length} bytes");
		}
		Console.WriteLine($"{made.Count} catalogues made in {watch.Elapsed.TotalSeconds:0} s");
		return 0;
	}

	// Each catalogue asked for, as its JSON.
	private static Dictionary<string, string> Make(string valheim, GameBundles bundles, HashSet<string> names)
	{
		var made = new Dictionary<string, string>();
		// The build tools' pieces are kept to those that are prefabs: pieces.json needs prefabs too.
		var prefabs = names.Contains("prefabs") || names.Contains("pieces") ? new PrefabScan() : null;
		var pieces = names.Contains("pieces") ? new PieceScan() : null;
		var tools = names.Contains("pieces") ? new ToolScan() : null;
		var cost = names.Contains("piece-cost") ? new CostScan(Localization.English(bundles.Folder)) : null;
		var place = names.Contains("piece-place") ? new PlaceScan() : null;
		var support = names.Contains("piece-support") ? new SupportScan() : null;
		var vegetation = names.Contains("vegetation") ? new VegetationScan() : null;
		var dungeons = names.Contains("dungeon-rooms") ? new DungeonScan() : null;
		var scans = new IScan?[] { prefabs, pieces, tools, cost, place, support, vegetation, dungeons }.OfType<IScan>().ToList();
		if (scans.Count > 0)
		{
			using var game = new Game(bundles.Folder);
			for (int i = 0; i < game.Bundles.Count; i++)
			{
				using (var b = game.Open(game.Bundles[i]))
				{
					if (b != null)
					{
						foreach (var scan in scans)
						{
							scan.Read(b);
						}
					}
				}
				if (i % 100 == 0)
				{
					Console.WriteLine($"{i} of {game.Bundles.Count} bundles read");
				}
			}
			if (prefabs != null && names.Contains("prefabs"))
			{
				prefabs.Finish(game);
				made["prefabs"] = CatalogueJson.Write(prefabs.Json(), new Style(",", ":", SortKeys: true));
			}
			if (pieces != null)
			{
				tools!.Finish(game, pieces.Entries, prefabs!.Names);
				made["pieces"] = CatalogueJson.Write(pieces.Entries, new Style());
			}
			if (cost != null)
			{
				made["piece-cost"] = CatalogueJson.Write(cost.Json(), new Style(",", ":", SortKeys: true, Ascii: false));
			}
			if (place != null)
			{
				made["piece-place"] = CatalogueJson.Write(place.Entries, new Style(",", ":"));
			}
			if (support != null)
			{
				made["piece-support"] = CatalogueJson.Write(support.Entries, new Style(",", ":"));
			}
			if (vegetation != null)
			{
				made["vegetation"] = CatalogueJson.Write(vegetation.Finish(game), new Style(",", Indent: 0));
			}
			if (dungeons != null)
			{
				made["dungeon-rooms"] = CatalogueJson.Write(dungeons.Json(), new Style(",", ":"));
			}
		}
		return made;
	}

	// The terrain modifiers of the game's prefabs, for the editor to read at its start (GameData):
	// found from the TerrainModifier scripts up to their prefabs (ModifierScan), not by reading every
	// object of every bundle as the catalogues do (seconds, not minutes).
	public static string TerrainModifiersJson(string bundlesFolder)
	{
		using var game = new Game(bundlesFolder);
		game.IndexCabs();
		var scan = new ModifierScan();
		foreach (string name in game.Bundles)
		{
			using var b = game.Open(name);
			if (b != null)
			{
				scan.Read(b, game);
			}
		}
		return CatalogueJson.Write(scan.Entries, new Style(",", Indent: 0));
	}

	// One catalogue's reading of every bundle.
	private interface IScan
	{
		void Read(Bundle b);
	}

	// ---- The bundles.

	// An object of another internal file of the bundles: the file's name (lower case) and its path id.
	private readonly record struct Ref(string Cab, long PathId);

	// The game's bundles, opened one at a time (all of them at once do not fit in memory).
	private sealed class Game : IDisposable
	{
		private readonly AssetsManager _am = new() { UseTemplateFieldCache = true, UseMonoTemplateFieldCache = true, UseRefTypeManagerCache = true, UseQuickLookup = true };
		private readonly string _dir;

		// The bundles' file names, in order.
		public List<string> Bundles { get; }

		// Each internal file (lower case) -> its bundle: filled as the bundles are read.
		public Dictionary<string, string> CabBundle { get; } = new(StringComparer.Ordinal);

		public Game(string dir)
		{
			_dir = dir;
			Bundles = Directory.GetFiles(dir).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal).ToList();
		}

		// A bundle, or null when it is not one.
		public Bundle? Open(string name)
		{
			try
			{
				var b = new Bundle(_am, _am.LoadBundleFile(Path.Combine(_dir, name), false));
				foreach (var f in b.Files)
				{
					CabBundle[f.Cab] = name;
				}
				return b;
			}
			catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or IndexOutOfRangeException or ArgumentException)
			{
				return null;
			}
		}

		// Every bundle's internal files, from their headers only (what Open fills as it goes): for
		// references into bundles not opened yet.
		public void IndexCabs()
		{
			foreach (string name in Bundles)
			{
				try
				{
					var b = _am.LoadBundleFile(Path.Combine(_dir, name), false);
					foreach (var d in b.file.BlockAndDirInfo.DirectoryInfos)
					{
						CabBundle[d.Name.ToLowerInvariant()] = name;
					}
					_am.UnloadBundleFile(b);
				}
				catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or IndexOutOfRangeException or ArgumentException)
				{
					// Not a bundle.
				}
			}
		}

		// The class of each script (MonoScript) met, by where it is; and the bundles opened for them.
		private readonly Dictionary<Ref, string?> _scripts = new();
		private readonly Dictionary<string, Bundle?> _scriptBundles = new(StringComparer.Ordinal);

		// A MonoBehaviour's class ("TerrainModifier", "ZNetView"...), from the script its file lists for
		// it: nothing of the object itself is read. Null when it cannot be told.
		public string? ScriptClass(Bundle b, Bundle.Inner f, AssetFileInfo info)
		{
			var file = f.Assets.file;
			ushort index = info.GetScriptIndex(file);
			if (index == 0xffff || index >= file.Metadata.ScriptTypes.Count)
			{
				return null;
			}
			var script = file.Metadata.ScriptTypes[index];
			if (f.FileOf(script.FileId) is not string cab)
			{
				return null;
			}
			var r = new Ref(cab, script.PathId);
			if (_scripts.TryGetValue(r, out string? cached))
			{
				return cached;
			}
			Bundle? sb = b.Files.Any(x => x.Cab == cab) ? b : null;
			if (sb == null && CabBundle.TryGetValue(cab, out string? name))
			{
				// Scripts are in a few bundles: kept open until the end.
				if (!_scriptBundles.TryGetValue(name, out sb))
				{
					_scriptBundles[name] = sb = Open(name);
				}
			}
			var inner = sb?.Files.FirstOrDefault(x => x.Cab == cab);
			return _scripts[r] = inner == null ? null : sb!.Field(inner, r.PathId)?["m_ClassName"].AsString;
		}

		public string? ScriptClass(Bundle b, long pid) =>
			b.FileWith(pid) is { } f && f.Assets.file.GetAssetInfo(pid) is { } info ? ScriptClass(b, f, info) : null;

		// Each reference's object, read in the bundle that has its file (each bundle opened once);
		// references to files no bundle has are left out.
		public void Visit(IEnumerable<Ref> refs, Action<Bundle, Bundle.Inner, long> visit)
		{
			foreach (var group in refs.Distinct().Where(r => CabBundle.ContainsKey(r.Cab)).GroupBy(r => CabBundle[r.Cab]).OrderBy(g => g.Key, StringComparer.Ordinal))
			{
				using var b = Open(group.Key);
				if (b == null)
				{
					continue;
				}
				foreach (var r in group)
				{
					if (b.Files.FirstOrDefault(f => f.Cab == r.Cab) is { } inner)
					{
						visit(b, inner, r.PathId);
					}
				}
			}
		}

		public void Dispose()
		{
			foreach (var b in _scriptBundles.Values)
			{
				b?.Dispose();
			}
			_am.UnloadAllAssetsFiles(true);
			_am.UnloadAllBundleFiles();
		}
	}

	// A bundle open for a scan: its internal files with objects (scene bundles have two), and every
	// object read once (kept until the bundle is closed).
	private sealed class Bundle : IDisposable
	{
		public sealed record Inner(string Cab, AssetsFileInstance Assets)
		{
			// A reference's file (m_FileID > 0: one this file lists as needed), by name (lower case).
			public string? FileOf(int fileId)
			{
				if (fileId == 0)
				{
					return Cab;
				}
				var ext = Assets.file.Metadata.Externals;
				return fileId - 1 < ext.Count ? ext[fileId - 1].PathName.Split('/').Last().ToLowerInvariant() : null;
			}
		}

		private readonly AssetsManager _am;
		private readonly BundleFileInstance _bundle;
		// Objects by path id over the whole bundle (where two files have the same id: the later file's).
		private readonly Dictionary<long, Inner> _byId = new();
		private readonly Dictionary<(Inner, long), AssetTypeValueField?> _fields = new();

		public List<Inner> Files { get; } = new();

		public Bundle(AssetsManager am, BundleFileInstance bundle)
		{
			_am = am;
			_bundle = bundle;
			var dirs = bundle.file.BlockAndDirInfo.DirectoryInfos;
			for (int i = 0; i < dirs.Count; i++)
			{
				if (!bundle.file.IsAssetsFile(i))
				{
					continue;
				}
				var inner = new Inner(dirs[i].Name.ToLowerInvariant(), am.LoadAssetsFileFromBundle(bundle, i, false));
				Files.Add(inner);
				foreach (var info in inner.Assets.file.AssetInfos)
				{
					_byId[info.PathId] = inner;
				}
			}
		}

		// Every object, in the bundle's order.
		public IEnumerable<(Inner File, AssetFileInfo Info)> Objects => Files.SelectMany(f => f.Assets.file.AssetInfos.Select(i => (f, i)));

		// An object's fields, or null when it is not there or cannot be read.
		public AssetTypeValueField? Field(Inner f, long pid)
		{
			if (_fields.TryGetValue((f, pid), out var cached))
			{
				return cached;
			}
			AssetTypeValueField? field = null;
			if (f.Assets.file.GetAssetInfo(pid) is { } info)
			{
				try
				{
					field = _am.GetBaseField(f.Assets, info);
				}
				catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or IndexOutOfRangeException or ArgumentException or NullReferenceException)
				{
					// A script whose fields are not described: not one of the game's.
				}
			}
			return _fields[(f, pid)] = field;
		}

		// The file that has this path id (where two have it: the later one).
		public Inner? FileWith(long pid) => _byId.GetValueOrDefault(pid);

		// A GameObject's components as (type, path id), nothing read.
		public IEnumerable<(int Type, long Pid)> ComponentIds(AssetTypeValueField go) =>
			go["m_Component.Array"].Children.Select(c => c["component.m_PathID"].AsLong).Select(pid => (TypeOf(pid), pid)).Where(c => c.Item1 >= 0);

		// By path id anywhere in the bundle.
		public AssetTypeValueField? Field(long pid) => _byId.TryGetValue(pid, out var f) ? Field(f, pid) : null;

		public static int TypeOf(Inner f, long pid) => f.Assets.file.GetAssetInfo(pid)?.TypeId ?? -1;

		public int TypeOf(long pid) => _byId.TryGetValue(pid, out var f) ? TypeOf(f, pid) : -1;

		// A GameObject's components that can be read: (type, path id, fields), looked up by path id.
		public IEnumerable<(int Type, long Pid, AssetTypeValueField F)> Components(AssetTypeValueField go)
		{
			foreach (var c in go["m_Component.Array"].Children)
			{
				long pid = c["component.m_PathID"].AsLong;
				int t = TypeOf(pid);
				if (t >= 0 && Field(pid) is { } f)
				{
					yield return (t, pid, f);
				}
			}
		}

		// A reference (m_FileID, m_PathID) made in a file: null for none (path id 0).
		public static Ref? RefOf(Inner f, AssetTypeValueField pptr)
		{
			long pid = pptr["m_PathID"].AsLong;
			return pid != 0 && f.FileOf(pptr["m_FileID"].AsInt) is string cab ? new Ref(cab, pid) : null;
		}

		// Data kept out of the object, in a resource file of the bundle (.resS): meshes.
		public byte[] StreamBytes(AssetTypeValueField streamData)
		{
			int idx = _bundle.file.GetFileIndex(streamData["path"].AsString.Split('/').Last());
			if (idx < 0)
			{
				return Array.Empty<byte>();
			}
			_bundle.file.GetFileRange(idx, out long start, out _);
			_bundle.file.DataReader.Position = start + streamData["offset"].AsLong;
			return _bundle.file.DataReader.ReadBytes((int)streamData["size"].AsUInt);
		}

		public void Dispose()
		{
			foreach (var f in Files)
			{
				_am.UnloadAssetsFile(f.Assets);
			}
			_am.UnloadBundleFile(_bundle);
		}
	}

	// ---- Values, as the catalogues have them.

	private const int GameObjectType = (int)AssetClassID.GameObject;
	private const int TransformType = (int)AssetClassID.Transform;
	private const int MonoBehaviourType = (int)AssetClassID.MonoBehaviour;

	// The "snappoint" tag (the first custom tag).
	private const int SnapTag = 20000;

	private static bool Has(AssetTypeValueField f, string name) => !f[name].IsDummy;

	// A field's value: ints (the game's bools are bytes: 0 or 1), floats (as doubles), strings; lists
	// and structures as lists and dictionaries; null when there is none.
	private static object? Py(AssetTypeValueField f)
	{
		if (f.IsDummy)
		{
			return null;
		}
		if (f.Value != null && f.Value.ValueType != AssetValueType.Array)
		{
			return f.Value.ValueType switch
			{
				AssetValueType.Bool => f.AsBool,
				AssetValueType.Int8 => (long)f.AsSByte,
				AssetValueType.UInt8 => (long)f.AsByte,
				AssetValueType.Int16 => (long)f.AsShort,
				AssetValueType.UInt16 => (long)f.AsUShort,
				AssetValueType.Int32 => (long)f.AsInt,
				AssetValueType.UInt32 => (long)f.AsUInt,
				AssetValueType.Int64 => f.AsLong,
				AssetValueType.UInt64 => (long)f.AsULong,
				AssetValueType.Float => (double)f.AsFloat,
				AssetValueType.Double => f.AsDouble,
				AssetValueType.String => f.AsString,
				_ => throw new NotSupportedException($"field {f.TemplateField.Name}: {f.Value.ValueType}"),
			};
		}
		if (f.TemplateField.IsArray)
		{
			return f.Children.Select(Py).ToList();
		}
		if (f.Children.Count == 1 && f.Children[0].TemplateField.IsArray)
		{
			return Py(f.Children[0]);
		}
		var d = new CatalogueJson.Dict();
		foreach (var c in f.Children)
		{
			d[c.TemplateField.Name] = Py(c);
		}
		return d;
	}

	// A field's value, or the fallback when there is no such field.
	private static object? Get(AssetTypeValueField f, string name, object? fallback) => Has(f, name) ? Py(f[name]) : fallback;

	private static double Num(object? v) => v switch
	{
		long l => l,
		double d => d,
		bool b => b ? 1 : 0,
		_ => throw new InvalidDataException($"not a number: {v}"),
	};

	// As Python's int(): floats cut toward zero.
	private static long Int(object? v) => v switch
	{
		long l => l,
		double d => (long)d,
		bool b => b ? 1 : 0,
		_ => throw new InvalidDataException($"not a number: {v}"),
	};

	private static bool Truthy(object? v) => v switch
	{
		null => false,
		bool b => b,
		long l => l != 0,
		double d => d != 0,
		string s => s.Length > 0,
		System.Collections.ICollection c => c.Count > 0,
		CatalogueJson.Dict d => d.Count > 0,
		_ => true,
	};

	// Rounded to n decimals: floats only (an int stays one).
	private static object? Round(object? v, int n) => v is double d ? CatalogueJson.Round(d, n) : v;

	private static double R(double v, int n) => CatalogueJson.Round(v, n);

	// The larger, the first when they are equal (as Python's max: -0.0 before 0 stays -0.0).
	private static double Max(double a, double b) => b > a ? b : a;

	// The children of a Transform, as GameObjects' path ids (children that cannot be read left out).
	private static IEnumerable<long> ChildObjects(Bundle b, AssetTypeValueField tr)
	{
		foreach (var ch in tr["m_Children.Array"].Children)
		{
			if (b.Field(ch["m_PathID"].AsLong) is { } ct)
			{
				yield return ct["m_GameObject.m_PathID"].AsLong;
			}
		}
	}

	// A prefab: a GameObject whose Transform has no parent. Its components, or null for any other object.
	private static List<(int Type, long Pid, AssetTypeValueField F)>? RootComponents(Bundle b, AssetTypeValueField go)
	{
		var comps = b.Components(go).ToList();
		var tr = comps.FirstOrDefault(c => c.Type == TransformType);
		return tr.F != null && tr.F["m_Father.m_PathID"].AsLong == 0 ? comps : null;
	}

	private static bool IsPiece(AssetTypeValueField f) => Has(f, "m_category") && Has(f, "m_resources") && Has(f, "m_craftingStation");

	private static (double X, double Y, double Z) Vec(AssetTypeValueField v) => (v["x"].AsFloat, v["y"].AsFloat, v["z"].AsFloat);

	// A rotation (x, y, z, w). A prefab root's frame is the identity in whole numbers (Whole): turned
	// negative, its zeros stay 0.0 where computed ones turn -0.0, and the sign of a zero shows in the
	// files ("-0.0"); written, its parts are whole numbers ("0", "1").
	private readonly record struct Quat(double X, double Y, double Z, double W, bool Whole = false)
	{
		public static readonly Quat Root = new(0, 0, 0, 1, true);

		public static Quat Of(AssetTypeValueField q) => new(q["x"].AsFloat, q["y"].AsFloat, q["z"].AsFloat, q["w"].AsFloat);

		private double Neg(double v) => Whole ? 0 - v : -v;

		public Quat Mul(Quat b) => new(
			W * b.X + X * b.W + Y * b.Z - Z * b.Y,
			W * b.Y - X * b.Z + Y * b.W + Z * b.X,
			W * b.Z + X * b.Y - Y * b.X + Z * b.W,
			W * b.W - X * b.X - Y * b.Y - Z * b.Z);

		// v turned by this rotation (q v q^-1).
		public (double X, double Y, double Z) Rotate(double vx, double vy, double vz)
		{
			double nx = Neg(X), ny = Neg(Y), nz = Neg(Z);
			double ix = W * vx + Y * vz - Z * vy, iy = W * vy + Z * vx - X * vz, iz = W * vz + X * vy - Y * vx, iw = nx * vx - Y * vy - Z * vz;
			return (ix * W + iw * nx + iy * nz - iz * ny, iy * W + iw * ny + iz * nx - ix * nz, iz * W + iw * nz + ix * ny - iy * nx);
		}

		public (double X, double Y, double Z) Rotate((double X, double Y, double Z) v) => Rotate(v.X, v.Y, v.Z);

		// Its parts rounded to n decimals (whole numbers stay whole).
		public object[] Rounded(int n) => Whole ? new object[] { 0L, 0L, 0L, 1L } : new object[] { R(X, n), R(Y, n), R(Z, n), R(W, n) };
	}

	private static (double X, double Y, double Z) Add((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X + b.X, a.Y + b.Y, a.Z + b.Z);

	private static (double X, double Y, double Z) Scale((double X, double Y, double Z) a, (double X, double Y, double Z) s) => (a.X * s.X, a.Y * s.Y, a.Z * s.Z);

	private static readonly int[] VertexFormatSize = { 4, 2, 1, 1, 2, 2, 1, 1, 2, 2, 4, 4 };

	// A mesh's vertices (x, y, z each) and each submesh's triangles, as UnityPy reads them (the
	// submesh's base vertex not added); null when it has no vertex data, or one not read here.
	private static (float[] Vertices, List<int[]> Triangles)? MeshData(Bundle b, AssetTypeValueField m)
	{
		var vd = m["m_VertexData"];
		int n = (int)vd["m_VertexCount"].AsUInt;
		byte[] data = vd["m_DataSize"].AsByteArray;
		if (Has(m, "m_StreamData") && m["m_StreamData.path"].AsString.Length > 0)
		{
			data = b.StreamBytes(m["m_StreamData"]);
		}
		if (n == 0 || data.Length == 0)
		{
			return null;
		}
		var channels = vd["m_Channels.Array"].Children.Select(c => (Stream: (int)c["stream"].AsByte, Offset: (int)c["offset"].AsByte, Format: (int)c["format"].AsByte, Dim: c["dimension"].AsByte & 0xF)).ToList();
		if (channels.Count == 0 || channels[0].Dim < 3 || channels[0].Format > 1 || channels.Any(c => c.Dim > 0 && c.Format >= VertexFormatSize.Length))
		{
			return null;
		}
		// Streams one after another, each 16-byte aligned; in a stream a vertex's channels side by side.
		int streams = channels.Max(c => c.Stream) + 1;
		var stride = new int[streams];
		var start = new int[streams];
		foreach (var c in channels.Where(c => c.Dim > 0))
		{
			stride[c.Stream] += c.Dim * VertexFormatSize[c.Format];
		}
		for (int s = 1; s < streams; s++)
		{
			start[s] = (start[s - 1] + n * stride[s - 1] + 15) & ~15;
		}
		var pos = channels[0];
		var vertices = new float[n * 3];
		for (int v = 0; v < n; v++)
		{
			int p = start[pos.Stream] + v * stride[pos.Stream] + pos.Offset;
			for (int d = 0; d < 3; d++)
			{
				vertices[v * 3 + d] = pos.Format == 0 ? BitConverter.ToSingle(data, p + 4 * d) : (float)BitConverter.ToHalf(data, p + 2 * d);
			}
		}
		byte[] ib = m["m_IndexBuffer.Array"].AsByteArray;
		bool wide = m["m_IndexFormat"].AsInt == 1;
		int count = ib.Length / (wide ? 4 : 2);
		var indices = new int[count];
		for (int k = 0; k < count; k++)
		{
			indices[k] = wide ? (int)BitConverter.ToUInt32(ib, 4 * k) : BitConverter.ToUInt16(ib, 2 * k);
		}
		var triangles = new List<int[]>();
		foreach (var sm in m["m_SubMeshes.Array"].Children)
		{
			// Triangles only (topology 0): the game's colliders and rocks have nothing else.
			if (sm["topology"].AsInt != 0)
			{
				return null;
			}
			int first = (int)sm["firstByte"].AsUInt / (wide ? 4 : 2), end = Math.Min(first + (int)sm["indexCount"].AsUInt, count);
			triangles.Add(first < end ? indices[first..end] : Array.Empty<int>());
		}
		return (vertices, triangles);
	}
}
