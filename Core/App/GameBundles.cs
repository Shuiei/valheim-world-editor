using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// The game's own files, read where Valheim keeps them (valheim_Data/StreamingAssets/SoftRef/Bundles):
/// models (their parts, meshes, materials) and textures, straight from the asset bundles, with nothing
/// copied. The bundles are Unity's (UnityFS, LZ4 blocks) and carry their type trees, so every field
/// reads by name (AssetsTools.NET). Which bundle holds which prefab comes from each bundle's table of
/// contents (its AssetBundle's m_Container): read once (a few seconds), kept in a file, read again when
/// the game's bundles change. Thread-safe: one reader at a time.
/// </summary>
public sealed partial class GameBundles
{
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
    /// Whether <see cref="_cab"/> holds every bundle's names: once the index is built, or <see cref="EnsureCabs"/> has
    /// gone through every bundle (some names alone, from bundles read before, do not count).
    /// </summary>
    private bool _cabsFull;
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
}
