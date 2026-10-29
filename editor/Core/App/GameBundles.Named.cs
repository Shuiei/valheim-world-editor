using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// The assets the game finds by name (meshes, materials, textures) and the names index.
/// </summary>
public sealed partial class GameBundles
{
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
}
