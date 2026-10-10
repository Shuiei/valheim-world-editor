using System.Collections.Concurrent;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkiaSharp;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// The game's models: read from the game's own files (BundleModels, from Valheim's asset bundles), or
// from files in the exporter's layout (Files: a folder with pieces/<name>.json listing a kind's parts
// (mesh, submesh, material, matrix in the view's right-handed space), meshes/<id>.bin holding
// interleaved position, normal, uv (float32, 8 per vertex) then the uint32 indices of each submesh,
// materials.json the colours and textures (tex/); what the tests give it). Thread-safe: models are read
// on worker threads.
public sealed class ModelStore
{
	// Where the models come from. Meshes come with Unity's winding (the store turns it around).
	internal interface ISource
	{
		// A world object's model name (objects with a model of their own: not pieces, creatures, items).
		string? ObjectName(int prefab);

		Model? Model(string name);

		MeshData? Mesh(string id);

		MaterialData Material(string id);

		ImageData? Texture(string id);
	}

	// A model with no root scale: as it is.
	private static readonly float[] UnitScale = { 1f, 1f, 1f };

	public sealed record Part(string Mesh, int Sub, string Material, Matrix4x4 Matrix);
	public sealed record Model(string Name, List<Part> Parts, Vector3 RootScale);
	public sealed record MeshData(float[] Vertices, uint[][] Submeshes)
	{
		// The mesh's box (its vertices' smallest and largest coordinates).
		public (Vector3 Min, Vector3 Max) Bounds { get; } = BoundsOf(Vertices);

		private static (Vector3, Vector3) BoundsOf(float[] v)
		{
			if (v.Length < 8)
			{
				return (Vector3.Zero, Vector3.Zero);
			}
			Vector3 min = new(float.MaxValue), max = new(float.MinValue);
			for (int i = 0; i + 2 < v.Length; i += 8)
			{
				var p = new Vector3(v[i], v[i + 1], v[i + 2]);
				min = Vector3.Min(min, p);
				max = Vector3.Max(max, p);
			}
			return (min, max);
		}
	}
	public sealed record MaterialData(Vector4 Color, string? Map, float Cutoff, bool DoubleSided, Vector4 UvTransform);
	public sealed record ImageData(int Width, int Height, byte[] Rgba);

	private readonly ISource _source;
	private readonly ConcurrentDictionary<string, Model?> _models = new();
	private readonly ConcurrentDictionary<string, Lazy<MeshData?>> _meshes = new();
	// Each mesh's box, kept when its vertices are let go (Forget).
	private readonly ConcurrentDictionary<string, (Vector3 Min, Vector3 Max)> _bounds = new();

	// Models from files in the exporter's layout (the tests' small sets).
	internal ModelStore(string root) : this(new Files(root))
	{
	}

	internal ModelStore(ISource source)
	{
		_source = source;
	}

	// The game's models, read from the Valheim game folder's bundles; null when the game is not found.
	// Run off the UI thread: the first time the bundles are indexed (a few seconds; kept in a file after).
	public static ModelStore? Open() => BundleModels.For(GameLook.Bundles) is { } game ? new ModelStore(game) : null;

	// A world object's model name, else a dungeon room's (their models are under the room's prefab name,
	// like pieces), else the prefab's.
	public string? NameOf(int prefab) => _source.ObjectName(prefab) ?? TerrainEditor.Editing.Dungeons.RoomOf(prefab)?.Name ?? PrefabCatalog.DisplayName(prefab);

	public Model? LoadModel(string name) => _models.GetOrAdd(name, n =>
	{
		try
		{
			return _source.Model(n) is { Parts.Count: > 0 } m ? m : null;
		}
		catch (Exception e) when (e is not OutOfMemoryException)
		{
			Console.WriteLine($"model {n} could not be read: {e.Message}");
			return null;
		}
	});

	public MeshData? LoadMesh(string id) => _meshes.GetOrAdd(id, i => new Lazy<MeshData?>(() =>
	{
		MeshData? md;
		try
		{
			md = _source.Mesh(i);
		}
		catch (Exception e) when (e is not OutOfMemoryException)
		{
			Console.WriteLine($"mesh {i} could not be read: {e.Message}");
			return null;
		}
		if (md == null)
		{
			return null;
		}
		// Mirroring z (left- to right-handed) turns Unity's front faces into back faces: swap two corners.
		foreach (uint[] idx in md.Submeshes)
		{
			for (int t = 0; t + 2 < idx.Length; t += 3)
			{
				(idx[t + 1], idx[t + 2]) = (idx[t + 2], idx[t + 1]);
			}
		}
		_bounds[i] = md.Bounds;
		return md;
	})).Value;

	// A mesh's box, if it was read before (null: read it with LoadMesh).
	public (Vector3 Min, Vector3 Max)? BoundsOf(string id) => _bounds.TryGetValue(id, out var b) ? b : null;

	// The mesh's vertices are not needed any more (they are on the graphics card): let go, its box kept.
	// Read again if asked again.
	public void Forget(string id) => _meshes.TryRemove(id, out _);

