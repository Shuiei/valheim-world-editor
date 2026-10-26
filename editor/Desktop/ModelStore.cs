using System.Collections.Concurrent;
using System.Numerics;
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
public sealed partial class ModelStore
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
    /// else the prefab's, else a held model's (an item on a stand, see StandLook).
    /// </summary>
    public string? NameOf(int prefab) => _source.ObjectName(prefab) ?? Editing.Dungeons.RoomOf(prefab)?.Name ?? PrefabCatalog.DisplayName(prefab) ?? StandLook.NameOf(prefab);

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
    /// Colours are sRGB; the shader lights in linear colours. One sRGB channel (0..1) to linear, by a 2.2 gamma.
    /// </summary>
    internal static float Linear(float v) => MathF.Pow(v, 2.2f);
}
