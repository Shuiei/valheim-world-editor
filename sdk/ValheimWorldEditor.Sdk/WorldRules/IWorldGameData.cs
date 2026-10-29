using System.Collections.Generic;
using System.Threading.Tasks;
using ValheimGen;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// The game's own world rules and objects as the player's Valheim has them (<see cref="IEditorHost.World"/>): what a
/// mod's world rules are converted against.
/// </summary>
public interface IWorldGameData
{
    /// <summary>Whether the game's location rules have been read (<see cref="Locations"/>).</summary>
    bool LocationsRead { get; }

    /// <summary>The game's location kinds and alt biomes (empty until read: <see cref="ReadLocations"/>).</summary>
    LocationRuleSet Locations { get; }

    /// <summary>Reads the game's location rules from its files, when they have not been. Throws when they cannot be read.</summary>
    Task ReadLocations();

    /// <summary>The game's vegetation rules.</summary>
    IReadOnlyList<VegetationRule> Vegetation { get; }

    /// <summary>Whether the game has a prefab of this name.</summary>
    /// <param name="name">The prefab's name.</param>
    bool HasPrefab(string name);

    /// <summary>Whether the game has a dungeon room of this name.</summary>
    /// <param name="name">The room's name.</param>
    bool IsGameRoom(string name);
}
