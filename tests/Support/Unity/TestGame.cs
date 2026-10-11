using System.Text;

namespace WorldEditor.Tests.Unity;

/// <summary>
/// A made-up Valheim folder in a temporary folder, laid out as the game's (valheim_Data/StreamingAssets/SoftRef/Bundles,
/// the SoftRef manifest beside it, resources.assets and Managed/assembly_valheim.dll), filled by the test with its own
/// bundles: what the editor reads of the game, without the game. Deleted when disposed.
/// </summary>
public sealed class TestGame : IDisposable
{
    /// <summary>
    /// The bundles to write, in the order made.
    /// </summary>
    private readonly List<TestBundle> _bundles = [];
    /// <summary>
    /// The manifest's assets: asset id, bundle, path in the bundle.
    /// </summary>
    private readonly List<(string Id, string Bundle, string Path)> _assets = [];
    /// <summary>
    /// The manifest's bundles with the bundles each needs.
    /// </summary>
    private readonly List<(string Bundle, string[] Needs)> _needs = [];

    /// <summary>
    /// An empty game folder in the temporary folder.
    /// </summary>
    public TestGame()
    {
        Root = Path.Combine(Path.GetTempPath(), $"vwe-game-{Guid.NewGuid():N}");
        Directory.CreateDirectory(BundlesDir);
    }

    /// <summary>
    /// The game folder (what GameLook.FindValheim gives).
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// valheim_Data.
    /// </summary>
    public string DataDir => Path.Combine(Root, "valheim_Data");

    /// <summary>
    /// The bundles folder.
    /// </summary>
    public string BundlesDir => Path.Combine(DataDir, "StreamingAssets", "SoftRef", "Bundles");

    /// <summary>
    /// Write the bundles with LZ4 blocks (else stored).
    /// </summary>
    public bool Lz4 { get; set; } = true;

    /// <summary>
    /// The bundles' block size.
    /// </summary>
    public int BlockSize { get; set; } = 1 << 17;

    /// <summary>
    /// A new bundle of this file name, written with the others by <c>Save</c>.
    /// </summary>
    public TestBundle Bundle(string name, bool typeTrees = true)
    {
        var b = new TestBundle(name, typeTrees);
        _bundles.Add(b);
        return b;
    }

    /// <summary>
    /// An asset in the SoftRef manifest: its id (32 hex digits) in a bundle, at a path ("Assets/.../name.prefab", a
    /// scene: ".unity").
    /// </summary>
    public void Manifest(string id, string bundle, string path) => _assets.Add((id, bundle, path));

    /// <summary>
    /// A bundle and the bundles it needs, in the SoftRef manifest.
    /// </summary>
    public void Needs(string bundle, params string[] needs) => _needs.Add((bundle, needs));

    /// <summary>
    /// Writes the bundles and the manifest.
    /// </summary>
    public void Save()
    {
        foreach (var b in _bundles)
        {
            b.Save(BundlesDir, Lz4, BlockSize);
        }
        var m = new StringBuilder();
        m.Append("bundles:\n");
        foreach (var (bundle, needs) in _needs)
        {
            m.Append("- bundle: ").Append(bundle).Append('\n');
            foreach (string n in needs)
            {
                m.Append("  - ").Append(n).Append('\n');
            }
        }
        m.Append("assets:\n");
        foreach (var (id, bundle, path) in _assets)
        {
            m.Append("- asset ID: ").Append(id).Append('\n');
            m.Append("  bundle: ").Append(bundle).Append('\n');
            m.Append("  path in bundle: ").Append(path).Append('\n');
        }
        File.WriteAllText(Path.Combine(BundlesDir, "..", "manifest"), m.ToString());
    }

    /// <summary>
    /// Writes resources.assets with these text assets (name, text): the game's localization tables are among them.
    /// </summary>
    public void Resources(IEnumerable<(string Name, string Text)> texts, bool typeTrees = true)
    {
        var f = new SerializedFileWriter { TypeTrees = typeTrees };
        foreach (var (name, text) in texts)
        {
            f.Add(UnityClass.TextAsset, UnityTypes.TextAsset, new Fields { ["m_Name"] = name, ["m_Script"] = text });
        }
        File.WriteAllBytes(Path.Combine(DataDir, "resources.assets"), f.ToBytes());
    }

    /// <summary>
    /// Puts a .NET assembly in as the game's code (Managed/assembly_valheim.dll): its string literals are what the
    /// editor names object data with.
    /// </summary>
    public void Code(string assemblyPath)
    {
        string managed = Path.Combine(DataDir, "Managed");
        Directory.CreateDirectory(managed);
        File.Copy(assemblyPath, Path.Combine(managed, "assembly_valheim.dll"), overwrite: true);
    }

    /// <summary>
    /// Deletes the folder.
    /// </summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left for the system's cleaning (on Windows a file still held open, or read-only, is refused as access).
        }
    }
}
