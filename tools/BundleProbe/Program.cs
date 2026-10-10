// Prototype: read Valheim's models straight from the game's asset bundles (no export step), and check
// the result against what the Python exporter (tools/asset-export) made.
// Usage: BundleProbe [--bundles <dir>] [--look <game-look dir>] [--index <model_index.json>] [names...]
// Findings (2026-10-10, Valheim build 25730771, AssetsTools.NET 3.0.5):
// - The 799 bundles are UnityFS with LZ4 blocks and carry their type trees: every field reads by name.
// - Index (prefab name -> bundle, object) from each bundle's AssetBundle m_Container: 6507 prefabs in
//   ~5.5 s, the same object as the Python scan for all 2029 exported models (names in two bundles: the
//   one with a ZNetView).
// - Models match the exporter exactly (parts, vertices): small pieces 20-70 ms, rooms ~0.1 s, the
//   biggest room (morkhalla_entrance02, 15k objects, 2028 parts) ~9 s; the time is field parsing
//   (~0.15 ms per object), not LZ4 (unpacking in memory is no faster).
// - Textures are BC7, DXT5 and DXT1 only (no crunch): GPU-uploadable as they are.
using System.Diagnostics;
using System.Text.Json;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
string bundlesDir = Path.Combine(home, ".steam/steam/steamapps/common/Valheim/valheim_Data/StreamingAssets/SoftRef/Bundles");
string look = Path.Combine(home, ".local/share/ValheimWorldEditor/game-look");
string? pyIndex = Path.Combine(home, ".local/share/ValheimWorldEditor/export-cache/model_index.json");
var names = new List<string>();
for (int i = 0; i < args.Length; i++)
{
	switch (args[i])
	{
		case "--bundles": bundlesDir = args[++i]; break;
		case "--look": look = args[++i]; break;
		case "--index": pyIndex = args[++i]; break;
		default: names.Add(args[i]); break;
	}
}
if (names.Count == 0)
{
	names.AddRange(new[] { "piece_workbench", "wood_wall_roof", "Beech1", "Pickable_Mushroom", "morkhalla_entrance02", "cave_new_deeproom_bottom_shrine" });
}

var total = Stopwatch.StartNew();
var game = new GameBundles(bundlesDir);
Console.WriteLine($"opened {game.Count} bundles (cab map {game.CabCount}) in {total.ElapsedMilliseconds} ms");
var sw = Stopwatch.StartNew();
var index = game.BuildIndex();
Console.WriteLine($"index: {index.Count} prefabs in {sw.ElapsedMilliseconds} ms");

// Against the Python scan: same bundle and object for every model it exported?
if (pyIndex != null && File.Exists(pyIndex))
{
	using var doc = JsonDocument.Parse(File.ReadAllText(pyIndex));
	int same = 0, missing = 0, other = 0;
	var missed = new List<string>();
	foreach (var e in doc.RootElement.EnumerateObject())
	{
		string n = e.Value.GetProperty("name").GetString()!;
		string b = e.Value.GetProperty("bundle").GetString()!;
		long pid = e.Value.GetProperty("pid").GetInt64();
		if (!index.TryGetValue(n, out var at))
		{
			missing++;
			missed.Add(n);
		}
		else if (at.Bundle == b && at.PathId == pid)
		{
			same++;
		}
		else
		{
			other++;
			if (other <= 5) Console.WriteLine($"  differs: {n} python {b}:{pid} here {at.Bundle}:{at.PathId}");
		}
	}
	Console.WriteLine($"vs python index: {same} same, {other} other object, {missing} missing{(missed.Count > 0 ? " (" + string.Join(", ", missed.Take(12)) + ")" : "")}");
}

