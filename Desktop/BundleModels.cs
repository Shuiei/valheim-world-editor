using System.Collections.Concurrent;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The models of ModelStore read from the game's own files (GameBundles), as the exporter used to copy
/// them: z mirrored (Unity is left-handed, the view right-handed), textures no larger than 1024 pixels,
/// cut-out textures bled at their edges. Ids: a mesh or material is "&lt;bundle&gt;:&lt;path id&gt;", a texture the
/// same with "|cut" for a cut-out one; "+" before them: in the mods' bundles (ModBundles). Mod prefabs, which no
/// bundle holds whole (mods make them while the game runs), are drawn as the game reported it (ModCatalogue.ModelOf):
/// each part's mesh and material found by name in the mods' bundles, else the game's.
/// </summary>
internal sealed partial class BundleModels : ModelStore.ISource
{
    /// <summary>Textures are drawn no larger than this, in pixels (the mip level at or under it).</summary>
    public const int TextureMax = 1024;

    /// <summary>
    /// Creatures players tame (and their young), drawn in the Tamed animals kind; Deer and Neck are tamed
    /// by server mods.
    /// </summary>
    private static readonly HashSet<string> Tameable = new(StringComparer.Ordinal)
    {
        "Boar", "Boar_piggy", "Wolf", "Wolf_cub", "Lox", "Lox_Calf", "Hen", "Chicken", "Asksvin", "Asksvin_hatchling", "Deer", "Neck",
    };

    /// <summary>The game's files the models are read from.</summary>
    private readonly GameBundles _game;
    /// <summary>The mods' bundles (copied out of their DLLs the first time a mod's model is wanted); null: none.</summary>
    private readonly Lazy<GameBundles?> _mods;
    /// <summary>What marks an id as one in the mods' bundles.</summary>
    private const char Mod = '+';
    /// <summary><see cref="ObjectName"/>'s answers by prefab hash, worked out once each.</summary>
    private readonly ConcurrentDictionary<int, string?> _objectNames = new();

    /// <summary>Models read from these game files; <see cref="For"/> checks they can be read first.</summary>
    /// <param name="game">The game's bundles.</param>
    /// <param name="mods">The mods' bundles, asked for the first time one is needed (null: ModBundles' for the
    /// settings).</param>
    internal BundleModels(GameBundles game, Func<GameBundles?>? mods = null)
    {
        _game = game;
        _mods = new(mods ?? (() => ModBundles.Reader(AppSettings.Load())));
    }

    /// <summary>The models of the game's files (their index read or built now), or null when there are none.</summary>
    public static BundleModels? For(GameBundles? game)
    {
        if (game == null)
        {
            return null;
        }
        try
        {
            return game.Index.Count > 0 ? new BundleModels(game) : null;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or NotImplementedException)
        {
            Log.Caught(e, "reading the game's bundles for models (objects are drawn as boxes)");
            return null;
        }
    }

    /// <summary>
    /// World objects with a model of their own: saved (networked) objects that are not build pieces,
    /// creatures, items or ragdolls; also locations (runestones, and the parts of ruins, camps and altars the
    /// game makes again from a location proxy, see LocationParts) and tameable creatures.
    /// </summary>
    /// <returns>The prefab's name, or null when it is not drawn as an object model.</returns>
    public string? ObjectName(int prefab) => _objectNames.GetOrAdd(prefab, p =>
    {
        if (PieceCatalog.Get(p) != null || _game.NameOf(p) is not string name || _game.KindOf(name) is not { } kind)
        {
            return null;
        }
        bool extra = kind.Location || name.StartsWith("Runestone_", StringComparison.Ordinal) || kind.Creature && Tameable.Contains(name);
        return extra || kind.Networked && !kind.Creature && !kind.Item && !name.EndsWith("_ragdoll", StringComparison.Ordinal) ? name : null;
    });

    /// <summary>The bundles an id is in, and the id there (without <see cref="Mod"/>); null for a mod's when there are none.</summary>
    private (GameBundles Game, string Id)? Source(string id) =>
        id.StartsWith(Mod) ? _mods.Value is { } mods ? (mods, id[1..]) : null : (_game, id);
}
