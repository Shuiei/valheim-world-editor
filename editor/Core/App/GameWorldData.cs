using TerrainEditor.Save;
using TerrainEditor.Terrain;
using ValheimGen;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>The game's own world rules and objects as the editor read them from the player's Valheim, as extensions see them (IWorldGameData).</summary>
internal sealed class GameWorldData : IWorldGameData
{
    /// <summary>The one there is: it reads the editor's state each time.</summary>
    public static GameWorldData Instance { get; } = new();

    /// <inheritdoc/>
    public bool LocationsRead => SeedLocations.Available;

    /// <inheritdoc/>
    public LocationRuleSet Locations => SeedLocations.Current;

    /// <inheritdoc/>
    public Task ReadLocations() => GameLocations.UseGameRules(GameLook.Bundles);

    /// <inheritdoc/>
    public IReadOnlyList<VegetationRule> Vegetation => Regrow.GameRules;

    /// <inheritdoc/>
    public bool HasPrefab(string name) => PrefabCatalog.Get(StableHash.Of(name)) != null;

    /// <inheritdoc/>
    public bool IsGameRoom(string name) => Editing.Dungeons.IsGameRoom(name);
}
