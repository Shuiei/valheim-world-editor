using System.Text.Json;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// The game's own files, read where Valheim keeps them (valheim_Data/StreamingAssets/SoftRef/Bundles):
/// models (their parts, meshes, materials) and textures, straight from the asset bundles, with nothing
/// copied. The bundles are Unity's (UnityFS, LZ4 blocks) and carry their type trees, so every field
/// reads by name (AssetsTools.NET). Which bundle holds which prefab comes from each bundle's table of
/// contents (its AssetBundle's m_Container): read once (a few seconds), kept in a file, read again when
/// the game's bundles change. Thread-safe: one reader at a time.
/// </summary>
public sealed class GameBundles
{
    /// <summary>
    /// Where an object is: a bundle and the object's path id in that bundle's assets file.
    /// </summary>
    /// <param name="Bundle">The bundle's file name in the bundles folder.</param>
    /// <param name="PathId">The object's m_PathID in the bundle.</param>
    public sealed record Where(string Bundle, long PathId)
    {
        /// <summary>
        /// "bundle:pathId", as kept in the index file.
        /// </summary>
        public string Key => $"{Bundle}:{PathId}";

        /// <summary>
        /// The reverse of <c>Key</c>; null when the text is not one.
        /// </summary>
        public static Where? FromKey(string key)
        {
            int c = key.LastIndexOf(':');
            return c > 0 && long.TryParse(key.AsSpan(c + 1), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out long pid) ? new Where(key[..c], pid) : null;
        }
    }

    /// <summary>
    /// A model's part: a submesh of a mesh with a material, placed by a matrix (row-major 4 x 4, Unity's left-handed
    /// space, relative to the prefab's root).
    /// </summary>
    /// <param name="Mesh">The mesh.</param>
    /// <param name="Sub">The submesh's index in it.</param>
    /// <param name="Material">The renderer's material for that submesh, null when it has none that resolves.</param>
    /// <param name="Matrix">The part's placement (see <c>Matrix</c>).</param>
    public sealed record Part(Where Mesh, int Sub, Where? Material, float[] Matrix);

    /// <summary>
    /// A prefab's model as <c>LoadModel</c> reads it.
    /// </summary>
    /// <param name="Name">The prefab name.</param>
    /// <param name="Parts">What is drawn, relative to the root.</param>
    /// <param name="RootScale">The root's own local scale (x, y, z), left out of the parts' matrices.</param>
    public sealed record Model(string Name, List<Part> Parts, float[] RootScale);

    /// <summary>
    /// A mesh's vertices and triangles, as the game has them (left-handed).
    /// </summary>
    /// <param name="Name">The mesh's name.</param>
    /// <param name="VertexCount">How many vertices there are.</param>
    /// <param name="Positions">Three floats per vertex.</param>
    /// <param name="Normals">Three floats per vertex, null when the mesh has none.</param>
    /// <param name="Uv">Two floats per vertex (the first UV channel), null when the mesh has none.</param>
    /// <param name="Submeshes">Per submesh, its triangles' vertex indices (base vertex added); empty for lines or
    /// points.</param>
    public sealed record Mesh(string Name, int VertexCount, float[] Positions, float[]? Normals, float[]? Uv, int[][] Submeshes);

    /// <summary>
    /// One texture slot of a material.
    /// </summary>
    /// <param name="Texture">The texture, null when the slot is empty.</param>
    /// <param name="ScaleX">The UV tiling along x.</param>
    /// <param name="ScaleY">The UV tiling along y.</param>
    /// <param name="OffsetX">The UV offset along x.</param>
    /// <param name="OffsetY">The UV offset along y.</param>
    public sealed record TexEnv(Where? Texture, float ScaleX, float ScaleY, float OffsetX, float OffsetY);

    /// <summary>
    /// A material's saved properties.
    /// </summary>
    /// <param name="Name">The material's name.</param>
    /// <param name="Shader">The shader's name (its parsed form's), empty when it cannot be read.</param>
    /// <param name="ShaderAt">Where the shader is, for <c>LoadShader</c>.</param>
    /// <param name="Textures">Texture slots by property name (_MainTex...).</param>
    /// <param name="Floats">Float properties by name.</param>
    /// <param name="Colors">Colour properties by name, as r, g, b, a.</param>
    public sealed record Material(string Name, string Shader, Where? ShaderAt, Dictionary<string, TexEnv> Textures, Dictionary<string, float> Floats, Dictionary<string, float[]> Colors);

    /// <summary>
    /// One mip level of a texture, as stored (bottom row first).
    /// </summary>
    /// <param name="Name">The texture's name ("name[i]" for slice i of a texture array).</param>
    /// <param name="Format">Unity's TextureFormat (see <c>FormatLayout</c>).</param>
    /// <param name="Width">The level's width in pixels.</param>
    /// <param name="Height">The level's height in pixels.</param>
    /// <param name="Data">The level's bytes, <c>LevelSize</c> of them.</param>
    public sealed record TextureLevel(string Name, int Format, int Width, int Height, byte[] Data);

