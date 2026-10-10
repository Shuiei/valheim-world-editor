using System.Text.Json;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using TerrainEditor.Save;

namespace TerrainEditor.App;

// The game's own files, read where Valheim keeps them (valheim_Data/StreamingAssets/SoftRef/Bundles):
// models (their parts, meshes, materials) and textures, straight from the asset bundles, with nothing
// copied. The bundles are Unity's (UnityFS, LZ4 blocks) and carry their type trees, so every field
// reads by name (AssetsTools.NET). Which bundle holds which prefab comes from each bundle's table of
// contents (its AssetBundle's m_Container): read once (a few seconds), kept in a file, read again when
// the game's bundles change. Thread-safe: one reader at a time.
public sealed class GameBundles
{
	public sealed record Where(string Bundle, long PathId)
	{
		public string Key => $"{Bundle}:{PathId}";

		public static Where? FromKey(string key)
		{
			int c = key.LastIndexOf(':');
			return c > 0 && long.TryParse(key.AsSpan(c + 1), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out long pid) ? new Where(key[..c], pid) : null;
		}
	}

	// A model's part: a submesh of a mesh with a material, placed by a matrix (row-major 4 x 4, Unity's
	// left-handed space, relative to the prefab's root).
	public sealed record Part(Where Mesh, int Sub, Where? Material, float[] Matrix);

	public sealed record Model(string Name, List<Part> Parts, float[] RootScale);

	// Per vertex: position (3), normal (3), uv (2), as the game has them (left-handed).
	public sealed record Mesh(string Name, int VertexCount, float[] Positions, float[]? Normals, float[]? Uv, int[][] Submeshes);

	public sealed record TexEnv(Where? Texture, float ScaleX, float ScaleY, float OffsetX, float OffsetY);

	public sealed record Material(string Name, string Shader, Where? ShaderAt, Dictionary<string, TexEnv> Textures, Dictionary<string, float> Floats, Dictionary<string, float[]> Colors);

	// One mip level of a texture, as stored (bottom row first): Unity's TextureFormat, its size, bytes.
	public sealed record TextureLevel(string Name, int Format, int Width, int Height, byte[] Data);

	// What a prefab's root has: a ZNetView (a saved, networked object), a Character (creature), an
	// ItemDrop (an item).
	public sealed record Kind(bool Networked, bool Creature, bool Item);

	private readonly object _sync = new();
	private readonly AssetsManager _am = new() { UseTemplateFieldCache = true, UseRefTypeManagerCache = true, UseQuickLookup = true };
	private readonly string _dir;
	private readonly string? _cacheFile;
	private readonly Dictionary<string, string> _files = new();
	private readonly Dictionary<string, string> _cab = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, (BundleFileInstance Bundle, AssetsFileInstance Assets)> _open = new();
	// Objects read for the model being read (cleared after: the whole game would not fit).
	private readonly Dictionary<(string, long), AssetTypeValueField?> _fields = new();
	private readonly Dictionary<string, Kind> _kinds = new();
	private Dictionary<string, Where>? _index;
	private Dictionary<string, Where> _assets = new();

	// Assets found by their file name in the bundles' tables of contents (besides prefabs): the
	// terrain's material and texture arrays, the world map's material.
	public static readonly string[] NamedAssets = { "Heightmap_basematerial.mat", "terrain_d_array.texture2darray", "terrain_n_array.texture2darray", "minimap.mat" };
	private Dictionary<int, string>? _byHash;

	// dir: the bundles folder; cacheFile: where the index is kept (null: not kept).
	public GameBundles(string dir, string? cacheFile)
	{
		_dir = dir;
		_cacheFile = cacheFile;
		foreach (string f in Directory.GetFiles(dir))
		{
			_files[Path.GetFileName(f)] = f;
		}
	}

	// The bundles folder of a Valheim game folder, or null.
	public static GameBundles? ForGame(string? valheim, string? cacheFile)
	{
		string? dir = valheim == null ? null : GameLook.BundlesDir(valheim);
		return dir == null ? null : new GameBundles(dir, cacheFile);
	}

	public int BundleCount => _files.Count;

	// The bundles folder read.
	public string Folder => _dir;

	// Whether the index is there already (built or read from the kept file); false: asking for it reads
	// it (a few seconds when it must be built).
	public bool Indexed
	{
		get
		{
			lock (_sync)
			{
				return _index != null;
			}
		}
	}

	// Prefab name -> its root object (built or read from the cache on first use).
	public IReadOnlyDictionary<string, Where> Index
	{
		get
		{
			lock (_sync)
			{
				return EnsureIndex();
			}
		}
	}