var meshInfo = File.Exists(Path.Combine(look, "models/meshinfo.json")) ? JsonDocument.Parse(File.ReadAllText(Path.Combine(look, "models/meshinfo.json"))) : null;
var formats = new Dictionary<int, int>();
foreach (string n in names)
{
	if (!index.TryGetValue(n, out var at))
	{
		Console.WriteLine($"{n}: not found");
		continue;
	}
	sw.Restart();
	var model = game.LoadModel(at, isRoom: n.Contains("room") || n.StartsWith("morkhalla") || n.StartsWith("cave_") || n.StartsWith("dvergr_") || n.StartsWith("hole_"));
	long ms = sw.ElapsedMilliseconds;
	int verts = model.Parts.Select(p => p.Mesh).Distinct().Sum(m => m.VertexCount);
	int tris = model.Parts.Sum(p => p.Mesh.SubMeshes[p.Sub].Length / 3);
	foreach (var t in model.Parts.SelectMany(p => p.Material?.Textures.Values ?? Enumerable.Empty<TextureRef>()).Distinct())
	{
		formats[t.Format] = formats.GetValueOrDefault(t.Format) + 1;
	}
	string py = "";
	string pj = Path.Combine(look, "models/pieces", n + ".json");
	if (File.Exists(pj) && meshInfo != null)
	{
		using var pd = JsonDocument.Parse(File.ReadAllText(pj));
		var parts = pd.RootElement.GetProperty("parts").EnumerateArray().ToList();
		int pv = parts.Select(p => p.GetProperty("mesh").GetString()!).Distinct().Sum(m => meshInfo.RootElement.TryGetProperty(m, out var mi) ? mi.GetProperty("v").GetInt32() : 0);
		py = $" | python: {parts.Count} parts, {pv} vertices";
	}
	Console.WriteLine($"{n}: {model.Parts.Count} parts, {model.Parts.Select(p => p.Mesh).Distinct().Count()} meshes, {verts} vertices, {tris} triangles, {model.Parts.Select(p => p.Material).Distinct().Count()} materials in {ms} ms{py}");
}
foreach (var kv in GameBundles.ReadTime.OrderByDescending(k => k.Value).Take(8)) Console.WriteLine($"  read {kv.Key}: {GameBundles.ReadCount[kv.Key]} in {kv.Value * 1000 / Stopwatch.Frequency} ms");
Console.WriteLine("texture formats used: " + string.Join(", ", formats.OrderByDescending(f => f.Value).Select(f => $"{TextureRef.FormatName(f.Key)}×{f.Value}")));
Console.WriteLine($"total {total.ElapsedMilliseconds} ms, memory {GC.GetTotalMemory(false) / 1e6:0} MB managed, {Environment.WorkingSet / 1e6:0} MB working set");

public sealed record Where(string Bundle, long PathId);

public sealed class MeshData
{
	public required string Name;
	public int VertexCount;
	public float[] Positions = Array.Empty<float>();
	public float[]? Normals;
	public float[]? Uv0;
	public List<int[]> SubMeshes = new();
}

public sealed partial record TextureRef(string Name, int Format, int Width, int Height, int Mips, long StreamSize);

public sealed class MaterialData
{
	public required string Name;
	public string Shader = "";
	public Dictionary<string, TextureRef> Textures = new();
}

public sealed record Part(MeshData Mesh, int Sub, MaterialData? Material, float[] Matrix);

public sealed class Model
{
	public List<Part> Parts = new();
}

public sealed class GameBundles
{
	private readonly AssetsManager _am = new() { UseTemplateFieldCache = true, UseRefTypeManagerCache = true, UseQuickLookup = true };
	private readonly Dictionary<(string, long), AssetTypeValueField?> _fields = new();
	private readonly Dictionary<string, string> _files = new();
	private readonly Dictionary<string, string> _cab = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, (BundleFileInstance Bundle, AssetsFileInstance Assets)> _open = new();
	private readonly Dictionary<(string, long), MeshData> _meshes = new();
	private readonly Dictionary<(string, long), MaterialData> _materials = new();

	public int Count => _files.Count;
	public static readonly Dictionary<string, long> ReadTime = new();
	public static readonly Dictionary<string, int> ReadCount = new();
	public int CabCount => _cab.Count;

	public GameBundles(string dir)
	{
		foreach (string f in Directory.GetFiles(dir))
		{
			string name = Path.GetFileName(f);
			_files[name] = f;
			var b = _am.LoadBundleFile(f, false);
			foreach (var d in b.file.BlockAndDirInfo.DirectoryInfos)
			{
				_cab[d.Name.Split('.')[0]] = name;
			}
			_am.UnloadBundleFile(b);
		}
	}

