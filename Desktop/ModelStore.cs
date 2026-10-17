using System.Collections.Concurrent;
using System.Numerics;
using System.Text.Json.Nodes;
using SkiaSharp;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The game's models: read from the game's own files (BundleModels, from Valheim's asset bundles), or from files in the
/// exporter's layout (Files: a folder with pieces/&lt;name&gt;.json listing a kind's parts (mesh, submesh, material,
/// matrix in the view's right-handed space), meshes/&lt;id&gt;.bin holding interleaved position, normal, uv (float32, 8
/// per vertex) then the uint32 indices of each submesh, materials.json the colours and textures (tex/); what the tests
/// give it). Thread-safe: models are read on worker threads.
/// </summary>
public sealed class ModelStore
{
    /// <summary>
    /// Where the models come from. Meshes come with Unity's winding (the store turns it around).
    /// </summary>
    internal interface ISource
    {
        /// <summary>
        /// A world object's model name (objects with a model of their own: not pieces, creatures, items).
        /// </summary>
        /// <returns>The model name, or null when the prefab has none of its own.</returns>
        string? ObjectName(int prefab);

        /// <summary>
        /// A model by name, or null when there is none.
        /// </summary>
        Model? Model(string name);

        /// <summary>
        /// A mesh by id, or null when there is none; Unity's winding.
        /// </summary>
        MeshData? Mesh(string id);

        /// <summary>
        /// A material by id.
        /// </summary>
        MaterialData Material(string id);

        /// <summary>
        /// A texture by id, or null when it cannot be read.
        /// </summary>
        ImageData? Texture(string id);
    }

    /// <summary>
    /// A model with no root scale: as it is.
    /// </summary>
    private static readonly float[] UnitScale = [1f, 1f, 1f];

    /// <summary>
    /// One part of a model: a submesh drawn with a material.
    /// </summary>
    /// <param name="Mesh">The mesh's id.</param>
    /// <param name="Sub">Which of the mesh's submeshes.</param>
    /// <param name="Material">The material's id.</param>
    /// <param name="Matrix">The part's placement in the model, in the view's right-handed space.</param>
    public sealed record Part(string Mesh, int Sub, string Material, Matrix4x4 Matrix);
    /// <summary>
    /// A kind's model: its parts and its root's scale.
    /// </summary>
    /// <param name="Name">The model name it was asked by.</param>
    /// <param name="Parts">Its parts, never empty when loaded through the store.</param>
    /// <param name="RootScale">The prefab root's scale; used when a placed object has no scale of its own.</param>
    public sealed record Model(string Name, List<Part> Parts, Vector3 RootScale);
    /// <summary>
    /// A mesh's vertices and triangles.
    /// </summary>
    /// <param name="Vertices">Interleaved position, normal, uv: 8 floats per vertex.</param>
    /// <param name="Submeshes">Each submesh's triangle indices, three per triangle.</param>
    public sealed record MeshData(float[] Vertices, uint[][] Submeshes)
    {
        /// <summary>
        /// Every triangle is there wound both ways (Unity's primitives, made to show whatever their material culls):
        /// drawn with back faces culled, it still shows from both sides, each side once (twice, a see-through material
        /// would look twice as thick).
        /// </summary>
        public bool BothWays { get; init; }

        /// <summary>
        /// The mesh's box (its vertices' smallest and largest coordinates).
        /// </summary>
        public (Vector3 Min, Vector3 Max) Bounds { get; } = BoundsOf(Vertices);

        /// <summary>
        /// The smallest and largest vertex positions; zero for a mesh without a whole vertex.
        /// </summary>
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
    /// <summary>
    /// How a part is coloured.
    /// </summary>
    /// <param name="Color">RGBA, the colour in linear space (alpha as is).</param>
    /// <param name="Map">The texture's id, or null for none.</param>
    /// <param name="Cutoff">Alpha below which pixels are dropped; 0 draws them all.</param>
    /// <param name="DoubleSided">Draw both faces.</param>
    /// <param name="UvTransform">Texture scale (x, y) then offset (z, w), applied as uv * scale + offset.</param>
    /// <param name="Transparent">See-through (glass, water): blended with what is behind it by its alpha (the colour's
    /// times the texture's), after the opaque objects; false: opaque, or cut out at <paramref name="Cutoff"/>.</param>
    public sealed record MaterialData(Vector4 Color, string? Map, float Cutoff, bool DoubleSided, Vector4 UvTransform, bool Transparent = false);
    /// <summary>
    /// A texture's pixels.
    /// </summary>
    /// <param name="Width">In pixels.</param>
    /// <param name="Height">In pixels.</param>
    /// <param name="Rgba">Four bytes per pixel, row by row.</param>
    public sealed record ImageData(int Width, int Height, byte[] Rgba);

    /// <summary>
    /// Where models, meshes, materials and textures are read from.
    /// </summary>
    private readonly ISource _source;
    /// <summary>
    /// Models read so far by name, null for those that have none (asked once).
    /// </summary>
    private readonly ConcurrentDictionary<string, Model?> _models = new();
    /// <summary>
    /// Meshes read so far by id; lazy so two threads asking at once read a mesh once.
    /// </summary>
    private readonly ConcurrentDictionary<string, Lazy<MeshData?>> _meshes = new();
    /// <summary>
    /// Each mesh's box, kept when its vertices are let go (Forget).
    /// </summary>
    private readonly ConcurrentDictionary<string, (Vector3 Min, Vector3 Max)> _bounds = new();

    /// <summary>
    /// Models from files in the exporter's layout (the tests' small sets).
    /// </summary>
    /// <param name="root">The folder holding pieces/, meshes/, tex/, meshinfo.json, materials.json and
    /// objects.json.</param>
    internal ModelStore(string root) : this(new Files(root))
    {
    }

    /// <summary>
    /// Models from any source (the game's bundles, or files).
    /// </summary>
    internal ModelStore(ISource source)
    {
        _source = source;
    }

    /// <summary>
    /// The game's models, read from the Valheim game folder's bundles; null when the game is not found. Run off the UI
    /// thread: the first time the bundles are indexed (a few seconds; kept in a file after).
    /// </summary>
    public static ModelStore? Open() => BundleModels.For(GameLook.Bundles) is { } game ? new ModelStore(game) : null;

    /// <summary>
    /// A world object's model name, else a dungeon room's (their models are under the room's prefab name, like pieces),
    /// else the prefab's.
    /// </summary>
    public string? NameOf(int prefab) => _source.ObjectName(prefab) ?? Editing.Dungeons.RoomOf(prefab)?.Name ?? PrefabCatalog.DisplayName(prefab);

    /// <summary>
    /// A model by name, read once and kept; null when there is none, it has no parts or it could not be read.
    /// </summary>
    public Model? LoadModel(string name) => _models.GetOrAdd(name, n =>
    {
        try
        {
            return _source.Model(n) is { Parts.Count: > 0 } m ? m : null;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Brief: when a bundle is damaged, each of its models fails.
            Log.CaughtBrief(e, $"reading the model {n}");
            return null;
        }
    });

    /// <summary>
    /// A mesh by id, read once (with its winding turned around for the right-handed view) and kept until <c>Forget</c>;
    /// null when there is none or it could not be read.
    /// </summary>
    public MeshData? LoadMesh(string id) => _meshes.GetOrAdd(id, i => new Lazy<MeshData?>(() =>
    {
        MeshData? md;
        try
        {
            md = _source.Mesh(i);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.CaughtBrief(e, $"reading the mesh {i}");
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

    /// <summary>
    /// A mesh's box, if it was read before (null: read it with LoadMesh).
    /// </summary>
    public (Vector3 Min, Vector3 Max)? BoundsOf(string id) => _bounds.TryGetValue(id, out var b) ? b : null;

    /// <summary>
    /// The mesh's vertices are not needed any more (they are on the graphics card): let go, its box kept. Read again if
    /// asked again.
    /// </summary>
    public void Forget(string id) => _meshes.TryRemove(id, out _);

    /// <summary>
    /// Colours are sRGB; the shader lights in linear colours. One sRGB channel (0..1) to linear, by a 2.2 gamma.
    /// </summary>
    internal static float Linear(float v) => MathF.Pow(v, 2.2f);

    /// <summary>
    /// A material by id; plain white and opaque when it could not be read.
    /// </summary>
    public MaterialData Material(string id)
    {
        try
        {
            return _source.Material(id);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.CaughtBrief(e, $"reading the material {id}");
            return new MaterialData(Vector4.One, null, 0, false, new Vector4(1, 1, 0, 0));
        }
    }

    /// <summary>
    /// A texture's pixels, RGBA, the bottom row first (as Unity keeps them); null when it could not be read.
    /// </summary>
    public ImageData? LoadTexture(string id)
    {
        try
        {
            return _source.Texture(id);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.CaughtBrief(e, $"reading the texture {id}");
            return null;
        }
    }

    /// <summary>
    /// Files in the exporter's layout (see the top).
    /// </summary>
    private sealed class Files : ISource
    {
        /// <summary>
        /// The folder the files are in.
        /// </summary>
        private readonly string _root;
        /// <summary>
        /// Object model names by prefab hash, from objects.json.
        /// </summary>
        private readonly Dictionary<int, string> _names = [];
        /// <summary>
        /// meshinfo.json (each mesh's vertex count and submesh index counts) and materials.json.
        /// </summary>
        private readonly JsonObject _meshInfo, _materials;

        /// <summary>
        /// Reads the index files; meshes, pieces and textures are read when asked.
        /// </summary>
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

        /// <inheritdoc/>
        public string? ObjectName(int prefab) => _names.TryGetValue(prefab, out string? n) ? n : null;

        /// <inheritdoc/>
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
                float[] m = [.. p!["m"]!.AsArray().Select(v => v!.GetValue<float>())];
                // three.js keeps matrices column by column; read in order, they are System.Numerics' rows
                // (row vectors), the same transform.
                var mat = new Matrix4x4(m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8], m[9], m[10], m[11], m[12], m[13], m[14], m[15]);
                parts.Add(new Part(p["mesh"]!.GetValue<string>(), p["sub"]!.GetValue<int>(), p["mat"]!.GetValue<string>(), mat));
            }
            float[] rs = doc["rootScale"]?.AsArray().Select(v => v!.GetValue<float>()).ToArray() ?? UnitScale;
            return new Model(name, parts, new Vector3(rs[0], rs[1], rs[2]));
        }

        /// <inheritdoc/>
        public MeshData? Mesh(string id)
        {
            if (_meshInfo[id] is not JsonObject info)
            {
                return null;
            }
            int v = info["v"]!.GetValue<int>();
            int[] subs = [.. info["sub"]!.AsArray().Select(c => c!.GetValue<int>())];
            byte[] bytes = File.ReadAllBytes(Path.Combine(_root, "meshes", id + ".bin"));
            float[] verts = new float[v * 8];
            Buffer.BlockCopy(bytes, 0, verts, 0, v * 32);
            int off = v * 32;
            uint[][] indices = new uint[subs.Length][];
            for (int s = 0; s < subs.Length; s++)
            {
                indices[s] = new uint[subs[s]];
                Buffer.BlockCopy(bytes, off, indices[s], 0, subs[s] * 4);
                off += subs[s] * 4;
            }
            return new MeshData(verts, indices);
        }

        /// <inheritdoc/>
        public MaterialData Material(string id)
        {
            var m = _materials[id] as JsonObject;
            float[] c = m?["color"]?.AsArray().Select(x => x!.GetValue<float>()).ToArray() ?? [1f, 1f, 1f, 1f];
            float cutoff = m?["cutoff"]?.GetValue<float>() ?? 0f;
            float[] uv = m?["uv"]?.AsArray().Select(x => x!.GetValue<float>()).ToArray() ?? [1f, 1f, 0f, 0f];
            return new MaterialData(new Vector4(Linear(c[0]), Linear(c[1]), Linear(c[2]), c.Length > 3 ? c[3] : 1f), m?["map"]?.GetValue<string>(),
                cutoff, m?["doubleSided"]?.GetValue<bool>() == true, new Vector4(uv[0], uv[1], uv[2], uv[3]), m?["transparent"]?.GetValue<bool>() == true);
        }

        /// <summary>
        /// A texture file under tex/, decoded as stored; null when it cannot be read.
        /// </summary>
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
            catch (Exception e)
            {
                Log.CaughtBrief(e, "decoding an exported texture file");
                return null;
            }
        }
    }
}