	// One of NamedAssets, if the game has it.
	public Where? Asset(string fileName)
	{
		lock (_sync)
		{
			EnsureIndex();
			return _assets.GetValueOrDefault(fileName);
		}
	}

	// The prefab of a stable hash (the saves' prefab ids), if the game has it.
	public string? NameOf(int hash)
	{
		lock (_sync)
		{
			EnsureIndex();
			return _byHash!.TryGetValue(hash, out var n) ? n : null;
		}
	}

	public Kind? KindOf(string name)
	{
		lock (_sync)
		{
			if (_kinds.TryGetValue(name, out var k))
			{
				return k;
			}
			if (!EnsureIndex().TryGetValue(name, out var at))
			{
				return null;
			}
			try
			{
				bool znv = false, creature = false, item = false;
				foreach (var (t, _, f) in Components(at.Bundle, Read(at.Bundle, at.PathId)!))
				{
					if (t != (int)AssetClassID.MonoBehaviour)
					{
						continue;
					}
					znv |= Has(f, "m_persistent") && Has(f, "m_distant") && Has(f, "m_type");
					creature |= Has(f, "m_runSpeed");
					item |= Has(f, "m_itemData");
				}
				return _kinds[name] = new Kind(znv, creature, item);
			}
			finally
			{
				_fields.Clear();
			}
		}
	}

	// ---- The index.

	private sealed record IndexFile(List<object[]> Bundles, Dictionary<string, string> Prefabs);

	private List<object[]> BundlesNow() => _files.Keys.Order(StringComparer.Ordinal).Select(n =>
	{
		var fi = new FileInfo(_files[n]);
		return new object[] { n, fi.Length, new DateTimeOffset(fi.LastWriteTimeUtc).ToUnixTimeSeconds() };
	}).ToList();