	private (BundleFileInstance Bundle, AssetsFileInstance Assets) Open(string bundle)
	{
		if (!_open.TryGetValue(bundle, out var o))
		{
			// Read in place (unpacking in memory was tried: no faster, the time is in parsing objects).
			var b = _am.LoadBundleFile(_files[bundle], false);
			o = (b, _am.LoadAssetsFileFromBundle(b, 0, false));
			_open[bundle] = o;
		}
		return o;
	}

	// Prefab name -> where its root is, from each bundle's table of contents (prefabs only).
	public Dictionary<string, Where> BuildIndex()
	{
		var res = new Dictionary<string, Where>();
		var twice = new Dictionary<string, List<Where>>();
		foreach (string bundle in _files.Keys.Order())
		{
			var b = _am.LoadBundleFile(_files[bundle], false);
			var a = _am.LoadAssetsFileFromBundle(b, 0, false);
			var ab = a.file.GetAssetsOfType(AssetClassID.AssetBundle).FirstOrDefault();
			if (ab != null)
			{
				foreach (var c in _am.GetBaseField(a, ab)["m_Container.Array"].Children)
				{
					string path = c["first"].AsString;
					if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
					{
						string n = Path.GetFileNameWithoutExtension(path);
						var w = new Where(bundle, c["second.asset.m_PathID"].AsLong);
						if (!res.TryAdd(n, w))
						{
							if (!twice.TryGetValue(n, out var l)) twice[n] = l = new List<Where> { res[n] };
							l.Add(w);
						}
					}
				}
			}
			_am.UnloadAssetsFile(a);
			_am.UnloadBundleFile(b);
		}
		// A name in several bundles (a model asset and the prefab made from it): the one with a
		// ZNetView (the networked prefab the game spawns), as the Python scan chose.
		foreach (var (n, list) in twice)
		{
			res[n] = list.FirstOrDefault(IsNetworked) ?? list[0];
		}
		return res;
	}

	private AssetTypeValueField? Read(string bundle, long pathId)
	{
		if (_fields.TryGetValue((bundle, pathId), out var cached)) return cached;
		long t0 = Stopwatch.GetTimestamp();
		var (_, a) = Open(bundle);
		var info = a.file.GetAssetInfo(pathId);
		var f = info == null ? null : _am.GetBaseField(a, info);
		_fields[(bundle, pathId)] = f;
		long dt = Stopwatch.GetTimestamp() - t0;
		string k = info == null ? "none" : ((AssetClassID)info.TypeId).ToString();
		ReadTime[k] = ReadTime.GetValueOrDefault(k) + dt;
		ReadCount[k] = ReadCount.GetValueOrDefault(k) + 1;
		return f;
	}

	private bool IsNetworked(Where w)
	{
		var go = Read(w.Bundle, w.PathId);
		if (go == null) return false;
		foreach (var c in go["m_Component.Array"].Children)
		{
			long pid = c["component.m_PathID"].AsLong;
			if (TypeOf(w.Bundle, pid) == (int)AssetClassID.MonoBehaviour && Read(w.Bundle, pid) is { } f && !f["m_persistent"].IsDummy && !f["m_distant"].IsDummy) return true;
		}
		return false;
	}

	private int TypeOf(string bundle, long pathId) => Open(bundle).Assets.file.GetAssetInfo(pathId)?.TypeId ?? -1;

	// A reference (m_FileID, m_PathID) from an object of this bundle: the bundle it points into.
	private Where? Resolve(string bundle, AssetTypeValueField pptr)
	{
		long pid = pptr["m_PathID"].AsLong;
		if (pid == 0) return null;
		int file = pptr["m_FileID"].AsInt;
		if (file == 0) return new Where(bundle, pid);
		var ext = Open(bundle).Assets.file.Metadata.Externals[file - 1];
		string cab = ext.PathName.Split('/').Last().Split('.')[0];
		return _cab.TryGetValue(cab, out var b) ? new Where(b, pid) : null;
	}