    /// <summary>
    /// What a prefab's root has.
    /// </summary>
    /// <param name="Networked">A ZNetView: a saved, networked object.</param>
    /// <param name="Creature">A Character: a creature.</param>
    /// <param name="Item">An ItemDrop: an item.</param>
    /// <param name="Location">A Location: a place the world generator puts (a ruin, a camp, an altar...).</param>
    public sealed record Kind(bool Networked, bool Creature, bool Item, bool Location = false);

    /// <summary>
    /// The lock all readers take: one reader at a time.
    /// </summary>
    private readonly Lock _sync = new();
    /// <summary>
    /// AssetsTools.NET's manager, which holds the open bundles and their type trees.
    /// </summary>
    private readonly AssetsManager _am;
    /// <summary>
    /// Where the index is kept (null: not kept).
    /// </summary>
    private readonly string? _cacheFile;
    /// <summary>
    /// Bundle file name -&gt; its path.
    /// </summary>
    private readonly Dictionary<string, string> _files = [];
    /// <summary>
    /// Unity's CAB name of each bundle's assets file -&gt; the bundle's file name, to follow references into other
    /// bundles.
    /// </summary>
    private readonly Dictionary<string, string> _cab = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Bundles kept open after being read once, by file name.
    /// </summary>
    private readonly Dictionary<string, (BundleFileInstance Bundle, AssetsFileInstance Assets)> _open = [];
    /// <summary>
    /// Objects read for the model being read (cleared after: the whole game would not fit).
    /// </summary>
    private readonly Dictionary<(string, long), AssetTypeValueField?> _fields = [];
    /// <summary>
    /// <c>KindOf</c>'s answers, by prefab name.
    /// </summary>
    private readonly Dictionary<string, Kind> _kinds = [];
    /// <summary>
    /// Prefab name -&gt; its root object; null until built or read from the kept file.
    /// </summary>
    private Dictionary<string, Where>? _index;
    /// <summary>
    /// <c>NamedAssets</c> found, by file name.
    /// </summary>
    private Dictionary<string, Where> _assets = [];

    /// <summary>
    /// Assets found by their file name in the bundles' tables of contents (besides prefabs): the
    /// terrain's material and texture arrays, the world map's material.
    /// </summary>
    public static readonly string[] NamedAssets = ["Heightmap_basematerial.mat", "terrain_d_array.texture2darray", "terrain_n_array.texture2darray", "minimap.mat"];
    /// <summary>
    /// The index's prefab names by stable hash (the first name when two share one).
    /// </summary>
    private Dictionary<int, string>? _byHash;

    /// <summary>
    /// A reader of a bundles folder; nothing is opened until asked for.
    /// </summary>
    /// <param name="dir">The bundles folder.</param>
    /// <param name="cacheFile">Where the index is kept (null: not kept).</param>
    /// <param name="mixedVersions">The bundles were built by different Unity versions (the mods' bundles): the layout
    /// of each kind of object is then read from each bundle, not kept from the first one read (AssetsTools keeps it by
    /// kind alone, and another version's layout misreads the object).</param>
    public GameBundles(string dir, string? cacheFile, bool mixedVersions = false)
    {
        _am = new() { UseTemplateFieldCache = !mixedVersions, UseRefTypeManagerCache = true, UseQuickLookup = true };
        Folder = dir;
        _cacheFile = cacheFile;
        foreach (string f in Directory.GetFiles(dir))
        {
            _files[Path.GetFileName(f)] = f;
        }
    }

    /// <summary>
    /// A reader of the bundles folder of a Valheim game folder, or null when there is no game folder or no bundles
    /// folder in it.
    /// </summary>
    public static GameBundles? ForGame(string? valheim, string? cacheFile)
    {
        string? dir = valheim == null ? null : GameLook.BundlesDir(valheim);
        return dir == null ? null : new GameBundles(dir, cacheFile);
    }

    /// <summary>
    /// How many files the bundles folder holds.
    /// </summary>
    public int BundleCount => _files.Count;

    /// <summary>
    /// The bundles folder read.
    /// </summary>
    public string Folder { get; }

    /// <summary>
    /// Whether the index is there already (built or read from the kept file); false: asking for it reads
    /// it (a few seconds when it must be built).
    /// </summary>
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

    /// <summary>
    /// Prefab name -&gt; its root object (built or read from the cache on first use).
    /// </summary>
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

    /// <summary>
    /// One of NamedAssets, if the game has it.
    /// </summary>
    public Where? Asset(string fileName)
    {
        lock (_sync)
        {
            EnsureIndex();
            return _assets.GetValueOrDefault(fileName);
        }
    }

