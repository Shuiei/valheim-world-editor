using ValheimGen;
using ValheimWorldEditor.Sdk;

namespace WorldEditor.Tests;

/// <summary>
/// None of the game's world rules or objects (IWorldGameData): no location rules read (none to read), no vegetation, no
/// prefab or room known. For the add-ons' fake hosts, which need no game.
/// </summary>
public sealed class NoWorldGameData : IWorldGameData
{
    /// <inheritdoc/>
    public bool LocationsRead => false;

    /// <inheritdoc/>
    public LocationRuleSet Locations { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<VegetationRule> Vegetation => [];

    /// <inheritdoc/>
    public Task ReadLocations() => Task.CompletedTask;

    /// <inheritdoc/>
    public bool HasPrefab(string name) => false;

    /// <inheritdoc/>
    public bool IsGameRoom(string name) => false;
}