	private byte[] StreamBytes(string bundle, AssetTypeValueField streamData)
	{
		string path = streamData["path"].AsString;
		long offset = streamData["offset"].AsLong;
		int size = (int)streamData["size"].AsUInt;
		var (b, _) = Open(bundle);
		string file = path.Split('/').Last();
		int idx = b.file.GetFileIndex(file);
		b.file.GetFileRange(idx, out long start, out _);
		var r = b.file.DataReader;
		r.Position = start + offset;
		return r.ReadBytes(size);
	}

	public Model LoadModel(Where root, bool isRoom)
	{
		string bundle = root.Bundle;
		var skipGo = new HashSet<long>();
		var lodSkip = new HashSet<long>();
		var model = new Model();

		IEnumerable<(int Type, long Pid, AssetTypeValueField F)> Comps(AssetTypeValueField go)
		{
			foreach (var c in go["m_Component.Array"].Children)
			{
				long pid = c["component.m_PathID"].AsLong;
				int t = TypeOf(bundle, pid);
				if (t < 0) continue;
				var f = Read(bundle, pid);
				if (f != null) yield return (t, pid, f);
			}
		}
		static bool Has(AssetTypeValueField f, string name) => !f[name].IsDummy;

		void Scan(long goPid)
		{
			var go = Read(bundle, goPid)!;
			foreach (var (t, _, f) in Comps(go))
			{
				if (t == (int)AssetClassID.MonoBehaviour)
				{
					foreach (string k in new[] { "m_worn", "m_broken" })
					{
						if (Has(f, k) && f[k]["m_FileID"].AsInt == 0 && f[k]["m_PathID"].AsLong != 0) skipGo.Add(f[k]["m_PathID"].AsLong);
					}
					if (Has(f, "m_new"))
					{
						long nr = f["m_new.m_PathID"].AsLong;
						foreach (string k in new[] { "m_worn", "m_broken" })
						{
							if (Has(f, k) && f[k]["m_PathID"].AsLong == nr) skipGo.Remove(nr);
						}
					}
				}
				else if (t == (int)AssetClassID.LODGroup)
				{
					var lods = f["m_LODs.Array"].Children;
					for (int li = 1; li < lods.Count; li++)
						foreach (var r in lods[li]["renderers.Array"].Children)
							if (r["renderer.m_PathID"].AsLong != 0) lodSkip.Add(r["renderer.m_PathID"].AsLong);
					if (lods.Count > 0)
						foreach (var r in lods[0]["renderers.Array"].Children) lodSkip.Remove(r["renderer.m_PathID"].AsLong);
				}
				else if (t == (int)AssetClassID.Transform)
				{
					foreach (var ch in f["m_Children.Array"].Children)
					{
						var co = Read(bundle, ch["m_PathID"].AsLong);
						if (co != null) Scan(co["m_GameObject.m_PathID"].AsLong);
					}
				}
			}
		}

		void RoomSkips(long goPid, bool isRootGo)
		{
			var go = Read(bundle, goPid)!;
			AssetTypeValueField? tr = null;
			foreach (var (t, _, f) in Comps(go))
			{
				if (t == (int)AssetClassID.Transform) tr = f;
				if (t != (int)AssetClassID.MonoBehaviour) continue;
				if (!isRootGo && Has(f, "m_persistent") && Has(f, "m_distant")) skipGo.Add(goPid);
				else if (Has(f, "m_chanceToSpawn") && Has(f, "m_OffObject"))
				{
					long off = f["m_OffObject.m_PathID"].AsLong;
					if (f["m_chanceToSpawn"].AsFloat >= 50) { if (off != 0) skipGo.Add(off); }
					else skipGo.Add(goPid);
				}
				else if (Has(f, "m_objects") && Has(f, "m_dungeonRequireTheme"))
				{
					foreach (var e in f["m_objects.Array"].Children.Skip(1))
					{
						long c = e["m_object.m_PathID"].AsLong;
						if (c != 0) skipGo.Add(c);
					}
				}
			}
			foreach (var ch in tr?["m_Children.Array"].Children ?? new List<AssetTypeValueField>())
			{
				var co = Read(bundle, ch["m_PathID"].AsLong);
				if (co != null) RoomSkips(co["m_GameObject.m_PathID"].AsLong, false);
			}
		}

		void Walk(long goPid, float[] parent, bool isRootGo)
		{
			if (!isRootGo && skipGo.Contains(goPid)) return;
			var go = Read(bundle, goPid)!;
			if (!isRootGo && go["m_IsActive"].AsBool == false) return;
			var cs = Comps(go).ToList();
			var tr = cs.FirstOrDefault(c => c.Type == (int)AssetClassID.Transform).F;
			if (tr == null) return;
			float[] w = isRootGo ? Mat.Identity() : Mat.Mul(parent, Mat.Trs(tr["m_LocalPosition"], tr["m_LocalRotation"], tr["m_LocalScale"]));
			var mf = cs.FirstOrDefault(c => c.Type == (int)AssetClassID.MeshFilter);
			var mr = cs.FirstOrDefault(c => c.Type == (int)AssetClassID.MeshRenderer);
			var smr = cs.FirstOrDefault(c => c.Type == (int)AssetClassID.SkinnedMeshRenderer);
			AssetTypeValueField? meshRef = mf.F?["m_Mesh"];
			if (smr.F != null && mr.F == null)
			{
				mr = smr;
				meshRef = smr.F["m_Mesh"];
			}
			if (meshRef != null && mr.F != null && mr.F["m_Enabled"].AsBool && !lodSkip.Contains(mr.Pid))
			{
				var mw = Resolve(bundle, meshRef);
				if (mw != null)
				{
					var mesh = LoadMesh(mw);
					var mats = mr.F["m_Materials.Array"].Children;
					for (int si = 0; si < mats.Count && si < mesh.SubMeshes.Count; si++)
					{
						var mat = Resolve(bundle, mats[si]) is Where m ? LoadMaterial(m) : null;
						model.Parts.Add(new Part(mesh, si, mat, w));
					}
				}
			}
			foreach (var ch in tr["m_Children.Array"].Children)
			{
				var co = Read(bundle, ch["m_PathID"].AsLong);
				if (co != null) Walk(co["m_GameObject.m_PathID"].AsLong, w, false);
			}
		}

		Scan(root.PathId);
		if (isRoom) RoomSkips(root.PathId, true);
		Walk(root.PathId, Mat.Identity(), true);
		return model;
	}