    /// <summary>
    /// The prefab of a stable hash (the saves' prefab ids), if the game has it.
    /// </summary>
    public string? NameOf(int hash)
    {
        lock (_sync)
        {
            EnsureIndex();
            return _byHash!.TryGetValue(hash, out string? n) ? n : null;
        }
    }

    /// <summary>
    /// What the prefab's root has (cached per name); null when the game has no such prefab.
    /// </summary>
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
                bool znv = false, creature = false, item = false, location = false;
                // A root that cannot be read (its bundle without type trees): nothing known of it.
                foreach (var (t, _, f) in Read(at.Bundle, at.PathId) is { } root ? Components(at.Bundle, root) : [])
                {
                    if (t != (int)AssetClassID.MonoBehaviour)
                    {
                        continue;
                    }
                    znv |= Has(f, "m_persistent") && Has(f, "m_distant") && Has(f, "m_type");
                    creature |= Has(f, "m_runSpeed");
                    item |= Has(f, "m_itemData");
                    location |= Has(f, "m_exteriorRadius");
                }
                return _kinds[name] = new Kind(znv, creature, item, location);
            }
            finally
            {
                _fields.Clear();
            }
        }
    }

    // ---- The index.

    /// <summary>
    /// The kept index's shape (the file is written from an anonymous object with an Assets map too).
    /// </summary>
    private sealed record IndexFile(List<object[]> Bundles, Dictionary<string, string> Prefabs);

    /// <summary>
    /// The bundles as [name, size, last write (Unix seconds)], ordered by name: the kept index is used only while they
    /// match.
    /// </summary>
    private List<object[]> BundlesNow() => [.. _files.Keys.Order(StringComparer.Ordinal).Select(n =>
    {
        var fi = new FileInfo(_files[n]);
        return new object[] { n, fi.Length, new DateTimeOffset(fi.LastWriteTimeUtc).ToUnixTimeSeconds() };
    })];

    /// <summary>
    /// Makes sure the index is there (see <c>Index</c>), telling how far building it is: bundles done, of all of
    /// them. Nothing is told when it is there already or read from the kept file.
    /// </summary>
    public void EnsureIndexed(Action<int, int>? progress)
    {
        lock (_sync)
        {
            EnsureIndex(progress);
        }
    }

    /// <summary>
    /// The index: already there, read from the kept file when the bundles have not changed, else built (and kept).
    /// </summary>
    /// <param name="progress">Told the bundles done, of all of them, while it is built.</param>
    private Dictionary<string, Where> EnsureIndex(Action<int, int>? progress = null)
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
        var index = BuildIndex(progress);
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

    /// <summary>
    /// Sets the index and its by-hash map.
    /// </summary>
    private Dictionary<string, Where> SetIndex(Dictionary<string, Where> index)
    {
        _byHash = [];
        foreach (string n in index.Keys)
        {
            _byHash.TryAdd(StableHash.Of(n), n);
        }
        return _index = index;
    }

    /// <summary>
    /// Prefab name -&gt; its root, from each bundle's table of contents (prefabs only). A name in several
    /// bundles (a model asset and the prefab made from it): the one with a ZNetView, the prefab the game
    /// spawns.
    /// </summary>
    /// <param name="progress">Told the bundles done, of all of them, before each.</param>
    private Dictionary<string, Where> BuildIndex(Action<int, int>? progress)
    {
        var res = new Dictionary<string, Where>();
        var twice = new Dictionary<string, List<Where>>();
        var assets = new Dictionary<string, Where>();
        int done = 0;
        foreach (string bundle in _files.Keys.Order(StringComparer.Ordinal))
        {
            progress?.Invoke(done++, _files.Count);
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
            // Without type trees its table of contents is not described either: nothing listed.
            var ab = a.file.Metadata.TypeTreeEnabled ? a.file.GetAssetsOfType(AssetClassID.AssetBundle).FirstOrDefault() : null;
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
                            twice[n] = l = [res[n]];
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

    /// <summary>
    /// Whether an object has a ZNetView (a MonoBehaviour with m_persistent and m_distant).
    /// </summary>
    private bool IsNetworked(Where w)
    {
        var go = Read(w.Bundle, w.PathId);
        return go != null && Components(w.Bundle, go).Any(c => c.Type == (int)AssetClassID.MonoBehaviour && Has(c.F, "m_persistent") && Has(c.F, "m_distant"));
    }

    // ---- Assets by name.

    /// <summary>
    /// The kinds of objects <see cref="Named"/> finds: what a model is made of.
    /// </summary>
    private static readonly AssetClassID[] NamedKinds = [AssetClassID.Mesh, AssetClassID.Material, AssetClassID.Texture2D];

    /// <summary>
    /// "kind:name" -&gt; the objects so named (kind: Unity's class id); null until built or read from its kept file.
    /// </summary>
    private Dictionary<string, List<Where>>? _named;

    /// <summary>
    /// The meshes, materials or textures of that name, in every bundle (several: the same name in several bundles, or
    /// twice in one). Asked first, the names are read from every bundle (seconds, once; kept in a file next to the
    /// index until the bundles change).
    /// </summary>
    /// <param name="kind">Mesh, Material or Texture2D.</param>
    /// <param name="name">The object's name (m_Name).</param>
    public IReadOnlyList<Where> Named(AssetClassID kind, string name)
    {
        lock (_sync)
        {
            return EnsureNamed().GetValueOrDefault($"{(int)kind}:{name}") ?? [];
        }
    }

    /// <summary>
    /// Makes sure the names are there (see <see cref="Named"/>).
    /// </summary>
    public void EnsureNamesRead()
    {
        lock (_sync)
        {
            EnsureNamed();
        }
    }

    /// <summary>
    /// Where the names are kept: beside the index's file (none when the index is not kept).
    /// </summary>
    private string? NamesFile => _cacheFile == null ? null : Path.Combine(Path.GetDirectoryName(_cacheFile)!, Path.GetFileNameWithoutExtension(_cacheFile) + "-names.json");

    /// <summary>
    /// The names: there already, read from their kept file while the bundles are unchanged, else read from the bundles
    /// (and kept).
    /// </summary>
    private Dictionary<string, List<Where>> EnsureNamed()
    {
        if (_named != null)
        {
            return _named;
        }
        string now = JsonSerializer.Serialize(BundlesNow());
        string? file = NamesFile;
        if (file != null && File.Exists(file))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                if (doc.RootElement.GetProperty("Bundles").GetRawText().Replace(" ", "", StringComparison.Ordinal) == now)
                {
                    var kept = new Dictionary<string, List<Where>>();
                    foreach (var p in doc.RootElement.GetProperty("Names").EnumerateObject())
                    {
                        kept[p.Name] = [.. p.Value.EnumerateArray().Select(k => Where.FromKey(k.GetString() ?? "")).OfType<Where>()];
                    }
                    return _named = kept;
                }
            }
            catch (Exception e) when (e is JsonException or IOException or KeyNotFoundException or InvalidOperationException)
            {
                Console.WriteLine($"game files: the names cache could not be read ({e.Message}), reading the bundles again");
            }
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var named = new Dictionary<string, List<Where>>();
        foreach (string bundle in _files.Keys.Order(StringComparer.Ordinal))
        {
            // A bundle that cannot be read (a mod's, damaged or unusual) is skipped: the others' names still count.
            try
            {
                var b = _am.LoadBundleFile(_files[bundle], false);
                var a = _am.LoadAssetsFileFromBundle(b, 0, false);
                var reader = a.file.Reader;
                foreach (var info in a.file.AssetInfos)
                {
                    if (Array.IndexOf(NamedKinds, (AssetClassID)info.TypeId) < 0)
                    {
                        continue;
                    }
                    // m_Name is a named object's first field: nothing else read.
                    reader.Position = info.GetAbsoluteByteOffset(a.file);
                    int n = reader.ReadInt32();
                    if (n is <= 0 or > 256)
                    {
                        continue;
                    }
                    string key = $"{info.TypeId}:{System.Text.Encoding.UTF8.GetString(reader.ReadBytes(n))}";
                    if (!named.TryGetValue(key, out var list))
                    {
                        named[key] = list = [];
                    }
                    list.Add(new Where(bundle, info.PathId));
                }
                if (!_open.ContainsKey(bundle))
                {
                    _am.UnloadAssetsFile(a);
                    _am.UnloadBundleFile(b);
                }
            }
            catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or EndOfStreamException
                or IndexOutOfRangeException or ArgumentException or NullReferenceException or OverflowException)
            {
                Console.WriteLine($"game files: the names of {bundle} could not be read ({e.Message}), skipped");
            }
        }
        Console.WriteLine($"game files: {named.Count} mesh, material and texture names read from {_files.Count} bundles in {sw.ElapsedMilliseconds} ms");
        if (file != null)
        {
            try
            {
                SafeFile.WriteAllText(file, JsonSerializer.Serialize(new { Bundles = BundlesNow(), Names = named.ToDictionary(kv => kv.Key, kv => kv.Value.Select(w => w.Key)) }));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"game files: the names could not be kept ({e.Message})");
            }
        }
        return _named = named;
    }

    /// <summary>
    /// A mesh's bounds as Unity keeps them (m_LocalAABB: centre and extent, x, y, z each), null when it cannot be read
    /// (to tell apart meshes of the same name; one that cannot be read is not a candidate).
    /// </summary>
    public float[]? BoundsOf(Where mesh)
    {
        lock (_sync)
        {
            try
            {
                if (Read(mesh.Bundle, mesh.PathId) is not { } m || !Has(m, "m_LocalAABB"))
                {
                    return null;
                }
                var c = m["m_LocalAABB.m_Center"];
                var e = m["m_LocalAABB.m_Extent"];
                return [c["x"].AsFloat, c["y"].AsFloat, c["z"].AsFloat, e["x"].AsFloat, e["y"].AsFloat, e["z"].AsFloat];
            }
            catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or IndexOutOfRangeException or ArgumentException)
            {
                return null;
            }
            finally
            {
                _fields.Clear();
            }
        }
    }

    /// <summary>
    /// A mesh's vertex count, -1 when it cannot be read (to tell apart meshes of the same name; one that cannot be read
    /// is not a candidate).
    /// </summary>
    public int VertexCountOf(Where mesh)
    {
        lock (_sync)
        {
            try
            {
                return Read(mesh.Bundle, mesh.PathId) is { } m ? (int)m["m_VertexData.m_VertexCount"].AsUInt : -1;
            }
            catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or IndexOutOfRangeException or ArgumentException)
            {
                return -1;
            }
            finally
            {
                _fields.Clear();
            }
        }
    }

    // ---- Reading objects.

    /// <summary>
    /// Whether the folder has this bundle (by file name).
    /// </summary>
    public bool HasBundle(string name) => _files.ContainsKey(name);

    /// <summary>
    /// The bundle's file, for what is read from its size and date.
    /// </summary>
    public string? PathOf(string name) => _files.GetValueOrDefault(name);

    /// <summary>
    /// Every MonoBehaviour of a bundle, its fields read whole (not kept): data the game keeps on its scene
    /// objects, such as the location rules (GameLocations). Objects that cannot be read are skipped.
    /// </summary>
    public List<AssetTypeValueField> MonoBehaviours(string bundle)
    {
        lock (_sync)
        {
            var (_, a) = Open(bundle);
            var list = new List<AssetTypeValueField>();
            // Without type trees no script's fields are described: none read.
            foreach (var info in a.file.Metadata.TypeTreeEnabled ? a.file.GetAssetsOfType(AssetClassID.MonoBehaviour) : [])
            {
                try
                {
                    list.Add(_am.GetBaseField(a, info));
                }
                catch (Exception e) when (e is IOException or InvalidDataException or NotImplementedException or NotSupportedException or IndexOutOfRangeException or ArgumentException)
                {
                    // A script whose fields are not described (no type tree): not one of ours.
                }
            }
            return list;
        }
    }

    /// <summary>
    /// A bundle and its assets file, opened on first use and kept open.
    /// </summary>
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

    /// <summary>
    /// Fills the CAB name map from every bundle, when the index was read from the kept file and did not build it.
    /// </summary>
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

    /// <summary>
    /// An object's fields, null when the bundle has no such object or does not describe its objects' fields (no type
    /// trees: the editor has no class database to read them by); kept in <c>_fields</c> until the read is over.
    /// </summary>
    private AssetTypeValueField? Read(string bundle, long pathId)
    {
        if (_fields.TryGetValue((bundle, pathId), out var cached))
        {
            return cached;
        }
        var (_, a) = Open(bundle);
        var info = a.file.GetAssetInfo(pathId);
        var f = info == null || !a.file.Metadata.TypeTreeEnabled ? null : _am.GetBaseField(a, info);
        _fields[(bundle, pathId)] = f;
        return f;
    }

    /// <summary>
    /// An object's Unity class id, -1 when it is not there.
    /// </summary>
    private int TypeOf(string bundle, long pathId) => Open(bundle).Assets.file.GetAssetInfo(pathId)?.TypeId ?? -1;

    /// <summary>
    /// Whether a field is present (AssetsTools gives a dummy for a missing one).
    /// </summary>
    private static bool Has(AssetTypeValueField f, string name) => !f[name].IsDummy;

    /// <summary>
    /// A reference (m_FileID, m_PathID) made in a bundle: where it points (another bundle for m_FileID &gt; 0).
    /// </summary>
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
        return _cab.TryGetValue(cab, out string? b) ? new Where(b, pid) : null;
    }

    /// <summary>
    /// Data kept out of the object, in a resource file of the same bundle (.resS): meshes, textures.
    /// </summary>
    /// <param name="bundle">The bundle the object is in.</param>
    /// <param name="streamData">The object's m_StreamData field (offset, size, path).</param>
    /// <param name="skip">Bytes to skip after the data's offset.</param>
    /// <param name="take">How many bytes to read; -1: the rest of the data.</param>
    private byte[] StreamBytes(string bundle, AssetTypeValueField streamData, long skip = 0, long take = -1)
    {
        long offset = streamData["offset"].AsLong + skip;
        long size = take >= 0 ? take : streamData["size"].AsUInt - skip;
        var (b, _) = Open(bundle);
        int idx = b.file.GetFileIndex(streamData["path"].AsString.Split('/').Last());
        if (idx < 0)
        {
            return [];
        }
        b.file.GetFileRange(idx, out long start, out _);
        lock (b.file.DataReader)
        {
            b.file.DataReader.Position = start + offset;
            return b.file.DataReader.ReadBytes((int)size);
        }
    }

    /// <summary>
    /// A GameObject's components that can be read, with their class id and path id.
    /// </summary>
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

    /// <summary>
    /// A prefab's model: what the game draws of it. Left out: the worn and broken looks of pieces
    /// (WearNTear), lower levels of detail, inactive objects, disabled renderers; for dungeon rooms and
    /// locations also their networked objects (saved objects of their own) and random parts not there half
    /// the time. Skinned meshes (animated stations) are drawn in their bind pose at the renderer.
    /// </summary>
    /// <param name="name">The prefab name.</param>
    /// <param name="room">A dungeon room or a location: leave out its networked objects and its random parts not
    /// there half the time.</param>
    /// <returns>The model, or null when the game has no such prefab.</returns>
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

    /// <summary>
    /// Reads the model of a prefab's root: marks worn, broken and lower-detail parts (and for rooms their networked
    /// objects and random parts) to be skipped, then walks the active objects placing each renderer's submeshes.
    /// </summary>
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
        float[] rootScale = [1, 1, 1];
        if (Read(bundle, root.PathId) is { } rgo && Components(bundle, rgo).FirstOrDefault(c => c.Type == (int)AssetClassID.Transform).F is { } rt)
        {
            var s = rt["m_LocalScale"];
            rootScale = [s["x"].AsFloat, s["y"].AsFloat, s["z"].AsFloat];
        }
        return new Model(name, parts, rootScale);
    }

    /// <summary>
    /// How many submeshes a mesh has (0 when it cannot be read).
    /// </summary>
    private int SubmeshCount(Where mesh) => Read(mesh.Bundle, mesh.PathId)?["m_SubMeshes.Array"].Children.Count ?? 0;

    /// <summary>
    /// Bytes per component, by Unity's VertexFormat (Float, Float16, UNorm8, SNorm8, UNorm16, SNorm16, UInt8, SInt8,
    /// UInt16, SInt16, UInt32, SInt32).
    /// </summary>
    private static readonly int[] FormatSize = [4, 2, 1, 1, 2, 2, 1, 1, 2, 2, 4, 4];

    /// <summary>
    /// A mesh's vertices and triangles, null when there is no such object (a compressed mesh too: Unity's mesh
    /// compression, which some mods' bundles use).
    /// </summary>
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

    /// <summary>
    /// Reads a mesh's vertex streams (positions, normals, first UV channel) and its 16- or 32-bit index buffer.
    /// </summary>
    private Mesh ReadMesh(Where at, AssetTypeValueField m)
    {
        string name = m["m_Name"].AsString;
        if (Has(m, "m_CompressedMesh") && m["m_CompressedMesh.m_Vertices.m_NumItems"].AsUInt > 0)
        {
            return ReadCompressedMesh(name, m);
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
        int[] stride = new int[streams];
        foreach (var c in used)
        {
            stride[c.Stream] = Math.Max(stride[c.Stream], c.Offset + c.Dim * FormatSize[c.Format]);
        }
        int[] start = new int[streams];
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
            float[] res = new float[n * want];
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
                subs.Add([]);
                continue;
            }
            int[] idx = new int[count - count % 3];
            for (int k = 0; k < idx.Length; k++)
            {
                idx[k] = baseV + (wide ? (int)BitConverter.ToUInt32(ib, first + 4 * k) : BitConverter.ToUInt16(ib, first + 2 * k));
            }
            subs.Add(idx);
        }
        return new Mesh(name, n, Channel(0, 3) ?? new float[n * 3], Channel(1, 3), Channel(4, 2), [.. subs]);
    }

    /// <summary>
    /// A compressed mesh (Unity's mesh compression: positions, normals, uv and triangles packed in bits, each value
    /// a step of its range): positions, normals (two parts and the sign of the third), the first uv channel, and the
    /// triangles cut into the submeshes as an uncompressed mesh's index buffer would be.
    /// </summary>
    private static Mesh ReadCompressedMesh(string name, AssetTypeValueField m)
    {
        var c = m["m_CompressedMesh"];
        float[] positions = UnpackFloats(c["m_Vertices"]);
        int n = positions.Length / 3;
        float[]? normals = null;
        float[] packedNormals = UnpackFloats(c["m_Normals"]);
        if (packedNormals.Length >= n * 2 && n > 0)
        {
            int[] signs = UnpackInts(c["m_NormalSigns"]);
            normals = new float[n * 3];
            for (int v = 0; v < n; v++)
            {
                float x = packedNormals[v * 2], y = packedNormals[v * 2 + 1];
                float z = MathF.Sqrt(Math.Max(0, 1 - x * x - y * y));
                if (v < signs.Length && signs[v] == 0)
                {
                    z = -z;
                }
                float len = MathF.Sqrt(x * x + y * y + z * z);
                if (len > 0)
                {
                    (x, y, z) = (x / len, y / len, z / len);
                }
                (normals[v * 3], normals[v * 3 + 1], normals[v * 3 + 2]) = (x, y, z);
            }
        }
        float[] uvs = UnpackFloats(c["m_UV"]);
        // The first channel comes first (m_UVInfo says which channels follow).
        float[]? uv = uvs.Length >= n * 2 ? uvs[..(n * 2)] : null;
        int[] triangles = UnpackInts(c["m_Triangles"]);
        bool wide = m["m_IndexFormat"].AsInt == 1;
        var subs = new List<int[]>();
        foreach (var sm in m["m_SubMeshes.Array"].Children)
        {
            int first = (int)sm["firstByte"].AsUInt / (wide ? 4 : 2), count = (int)sm["indexCount"].AsUInt, baseV = (int)sm["baseVertex"].AsUInt;
            if (sm["topology"].AsInt != 0 || first + count > triangles.Length)
            {
                subs.Add([]);
                continue;
            }
            subs.Add([.. triangles.AsSpan(first, count - count % 3).ToArray().Select(i => baseV + i)]);
        }
        return new Mesh(name, n, positions, normals, uv, [.. subs]);
    }

    /// <summary>
    /// A PackedBitVector of numbers: each m_BitSize bits (little end first) a step of m_Range from m_Start.
    /// </summary>
    private static float[] UnpackFloats(AssetTypeValueField v)
    {
        int bits = v["m_BitSize"].AsByte;
        float range = v["m_Range"].AsFloat, start = v["m_Start"].AsFloat;
        int[] raw = Unpack(v, bits);
        float max = bits >= 32 ? uint.MaxValue : (1u << bits) - 1;
        float[] res = new float[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            res[i] = max > 0 ? start + range * ((uint)raw[i] / max) : start;
        }
        return res;
    }

    /// <summary>A PackedBitVector of integers (each m_BitSize bits, little end first).</summary>
    private static int[] UnpackInts(AssetTypeValueField v) => Unpack(v, v["m_BitSize"].AsByte);

    /// <summary>The m_NumItems values of <paramref name="bits"/> bits each packed in m_Data, as Unity packs them.</summary>
    private static int[] Unpack(AssetTypeValueField v, int bits)
    {
        int count = (int)v["m_NumItems"].AsUInt;
        byte[] data = v["m_Data.Array"].AsByteArray;
        if (count <= 0 || bits is <= 0 or > 32 || (long)count * bits > (long)data.Length * 8)
        {
            return [];
        }
        int[] res = new int[count];
        int index = 0, bit = 0;
        for (int i = 0; i < count; i++)
        {
            uint x = 0;
            int got = 0;
            while (got < bits)
            {
                x |= (uint)(data[index] >> bit) << got;
                int take = Math.Min(bits - got, 8 - bit);
                bit += take;
                got += take;
                if (bit == 8)
                {
                    index++;
                    bit = 0;
                }
            }
            res[i] = (int)(bits == 32 ? x : x & ((1u << bits) - 1));
        }
        return res;
    }

    // ---- Materials and textures.

    /// <summary>
    /// A material's shader and saved properties, null when there is no such object.
    /// </summary>
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
                    colors[c["first"].AsString] = [v["r"].AsFloat, v["g"].AsFloat, v["b"].AsFloat, v["a"].AsFloat];
                }
                return new Material(m["m_Name"].AsString, shader, shaderAt, texs, floats, colors);
            }
            finally
            {
                _fields.Clear();
            }
        }
    }

    /// <summary>
    /// Bytes per 4 x 4 block (block formats) or per pixel, by Unity TextureFormat; 0: not read here.
    /// </summary>
    public static (int Bytes, bool Block) FormatLayout(int format) => format switch
    {
        10 or 26 => (8, true),             // DXT1, BC4
        12 or 25 or 27 or 24 => (16, true), // DXT5, BC7, BC5, BC6H
        1 or 63 => (1, false),             // Alpha8, R8
        3 => (3, false),                   // RGB24
        4 or 5 or 14 => (4, false),        // RGBA32, ARGB32, BGRA32
        _ => (0, false),
    };

    /// <summary>
    /// The bytes of one mip level of a texture of this format and size (block formats: whole 4 x 4 blocks).
    /// </summary>
    public static int LevelSize(int format, int w, int h)
    {
        var (bytes, block) = FormatLayout(format);
        return block ? Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4) * bytes : w * h * bytes;
    }

    /// <summary>
    /// The largest mip level no wider or taller than maxSize (the smallest there is when none is). Null when the object
    /// is not a Texture2D or its format is not one <c>FormatLayout</c> reads.
    /// </summary>
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

    /// <summary>
    /// A Texture2DArray's GraphicsFormat as a TextureFormat (the compressed ones read here).
    /// </summary>
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

    /// <summary>
    /// The top level of each slice of a texture array (bottom row first). Null when the object is not a Texture2DArray
    /// or its format is not read here.
    /// </summary>
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
                return [.. Enumerable.Range(0, depth).Select(i => new TextureLevel($"{t["m_Name"].AsString}[{i}]", format, w, h, data.AsSpan(i * per, size).ToArray()))];
            }
            finally
            {
                _fields.Clear();
            }
        }
    }

    /// <summary>
    /// A shader's programs.
    /// </summary>
    /// <param name="Platforms">The shader platforms it has (Unity's ShaderCompilerPlatform).</param>
    /// <param name="Blobs">Each platform's program blob, LZ4-unpacked.</param>
    /// <param name="Deferred">The sub-programs of its deferred pass's vertex program (for the Vulkan one).</param>
    public sealed record ShaderPrograms(int[] Platforms, Dictionary<int, byte[]> Blobs, List<GameShader.SubProgram> Deferred);

    /// <summary>
    /// A shader's programs, null when the object is not a Shader. Throws InvalidDataException when a blob does not
    /// unpack.
    /// </summary>
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
                int[] platforms = [.. sh["platforms.Array"].Children.Select(c => (int)c.AsUInt)];
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
                        byte[] outBuf = new byte[full[k]];
                        int got;
                        try
                        {
                            got = LZ4ps.LZ4Codec.Decode32(blob, (int)offs[k], (int)comp[k], outBuf, 0, (int)full[k], true);
                        }
                        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException)
                        {
                            // LZ4ps's own errors for a broken block or one shorter than said.
                            got = -1;
                        }
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
                            var prms = li < paramLists.Count ? Numbers(paramLists[li]).ToList() : [];
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

    /// <summary>
    /// The numbers of a field: itself, or those of its array (nested arrays flattened).
    /// </summary>
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

    /// <summary>
    /// Matrices: row-major 4 x 4, column vectors (Unity's).
    /// </summary>
    public static class Matrix
    {
        /// <summary>
        /// The identity matrix.
        /// </summary>
        public static float[] Identity() => [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

        /// <summary>
        /// The product a * b (b applied first).
        /// </summary>
        public static float[] Mul(float[] a, float[] b)
        {
            float[] r = new float[16];
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

        /// <summary>
        /// A Unity transform as a matrix: translate * rotate * scale.
        /// </summary>
        /// <param name="px">The position's x.</param>
        /// <param name="py">The position's y.</param>
        /// <param name="pz">The position's z.</param>
        /// <param name="x">The rotation quaternion's x.</param>
        /// <param name="y">The rotation quaternion's y.</param>
        /// <param name="z">The rotation quaternion's z.</param>
        /// <param name="w">The rotation quaternion's w.</param>
        /// <param name="sx">The scale along x.</param>
        /// <param name="sy">The scale along y.</param>
        /// <param name="sz">The scale along z.</param>
        public static float[] Trs(float px, float py, float pz, float x, float y, float z, float w, float sx, float sy, float sz) =>
        [
            (1 - 2 * (y * y + z * z)) * sx, 2 * (x * y - z * w) * sy, 2 * (x * z + y * w) * sz, px,
            2 * (x * y + z * w) * sx, (1 - 2 * (x * x + z * z)) * sy, 2 * (y * z - x * w) * sz, py,
            2 * (x * z - y * w) * sx, 2 * (y * z + x * w) * sy, (1 - 2 * (x * x + y * y)) * sz, pz,
            0, 0, 0, 1,
        ];

        /// <summary>
        /// A Transform's local position, rotation and scale fields as a matrix.
        /// </summary>
        internal static float[] Trs(AssetTypeValueField p, AssetTypeValueField q, AssetTypeValueField s) =>
            Trs(p["x"].AsFloat, p["y"].AsFloat, p["z"].AsFloat, q["x"].AsFloat, q["y"].AsFloat, q["z"].AsFloat, q["w"].AsFloat, s["x"].AsFloat, s["y"].AsFloat, s["z"].AsFloat);
    }
}