	// Colours are sRGB; the shader lights in linear colours.
	internal static float Linear(float v) => MathF.Pow(v, 2.2f);

	public MaterialData Material(string id)
	{
		try
		{
			return _source.Material(id);
		}
		catch (Exception e) when (e is not OutOfMemoryException)
		{
			Console.WriteLine($"material {id} could not be read: {e.Message}");
			return new MaterialData(Vector4.One, null, 0, false, new Vector4(1, 1, 0, 0));
		}
	}

	// A texture's pixels, RGBA, the bottom row first (as Unity keeps them).
	public ImageData? LoadTexture(string id)
	{
		try
		{
			return _source.Texture(id);
		}
		catch (Exception e) when (e is not OutOfMemoryException)
		{
			Console.WriteLine($"texture {id} could not be read: {e.Message}");
			return null;
		}
	}

	// Files in the exporter's layout (see the top).
	private sealed class Files : ISource
	{
		private readonly string _root;
		private readonly Dictionary<int, string> _names = new();
		private readonly JsonObject _meshInfo, _materials;

		public Files(string root)
		{
			_root = root;
			_meshInfo = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "meshinfo.json")))!.AsObject();
			_materials = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "materials.json")))!.AsObject();
			foreach (var (k, v) in JsonNode.Parse(File.ReadAllText(Path.Combine(root, "objects.json")))!.AsObject())
			{
				if (int.TryParse(k, out int hash) && v?.GetValue<string>() is string n)
				{
					_names[hash] = n;
				}
			}
		}

		public string? ObjectName(int prefab) => _names.TryGetValue(prefab, out string? n) ? n : null;

		public Model? Model(string name)
		{
			string file = Path.Combine(_root, "pieces", name + ".json");
			if (!File.Exists(file))
			{
				return null;
			}
			var doc = JsonNode.Parse(File.ReadAllText(file))!;
			var parts = new List<Part>();
			foreach (var p in doc["parts"]!.AsArray())
			{
				var m = p!["m"]!.AsArray().Select(v => v!.GetValue<float>()).ToArray();
				// three.js keeps matrices column by column; read in order, they are System.Numerics' rows
				// (row vectors), the same transform.
				var mat = new Matrix4x4(m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8], m[9], m[10], m[11], m[12], m[13], m[14], m[15]);
				parts.Add(new Part(p["mesh"]!.GetValue<string>(), p["sub"]!.GetValue<int>(), p["mat"]!.GetValue<string>(), mat));
			}
			var rs = doc["rootScale"]?.AsArray().Select(v => v!.GetValue<float>()).ToArray() ?? UnitScale;
			return new Model(name, parts, new Vector3(rs[0], rs[1], rs[2]));
		}

		public MeshData? Mesh(string id)
		{
			if (_meshInfo[id] is not JsonObject info)
			{
				return null;
			}
			int v = info["v"]!.GetValue<int>();
			int[] subs = info["sub"]!.AsArray().Select(c => c!.GetValue<int>()).ToArray();
			byte[] bytes = File.ReadAllBytes(Path.Combine(_root, "meshes", id + ".bin"));
			float[] verts = new float[v * 8];
			Buffer.BlockCopy(bytes, 0, verts, 0, v * 32);
			int off = v * 32;
			var indices = new uint[subs.Length][];
			for (int s = 0; s < subs.Length; s++)
			{
				indices[s] = new uint[subs[s]];
				Buffer.BlockCopy(bytes, off, indices[s], 0, subs[s] * 4);
				off += subs[s] * 4;
			}
			return new MeshData(verts, indices);
		}

		public MaterialData Material(string id)
		{
			var m = _materials[id] as JsonObject;
			var c = m?["color"]?.AsArray().Select(x => x!.GetValue<float>()).ToArray() ?? new[] { 1f, 1f, 1f, 1f };
			float cutoff = m?["cutoff"]?.GetValue<float>() ?? (m?["transparent"]?.GetValue<bool>() == true ? 0.5f : 0f);
			var uv = m?["uv"]?.AsArray().Select(x => x!.GetValue<float>()).ToArray() ?? new[] { 1f, 1f, 0f, 0f };
			return new MaterialData(new Vector4(Linear(c[0]), Linear(c[1]), Linear(c[2]), c.Length > 3 ? c[3] : 1f), m?["map"]?.GetValue<string>(),
				cutoff, m?["doubleSided"]?.GetValue<bool>() == true, new Vector4(uv[0], uv[1], uv[2], uv[3]));
		}

		public ImageData? Texture(string file)
		{
			string path = Path.Combine(_root, "tex", file);
			try
			{
				using var codec = SKCodec.Create(path);
				if (codec == null)
				{
					return null;
				}
				var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
				byte[] rgba = new byte[info.BytesSize];
				unsafe
				{
					fixed (byte* p = rgba)
					{
						if (codec.GetPixels(info, (IntPtr)p) is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
						{
							return null;
						}
					}
				}
				return new ImageData(info.Width, info.Height, rgba);
			}
			catch (Exception)
			{
				return null;
			}
		}
	}
}