	private Dictionary<string, Where> EnsureIndex()
	{
		if (_index != null)
		{
			return _index;
		}
		string now = JsonSerializer.Serialize(BundlesNow());
		if (_cacheFile != null && File.Exists(_cacheFile))
		{
			try
			{
				using var doc = JsonDocument.Parse(File.ReadAllText(_cacheFile));
				if (doc.RootElement.GetProperty("Bundles").GetRawText().Replace(" ", "", StringComparison.Ordinal) == now)
				{
					var res = new Dictionary<string, Where>();
					foreach (var p in doc.RootElement.GetProperty("Prefabs").EnumerateObject())
					{
						if (Where.FromKey(p.Value.GetString() ?? "") is Where w)
						{
							res[p.Name] = w;
						}
					}
					var assets = new Dictionary<string, Where>();
					foreach (var p in doc.RootElement.GetProperty("Assets").EnumerateObject())
					{
						if (Where.FromKey(p.Value.GetString() ?? "") is Where w)
						{
							assets[p.Name] = w;
						}
					}
					_assets = assets;
					return SetIndex(res);
				}
			}
			catch (Exception e) when (e is JsonException or IOException or KeyNotFoundException or InvalidOperationException)
			{
				Console.WriteLine($"game files: the index cache could not be read ({e.Message}), reading the bundles again");
			}
		}
		var sw = System.Diagnostics.Stopwatch.StartNew();
		var index = BuildIndex();
		Console.WriteLine($"game files: {index.Count} prefabs in {_files.Count} bundles indexed in {sw.ElapsedMilliseconds} ms");
		if (_cacheFile != null)
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(_cacheFile)!);
				SafeFile.WriteAllText(_cacheFile, JsonSerializer.Serialize(new { Bundles = BundlesNow(), Prefabs = index.ToDictionary(kv => kv.Key, kv => kv.Value.Key), Assets = _assets.ToDictionary(kv => kv.Key, kv => kv.Value.Key) }));
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				Console.WriteLine($"game files: the index could not be kept ({e.Message})");
			}
		}
		return SetIndex(index);
	}

	private Dictionary<string, Where> SetIndex(Dictionary<string, Where> index)
	{
		_byHash = new Dictionary<int, string>();
		foreach (string n in index.Keys)
		{
			_byHash.TryAdd(StableHash.Of(n), n);
		}
		return _index = index;
	}

	// Prefab name -> its root, from each bundle's table of contents (prefabs only). A name in several
	// bundles (a model asset and the prefab made from it): the one with a ZNetView, the prefab the game
	// spawns.
	private Dictionary<string, Where> BuildIndex()
	{
		var res = new Dictionary<string, Where>();
		var twice = new Dictionary<string, List<Where>>();
		var assets = new Dictionary<string, Where>();
		foreach (string bundle in _files.Keys.Order(StringComparer.Ordinal))
		{
			BundleFileInstance b;
			try
			{
				b = _am.LoadBundleFile(_files[bundle], false);
			}
			catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException)
			{
				continue;
			}
			foreach (var d in b.file.BlockAndDirInfo.DirectoryInfos)
			{
				_cab[d.Name.Split('.')[0]] = bundle;
			}
			var a = _am.LoadAssetsFileFromBundle(b, 0, false);
			var ab = a.file.GetAssetsOfType(AssetClassID.AssetBundle).FirstOrDefault();
			if (ab != null)
			{
				foreach (var c in _am.GetBaseField(a, ab)["m_Container.Array"].Children)
				{
					string path = c["first"].AsString;
					if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
					{
						string file = Path.GetFileName(path);
						if (Array.IndexOf(NamedAssets, file) >= 0)
						{
							assets.TryAdd(file, new Where(bundle, c["second.asset.m_PathID"].AsLong));
						}
						continue;
					}
					string n = Path.GetFileNameWithoutExtension(path);
					var w = new Where(bundle, c["second.asset.m_PathID"].AsLong);
					if (!res.TryAdd(n, w))
					{
						if (!twice.TryGetValue(n, out var l))
						{
							twice[n] = l = new List<Where> { res[n] };
						}
						l.Add(w);
					}
				}
			}
			if (!_open.ContainsKey(bundle))
			{
				_am.UnloadAssetsFile(a);
				_am.UnloadBundleFile(b);
			}
		}
		foreach (var (n, list) in twice)
		{
			res[n] = list.FirstOrDefault(IsNetworked) ?? list[0];
		}
		_fields.Clear();
		_assets = assets;
		return res;
	}

	private bool IsNetworked(Where w)
	{
		var go = Read(w.Bundle, w.PathId);
		return go != null && Components(w.Bundle, go).Any(c => c.Type == (int)AssetClassID.MonoBehaviour && Has(c.F, "m_persistent") && Has(c.F, "m_distant"));
	}

	// ---- Reading objects.

	private (BundleFileInstance Bundle, AssetsFileInstance Assets) Open(string bundle)
	{
		if (!_open.TryGetValue(bundle, out var o))
		{
			// Read in place: LZ4 blocks are unpacked as they are read (unpacking all of it is no faster;
			// the time goes into reading objects' fields).
			var b = _am.LoadBundleFile(_files[bundle], false);
			o = (b, _am.LoadAssetsFileFromBundle(b, 0, false));
			_open[bundle] = o;
		}
		return o;
	}

	private void EnsureCabs()
	{
		if (_cab.Count > 0)
		{
			return;
		}
		foreach (var (name, path) in _files)
		{
			var b = _am.LoadBundleFile(path, false);
			foreach (var d in b.file.BlockAndDirInfo.DirectoryInfos)
			{
				_cab[d.Name.Split('.')[0]] = name;
			}
			if (!_open.ContainsKey(name))
			{
				_am.UnloadBundleFile(b);
			}
		}
	}

	private AssetTypeValueField? Read(string bundle, long pathId)
	{
		if (_fields.TryGetValue((bundle, pathId), out var cached))
		{
			return cached;
		}
		var (_, a) = Open(bundle);
		var info = a.file.GetAssetInfo(pathId);
		var f = info == null ? null : _am.GetBaseField(a, info);
		_fields[(bundle, pathId)] = f;
		return f;
	}

	private int TypeOf(string bundle, long pathId) => Open(bundle).Assets.file.GetAssetInfo(pathId)?.TypeId ?? -1;

	private static bool Has(AssetTypeValueField f, string name) => !f[name].IsDummy;

	// A reference (m_FileID, m_PathID) made in a bundle: where it points (another bundle for m_FileID > 0).
	private Where? Resolve(string bundle, AssetTypeValueField pptr)
	{
		long pid = pptr["m_PathID"].AsLong;
		if (pid == 0)
		{
			return null;
		}
		int file = pptr["m_FileID"].AsInt;
		if (file == 0)
		{
			return new Where(bundle, pid);
		}
		var externals = Open(bundle).Assets.file.Metadata.Externals;
		if (file - 1 >= externals.Count)
		{
			return null;
		}
		EnsureCabs();
		string cab = externals[file - 1].PathName.Split('/').Last().Split('.')[0];
		return _cab.TryGetValue(cab, out var b) ? new Where(b, pid) : null;
	}

	// Data kept out of the object, in a resource file of the same bundle (.resS): meshes, textures.
	private byte[] StreamBytes(string bundle, AssetTypeValueField streamData, long skip = 0, long take = -1)
	{
		long offset = streamData["offset"].AsLong + skip;
		long size = take >= 0 ? take : streamData["size"].AsUInt - skip;
		var (b, _) = Open(bundle);
		int idx = b.file.GetFileIndex(streamData["path"].AsString.Split('/').Last());
		if (idx < 0)
		{
			return Array.Empty<byte>();
		}
		b.file.GetFileRange(idx, out long start, out _);
		lock (b.file.DataReader)
		{
			b.file.DataReader.Position = start + offset;
			return b.file.DataReader.ReadBytes((int)size);
		}
	}

	private IEnumerable<(int Type, long Pid, AssetTypeValueField F)> Components(string bundle, AssetTypeValueField go)
	{
		foreach (var c in go["m_Component.Array"].Children)
		{
			long pid = c["component.m_PathID"].AsLong;
			int t = TypeOf(bundle, pid);
			if (t >= 0 && Read(bundle, pid) is { } f)
			{
				yield return (t, pid, f);
			}
		}
	}

	// ---- Models.

	// A prefab's model: what the game draws of it. Left out: the worn and broken looks of pieces
	// (WearNTear), lower levels of detail, inactive objects, disabled renderers; for dungeon rooms also
	// their networked objects (saved objects of their own) and random parts not there half the time.
	// Skinned meshes (animated stations) are drawn in their bind pose at the renderer.
	public Model? LoadModel(string name, bool room)
	{
		lock (_sync)
		{
			if (!EnsureIndex().TryGetValue(name, out var root))
			{
				return null;
			}
			try
			{
				return ReadModel(name, root, room);
			}
			finally
			{
				_fields.Clear();
			}
		}
	}

	private Model ReadModel(string name, Where root, bool room)
	{
		string bundle = root.Bundle;
		var skipGo = new HashSet<long>();
		var lodSkip = new HashSet<long>();
		var parts = new List<Part>();

		IEnumerable<long> ChildObjects(AssetTypeValueField tr)
		{
			foreach (var ch in tr["m_Children.Array"].Children)
			{
				if (Read(bundle, ch["m_PathID"].AsLong) is { } co)
				{
					yield return co["m_GameObject.m_PathID"].AsLong;
				}
			}
		}

		void Scan(long goPid)
		{
			if (Read(bundle, goPid) is not { } go)
			{
				return;
			}
			foreach (var (t, _, f) in Components(bundle, go))
			{
				if (t == (int)AssetClassID.MonoBehaviour)
				{
					foreach (string k in new[] { "m_worn", "m_broken" })
					{
						if (Has(f, k) && f[k]["m_FileID"].AsInt == 0 && f[k]["m_PathID"].AsLong != 0)
						{
							skipGo.Add(f[k]["m_PathID"].AsLong);
						}
					}
					if (Has(f, "m_new"))
					{
						long nr = f["m_new.m_PathID"].AsLong;
						foreach (string k in new[] { "m_worn", "m_broken" })
						{
							if (Has(f, k) && f[k]["m_PathID"].AsLong == nr)
							{
								skipGo.Remove(nr);
							}
						}
					}
				}
				else if (t == (int)AssetClassID.LODGroup)
				{
					var lods = f["m_LODs.Array"].Children;
					for (int li = 1; li < lods.Count; li++)
					{
						foreach (var r in lods[li]["renderers.Array"].Children)
						{
							if (r["renderer.m_PathID"].AsLong != 0)
							{
								lodSkip.Add(r["renderer.m_PathID"].AsLong);
							}
						}
					}
					if (lods.Count > 0)
					{
						foreach (var r in lods[0]["renderers.Array"].Children)
						{
							lodSkip.Remove(r["renderer.m_PathID"].AsLong);
						}
					}
				}
				else if (t == (int)AssetClassID.Transform)
				{
					foreach (long c in ChildObjects(f))
					{
						Scan(c);
					}
				}
			}
		}

		void RoomSkips(long goPid, bool isRoot)
		{
			if (Read(bundle, goPid) is not { } go)
			{
				return;
			}
			AssetTypeValueField? tr = null;
			foreach (var (t, _, f) in Components(bundle, go))
			{
				if (t == (int)AssetClassID.Transform)
				{
					tr = f;
				}
				if (t != (int)AssetClassID.MonoBehaviour)
				{
					continue;
				}
				if (!isRoot && Has(f, "m_persistent") && Has(f, "m_distant"))
				{
					skipGo.Add(goPid);
				}
				else if (Has(f, "m_chanceToSpawn") && Has(f, "m_OffObject"))
				{
					long off = f["m_OffObject.m_PathID"].AsLong;
					if (f["m_chanceToSpawn"].AsFloat >= 50)
					{
						if (off != 0)
						{
							skipGo.Add(off);
						}
					}
					else
					{
						skipGo.Add(goPid);
					}
				}
				else if (Has(f, "m_objects") && Has(f, "m_dungeonRequireTheme"))
				{
					foreach (var e in f["m_objects.Array"].Children.Skip(1))
					{
						long c = e["m_object.m_PathID"].AsLong;
						if (c != 0)
						{
							skipGo.Add(c);
						}
					}
				}
			}
			if (tr != null)
			{
				foreach (long c in ChildObjects(tr))
				{
					RoomSkips(c, false);
				}
			}
		}

		void Walk(long goPid, float[] parent, bool isRoot)
		{
			if (!isRoot && skipGo.Contains(goPid) || Read(bundle, goPid) is not { } go || !isRoot && !go["m_IsActive"].AsBool)
			{
				return;
			}
			var cs = Components(bundle, go).ToList();
			var tr = cs.FirstOrDefault(c => c.Type == (int)AssetClassID.Transform).F;
			if (tr == null)
			{
				return;
			}
			float[] w = isRoot ? Matrix.Identity() : Matrix.Mul(parent, Matrix.Trs(tr["m_LocalPosition"], tr["m_LocalRotation"], tr["m_LocalScale"]));
			var mf = cs.FirstOrDefault(c => c.Type == (int)AssetClassID.MeshFilter);
			var mr = cs.FirstOrDefault(c => c.Type == (int)AssetClassID.MeshRenderer);
			var smr = cs.FirstOrDefault(c => c.Type == (int)AssetClassID.SkinnedMeshRenderer);
			AssetTypeValueField? meshRef = mf.F?["m_Mesh"];
			if (smr.F != null && mr.F == null)
			{
				mr = smr;
				meshRef = smr.F["m_Mesh"];
			}
			if (meshRef != null && mr.F != null && mr.F["m_Enabled"].AsBool && !lodSkip.Contains(mr.Pid) && Resolve(bundle, meshRef) is Where mw)
			{
				int subs = SubmeshCount(mw);
				var mats = mr.F["m_Materials.Array"].Children;
				for (int si = 0; si < mats.Count && si < subs; si++)
				{
					parts.Add(new Part(mw, si, Resolve(bundle, mats[si]), w));
				}
			}
			foreach (long c in ChildObjects(tr))
			{
				Walk(c, w, false);
			}
		}

		Scan(root.PathId);
		if (room)
		{
			RoomSkips(root.PathId, true);
		}
		Walk(root.PathId, Matrix.Identity(), true);
		float[] rootScale = { 1, 1, 1 };
		if (Read(bundle, root.PathId) is { } rgo && Components(bundle, rgo).FirstOrDefault(c => c.Type == (int)AssetClassID.Transform).F is { } rt)
		{
			var s = rt["m_LocalScale"];
			rootScale = new[] { s["x"].AsFloat, s["y"].AsFloat, s["z"].AsFloat };
		}
		return new Model(name, parts, rootScale);
	}

	private int SubmeshCount(Where mesh) => Read(mesh.Bundle, mesh.PathId)?["m_SubMeshes.Array"].Children.Count ?? 0;

	private static readonly int[] FormatSize = { 4, 2, 1, 1, 2, 2, 1, 1, 2, 2, 4, 4 };

	public Mesh? LoadMesh(Where at)
	{
		lock (_sync)
		{
			try
			{
				return Read(at.Bundle, at.PathId) is { } m ? ReadMesh(at, m) : null;
			}
			finally
			{
				_fields.Clear();
			}
		}
	}

	private Mesh ReadMesh(Where at, AssetTypeValueField m)
	{
		string name = m["m_Name"].AsString;
		if (Has(m, "m_CompressedMesh") && m["m_CompressedMesh.m_Vertices.m_NumItems"].AsUInt > 0)
		{
			throw new NotSupportedException($"mesh {name} is compressed");
		}
		var vd = m["m_VertexData"];
		int n = (int)vd["m_VertexCount"].AsUInt;
		byte[] data = vd["m_DataSize"].AsByteArray;
		if (data.Length == 0 && m["m_StreamData.size"].AsUInt > 0)
		{
			data = StreamBytes(at.Bundle, m["m_StreamData"]);
		}
		var channels = vd["m_Channels.Array"].Children.Select(c => (Stream: (int)c["stream"].AsByte, Offset: (int)c["offset"].AsByte, Format: (int)c["format"].AsByte, Dim: c["dimension"].AsByte & 0xF)).ToList();
		var used = channels.Where(c => c.Dim > 0 && c.Format < FormatSize.Length).ToList();
		// Streams follow one another, each starting 16-byte aligned; in a stream a vertex's channels are
		// side by side.
		int streams = used.Select(c => c.Stream).DefaultIfEmpty(0).Max() + 1;
		var stride = new int[streams];
		foreach (var c in used)
		{
			stride[c.Stream] = Math.Max(stride[c.Stream], c.Offset + c.Dim * FormatSize[c.Format]);
		}
		var start = new int[streams];
		for (int s = 1; s < streams; s++)
		{
			start[s] = (start[s - 1] + stride[s - 1] * n + 15) & ~15;
		}
		float[]? Channel(int i, int want)
		{
			if (i >= channels.Count || channels[i].Dim == 0 || channels[i].Format >= FormatSize.Length)
			{
				return null;
			}
			var c = channels[i];
			var res = new float[n * want];
			for (int v = 0; v < n; v++)
			{
				int p = start[c.Stream] + v * stride[c.Stream] + c.Offset;
				if (p + c.Dim * FormatSize[c.Format] > data.Length)
				{
					break;
				}
				for (int d = 0; d < Math.Min(want, c.Dim); d++)
				{
					res[v * want + d] = c.Format switch
					{
						0 => BitConverter.ToSingle(data, p + 4 * d),
						1 => (float)BitConverter.ToHalf(data, p + 2 * d),
						2 => data[p + d] / 255f,
						3 => Math.Max((sbyte)data[p + d] / 127f, -1),
						4 => BitConverter.ToUInt16(data, p + 2 * d) / 65535f,
						5 => Math.Max(BitConverter.ToInt16(data, p + 2 * d) / 32767f, -1),
						_ => 0,
					};
				}
			}
			return res;
		}
		byte[] ib = m["m_IndexBuffer.Array"].AsByteArray;
		bool wide = m["m_IndexFormat"].AsInt == 1;
		var subs = new List<int[]>();
		foreach (var sm in m["m_SubMeshes.Array"].Children)
		{
			int first = (int)sm["firstByte"].AsUInt, count = (int)sm["indexCount"].AsUInt, baseV = (int)sm["baseVertex"].AsUInt;
			// Triangles only (0); other topologies (lines, points) draw nothing here.
			if (sm["topology"].AsInt != 0 || first + count * (wide ? 4 : 2) > ib.Length)
			{
				subs.Add(Array.Empty<int>());
				continue;
			}
			var idx = new int[count - count % 3];
			for (int k = 0; k < idx.Length; k++)
			{
				idx[k] = baseV + (wide ? (int)BitConverter.ToUInt32(ib, first + 4 * k) : BitConverter.ToUInt16(ib, first + 2 * k));
			}
			subs.Add(idx);
		}
		return new Mesh(name, n, Channel(0, 3) ?? new float[n * 3], Channel(1, 3), Channel(4, 2), subs.ToArray());
	}

	// ---- Materials and textures.

	public Material? LoadMaterial(Where at)
	{
		lock (_sync)
		{
			try
			{
				if (Read(at.Bundle, at.PathId) is not { } m)
				{
					return null;
				}
				string shader = "";
				var shaderAt = Resolve(at.Bundle, m["m_Shader"]);
				if (shaderAt is Where sh && Read(sh.Bundle, sh.PathId) is { } shf && Has(shf, "m_ParsedForm"))
				{
					shader = shf["m_ParsedForm.m_Name"].AsString;
				}
				var props = m["m_SavedProperties"];
				var texs = new Dictionary<string, TexEnv>();
				foreach (var te in props["m_TexEnvs.Array"].Children)
				{
					var s = te["second"];
					texs[te["first"].AsString] = new TexEnv(Resolve(at.Bundle, s["m_Texture"]), s["m_Scale.x"].AsFloat, s["m_Scale.y"].AsFloat, s["m_Offset.x"].AsFloat, s["m_Offset.y"].AsFloat);
				}
				var floats = new Dictionary<string, float>();
				foreach (var f in props["m_Floats.Array"].Children)
				{
					floats[f["first"].AsString] = f["second"].AsFloat;
				}
				var colors = new Dictionary<string, float[]>();
				foreach (var c in props["m_Colors.Array"].Children)
				{
					var v = c["second"];
					colors[c["first"].AsString] = new[] { v["r"].AsFloat, v["g"].AsFloat, v["b"].AsFloat, v["a"].AsFloat };
				}
				return new Material(m["m_Name"].AsString, shader, shaderAt, texs, floats, colors);
			}
			finally
			{
				_fields.Clear();
			}
		}
	}

	// Bytes per 4 x 4 block (block formats) or per pixel, by Unity TextureFormat; 0: not read here.
	public static (int Bytes, bool Block) FormatLayout(int format) => format switch
	{
		10 or 26 => (8, true),             // DXT1, BC4
		12 or 25 or 27 or 24 => (16, true), // DXT5, BC7, BC5, BC6H
		1 or 63 => (1, false),             // Alpha8, R8
		3 => (3, false),                   // RGB24
		4 or 5 or 14 => (4, false),        // RGBA32, ARGB32, BGRA32
		_ => (0, false),
	};

	public static int LevelSize(int format, int w, int h)
	{
		var (bytes, block) = FormatLayout(format);
		return block ? Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4) * bytes : w * h * bytes;
	}

	// The largest mip level no wider or taller than maxSize (the smallest there is when none is).
	public TextureLevel? LoadTexture(Where at, int maxSize)
	{
		lock (_sync)
		{
			try
			{
				if (Read(at.Bundle, at.PathId) is not { } t || TypeOf(at.Bundle, at.PathId) != (int)AssetClassID.Texture2D)
				{
					return null;
				}
				int format = t["m_TextureFormat"].AsInt, w = t["m_Width"].AsInt, h = t["m_Height"].AsInt, mips = Math.Max(1, t["m_MipCount"].AsInt);
				if (FormatLayout(format).Bytes == 0 || w <= 0 || h <= 0)
				{
					return null;
				}
				long skip = 0;
				int level = 0;
				while (level < mips - 1 && Math.Max(w, h) > maxSize)
				{
					skip += LevelSize(format, w, h);
					w = Math.Max(1, w / 2);
					h = Math.Max(1, h / 2);
					level++;
				}
				int size = LevelSize(format, w, h);
				byte[] data;
				if (t["m_StreamData.size"].AsUInt > 0)
				{
					data = StreamBytes(at.Bundle, t["m_StreamData"], skip, size);
				}
				else
				{
					byte[] all = t["image data"].AsByteArray;
					if (skip + size > all.Length)
					{
						return null;
					}
					data = all.AsSpan((int)skip, size).ToArray();
				}
				return data.Length == size ? new TextureLevel(t["m_Name"].AsString, format, w, h, data) : null;
			}
			finally
			{
				_fields.Clear();
			}
		}
	}

	// A Texture2DArray's GraphicsFormat as a TextureFormat (the compressed ones read here).
	private static int FromGraphicsFormat(int gf) => gf switch
	{
		96 or 97 => 10,
		98 or 99 => 11,
		100 or 101 => 12,
		102 or 103 => 26,
		104 or 105 => 27,
		106 or 107 => 24,
		108 or 109 => 25,
		8 or 4 => 4,
		_ => gf,
	};

	// The top level of each slice of a texture array (bottom row first).
	public List<TextureLevel>? LoadTextureArray(Where at)
	{
		lock (_sync)
		{
			try
			{
				if (Read(at.Bundle, at.PathId) is not { } t || TypeOf(at.Bundle, at.PathId) != (int)AssetClassID.Texture2DArray)
				{
					return null;
				}
				int format = FromGraphicsFormat(t["m_Format"].AsInt), w = t["m_Width"].AsInt, h = t["m_Height"].AsInt, depth = t["m_Depth"].AsInt;
				byte[] data = t["image data"].AsByteArray;
				if (data.Length == 0 && t["m_StreamData.size"].AsUInt > 0)
				{
					data = StreamBytes(at.Bundle, t["m_StreamData"]);
				}
				int size = LevelSize(format, w, h);
				if (depth <= 0 || FormatLayout(format).Bytes == 0 || data.Length / depth < size)
				{
					return null;
				}
				// Slice after slice, each with its mip levels.
				int per = data.Length / depth;
				return Enumerable.Range(0, depth).Select(i => new TextureLevel($"{t["m_Name"].AsString}[{i}]", format, w, h, data.AsSpan(i * per, size).ToArray())).ToList();
			}
			finally
			{
				_fields.Clear();
			}
		}
	}

	// A shader's programs: the platforms it has, each platform's program blob (LZ4-unpacked), and the
	// sub-programs of its deferred pass (for the Vulkan one).
	public sealed record ShaderPrograms(int[] Platforms, Dictionary<int, byte[]> Blobs, List<GameShader.SubProgram> Deferred);

	public ShaderPrograms? LoadShader(Where at)
	{
		lock (_sync)
		{
			try
			{
				if (Read(at.Bundle, at.PathId) is not { } sh || TypeOf(at.Bundle, at.PathId) != (int)AssetClassID.Shader)
				{
					return null;
				}
				int[] platforms = sh["platforms.Array"].Children.Select(c => (int)c.AsUInt).ToArray();
				byte[] blob = sh["compressedBlob.Array"].AsByteArray;
				var blobs = new Dictionary<int, byte[]>();
				for (int pi = 0; pi < platforms.Length; pi++)
				{
					var offs = Numbers(sh["offsets.Array"].Children[pi]).ToList();
					var comp = Numbers(sh["compressedLengths.Array"].Children[pi]).ToList();
					var full = Numbers(sh["decompressedLengths.Array"].Children[pi]).ToList();
					using var ms = new MemoryStream();
					for (int k = 0; k < offs.Count; k++)
					{
						var outBuf = new byte[full[k]];
						int got = LZ4ps.LZ4Codec.Decode32(blob, (int)offs[k], (int)comp[k], outBuf, 0, (int)full[k], true);
						if (got != full[k])
						{
							throw new InvalidDataException("the terrain shader's program could not be unpacked");
						}
						ms.Write(outBuf);
					}
					blobs[platforms[pi]] = ms.ToArray();
				}
				var deferred = new List<GameShader.SubProgram>();
				foreach (var sub in sh["m_ParsedForm.m_SubShaders.Array"].Children)
				{
					foreach (var ps in sub["m_Passes.Array"].Children)
					{
						if (ps["m_State.m_Name"].AsString != "DEFERRED")
						{
							continue;
						}
						var prog = ps["progVertex"];
						var lists = prog["m_PlayerSubPrograms.Array"].Children;
						var paramLists = prog["m_ParameterBlobIndices.Array"].Children;
						for (int li = 0; li < lists.Count; li++)
						{
							var items = lists[li]["Array"].Children;
							var prms = li < paramLists.Count ? Numbers(paramLists[li]).ToList() : new List<uint>();
							for (int j = 0; j < items.Count; j++)
							{
								var sp = items[j];
								deferred.Add(new GameShader.SubProgram(sp["m_GpuProgramType"].AsInt, sp["m_KeywordIndices.Array"].Children.Count, (int)sp["m_BlobIndex"].AsUInt, j < prms.Count ? (int)prms[j] : int.MaxValue));
							}
						}
					}
				}
				return new ShaderPrograms(platforms, blobs, deferred);
			}
			finally
			{
				_fields.Clear();
			}
		}
	}

	// The numbers of a field: itself, or those of its array (nested arrays flattened).
	private static IEnumerable<uint> Numbers(AssetTypeValueField f)
	{
		if (!f["Array"].IsDummy)
		{
			foreach (var c in f["Array"].Children)
			{
				foreach (uint n in Numbers(c))
				{
					yield return n;
				}
			}
		}
		else if (f.TemplateField.IsArray)
		{
			foreach (var c in f.Children)
			{
				foreach (uint n in Numbers(c))
				{
					yield return n;
				}
			}
		}
		else
		{
			yield return f.AsUInt;
		}
	}

	// ---- Matrices: row-major 4 x 4, column vectors (Unity's).
	public static class Matrix
	{
		public static float[] Identity() => new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };

		public static float[] Mul(float[] a, float[] b)
		{
			var r = new float[16];
			for (int i = 0; i < 4; i++)
			{
				for (int j = 0; j < 4; j++)
				{
					float s = 0;
					for (int k = 0; k < 4; k++)
					{
						s += a[i * 4 + k] * b[k * 4 + j];
					}
					r[i * 4 + j] = s;
				}
			}
			return r;
		}

		public static float[] Trs(float px, float py, float pz, float x, float y, float z, float w, float sx, float sy, float sz) => new[]
		{
			(1 - 2 * (y * y + z * z)) * sx, 2 * (x * y - z * w) * sy, 2 * (x * z + y * w) * sz, px,
			2 * (x * y + z * w) * sx, (1 - 2 * (x * x + z * z)) * sy, 2 * (y * z - x * w) * sz, py,
			2 * (x * z - y * w) * sx, 2 * (y * z + x * w) * sy, (1 - 2 * (x * x + y * y)) * sz, pz,
			0, 0, 0, 1,
		};

		internal static float[] Trs(AssetTypeValueField p, AssetTypeValueField q, AssetTypeValueField s) =>
			Trs(p["x"].AsFloat, p["y"].AsFloat, p["z"].AsFloat, q["x"].AsFloat, q["y"].AsFloat, q["z"].AsFloat, q["w"].AsFloat, s["x"].AsFloat, s["y"].AsFloat, s["z"].AsFloat);
	}
}
