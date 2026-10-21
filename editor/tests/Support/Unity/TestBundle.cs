namespace WorldEditor.Tests.Unity;

/// <summary>
/// One made-up asset bundle being written: its serialized file (CAB-name), its scripts, prefabs (GameObject trees),
/// table of contents (AssetBundle) and resource file (.resS). Saved into a bundles folder with <c>Save</c>.
/// </summary>
public sealed partial class TestBundle
{
    /// <summary>
    /// The scripts made in this bundle (MonoScripts), by class name: their path ids.
    /// </summary>
    private readonly Dictionary<string, long> _scripts = new(StringComparer.Ordinal);
    /// <summary>
    /// The table of contents: each asset's path and path id.
    /// </summary>
    private readonly List<(string Path, long PathId)> _contents = [];
    /// <summary>
    /// The resource file's bytes (data kept out of objects).
    /// </summary>
    private readonly List<byte> _resource = [];

    /// <summary>
    /// A bundle of this file name; its serialized file is "CAB-" + the name.
    /// </summary>
    public TestBundle(string name, bool typeTrees = true)
    {
        Name = name;
        Assets = new SerializedFileWriter { TypeTrees = typeTrees };
    }

    /// <summary>
    /// The bundle's file name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Its serialized file's name (what references from other bundles name).
    /// </summary>
    public string Cab => "CAB-" + Name;

    /// <summary>
    /// Its resource file's name.
    /// </summary>
    public string ResourceName => Cab + ".resS";

    /// <summary>
    /// Its serialized file, for objects added directly.
    /// </summary>
    public SerializedFileWriter Assets { get; }

    /// <summary>
    /// Where the scripts of classes not made here are: another bundle (null: made here when first used).
    /// </summary>
    public TestBundle? ScriptsFrom { get; set; }

    /// <summary>
    /// Whether the table of contents (AssetBundle object) is written.
    /// </summary>
    public bool WithContents { get; set; } = true;

    /// <summary>
    /// A path id kept for an object added later (one referenced before it is written).
    /// </summary>
    public long Reserve() => Assets.Reserve();

    /// <summary>
    /// The file id of another bundle's serialized file in this one's references.
    /// </summary>
    public int FileOf(TestBundle other) => ReferenceEquals(other, this) ? 0 : Assets.External($"archive:/{other.Cab}/{other.Cab}");

    /// <summary>
    /// A reference to an object of another bundle (or this one).
    /// </summary>
    public Fields RefTo(TestBundle other, long pathId) => Fields.Ref(pathId, FileOf(other));

    /// <summary>
    /// Lists an asset in the table of contents (a prefab: "assets/....prefab").
    /// </summary>
    public void Contents(string path, long pathId) => _contents.Add((path, pathId));

    /// <summary>
    /// Adds bytes to the resource file; returns their offset in it.
    /// </summary>
    public long Resource(byte[] data)
    {
        long at = _resource.Count;
        _resource.AddRange(data);
        return at;
    }

    /// <summary>
    /// Where data put in the resource file is, as an object's m_StreamData.
    /// </summary>
    public Fields StreamData(long offset, int size) => new() { ["offset"] = (ulong)offset, ["size"] = (uint)size, ["path"] = $"archive:/{Cab}/{ResourceName}" };

    /// <summary>
    /// A prefab: a GameObject tree built by <paramref name="build"/> on its root, listed in the table of contents
    /// (assets/name.prefab unless <paramref name="path"/> is given). Returns the root.
    /// </summary>
    public PrefabNode Prefab(string name, Action<PrefabNode>? build = null, string? path = null)
    {
        var root = new PrefabNode(this, name, father: 0);
        build?.Invoke(root);
        root.Write();
        Contents(path ?? $"assets/prefabs/{name}.prefab", root.Go);
        return root;
    }
}