	private static readonly int[] FormatSize = { 4, 2, 1, 1, 2, 2, 1, 1, 2, 2, 4, 4 };

	public MeshData LoadMesh(Where at)
	{
		if (_meshes.TryGetValue((at.Bundle, at.PathId), out var cached)) return cached;
		var m = Read(at.Bundle, at.PathId)!;
		var vd = m["m_VertexData"];
		int n = (int)vd["m_VertexCount"].AsUInt;
		byte[] data = vd["m_DataSize"].AsByteArray;
		if (data.Length == 0 && m["m_StreamData.size"].AsUInt > 0) data = StreamBytes(at.Bundle, m["m_StreamData"]);
		var channels = vd["m_Channels.Array"].Children.Select(c => (Stream: (int)c["stream"].AsByte, Offset: (int)c["offset"].AsByte, Format: (int)c["format"].AsByte, Dim: c["dimension"].AsByte & 0xF)).ToList();
		// Streams follow one another, each 16-byte aligned; a vertex's channels in a stream are interleaved.
		int streams = channels.Where(c => c.Dim > 0).Select(c => c.Stream).DefaultIfEmpty(0).Max() + 1;
		var stride = new int[streams];
		foreach (var c in channels.Where(c => c.Dim > 0)) stride[c.Stream] = Math.Max(stride[c.Stream], c.Offset + c.Dim * FormatSize[c.Format]);
		var start = new int[streams];
		for (int s = 1; s < streams; s++) start[s] = (start[s - 1] + stride[s - 1] * n + 15) & ~15;
		float[]? Channel(int i, int want)
		{
			if (i >= channels.Count || channels[i].Dim == 0) return null;
			var c = channels[i];
			var res = new float[n * want];
			for (int v = 0; v < n; v++)
			{
				int p = start[c.Stream] + v * stride[c.Stream] + c.Offset;
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
		var mesh = new MeshData { Name = m["m_Name"].AsString, VertexCount = n, Positions = Channel(0, 3) ?? new float[n * 3], Normals = Channel(1, 3), Uv0 = Channel(4, 2) };
		if (!m["m_CompressedMesh.m_Vertices.m_NumItems"].IsDummy && m["m_CompressedMesh.m_Vertices.m_NumItems"].AsUInt > 0)
		{
			throw new NotSupportedException($"compressed mesh {mesh.Name}");
		}
		byte[] ib = m["m_IndexBuffer.Array"].AsByteArray;
		bool wide = m["m_IndexFormat"].AsInt == 1;
		foreach (var sm in m["m_SubMeshes.Array"].Children)
		{
			int first = (int)sm["firstByte"].AsUInt, count = (int)sm["indexCount"].AsUInt, baseV = (int)sm["baseVertex"].AsUInt;
			var idx = new int[count];
			for (int k = 0; k < count; k++) idx[k] = baseV + (wide ? (int)BitConverter.ToUInt32(ib, first + 4 * k) : BitConverter.ToUInt16(ib, first + 2 * k));
			mesh.SubMeshes.Add(idx);
		}
		_meshes[(at.Bundle, at.PathId)] = mesh;
		return mesh;
	}

	public MaterialData LoadMaterial(Where at)
	{
		if (_materials.TryGetValue((at.Bundle, at.PathId), out var cached)) return cached;
		var m = Read(at.Bundle, at.PathId)!;
		var mat = new MaterialData { Name = m["m_Name"].AsString };
		if (Resolve(at.Bundle, m["m_Shader"]) is Where sh && Read(sh.Bundle, sh.PathId) is { } shf && !shf["m_ParsedForm.m_Name"].IsDummy) mat.Shader = shf["m_ParsedForm.m_Name"].AsString;
		foreach (var te in m["m_SavedProperties.m_TexEnvs.Array"].Children)
		{
			string prop = te["first"].AsString;
			if (prop is not ("_MainTex" or "_BumpMap")) continue;
			if (Resolve(at.Bundle, te["second.m_Texture"]) is not Where tw || Read(tw.Bundle, tw.PathId) is not { } t) continue;
			mat.Textures[prop] = new TextureRef(t["m_Name"].AsString, t["m_TextureFormat"].AsInt, t["m_Width"].AsInt, t["m_Height"].AsInt, t["m_MipCount"].AsInt, t["m_StreamData.size"].AsUInt);
		}
		_materials[(at.Bundle, at.PathId)] = mat;
		return mat;
	}
}

public static class Mat
{
	public static float[] Identity() => new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };

	// Row-major 4×4.
	public static float[] Mul(float[] a, float[] b)
	{
		var r = new float[16];
		for (int i = 0; i < 4; i++)
			for (int j = 0; j < 4; j++)
				for (int k = 0; k < 4; k++) r[i * 4 + j] += a[i * 4 + k] * b[k * 4 + j];
		return r;
	}

	public static float[] Trs(AssetTypeValueField p, AssetTypeValueField q, AssetTypeValueField s)
	{
		float x = q["x"].AsFloat, y = q["y"].AsFloat, z = q["z"].AsFloat, w = q["w"].AsFloat;
		float sx = s["x"].AsFloat, sy = s["y"].AsFloat, sz = s["z"].AsFloat;
		return new[]
		{
			(1 - 2 * (y * y + z * z)) * sx, 2 * (x * y - z * w) * sy, 2 * (x * z + y * w) * sz, p["x"].AsFloat,
			2 * (x * y + z * w) * sx, (1 - 2 * (x * x + z * z)) * sy, 2 * (y * z - x * w) * sz, p["y"].AsFloat,
			2 * (x * z - y * w) * sx, 2 * (y * z + x * w) * sy, (1 - 2 * (x * x + y * y)) * sz, p["z"].AsFloat,
			0, 0, 0, 1,
		};
	}
}



public partial record TextureRef
{
	public static string FormatName(int f) => f switch
	{
		3 => "RGB24", 4 => "RGBA32", 5 => "ARGB32", 10 => "DXT1", 12 => "DXT5", 25 => "BC7", 24 => "BC6H", 26 => "BC4", 27 => "BC5",
		28 => "DXT1Crunched", 29 => "DXT5Crunched", 1 => "Alpha8", 63 => "R8", _ => f.ToString(),
	};
}
