namespace TerrainEditor.App;

/// <summary>
/// The kinds objects are sorted into for the View switches, the same rules as the web editor's
/// objectKind: by name, building pieces placed by players apart.
/// </summary>
public enum ObjectKind
{
    /// <summary>Building pieces a player placed (they have a builder).</summary>
    Buildings,
    /// <summary>
    /// Building pieces without a builder and the game's own structures (goblin camps, wrecks, statues, Ashlands
    /// and Deep North ruins...).
    /// </summary>
    Ruins,
    /// <summary>The game's furniture and props: dverger and Deep North props, rugs, banners, cauldrons, webs...</summary>
    Furniture,
    /// <summary>Trees, stumps, logs, roots, branches and saplings.</summary>
    Trees,
    /// <summary>Rocks, stones, ice, boulders, cliffs and pillars.</summary>
    Rocks,
    /// <summary>Mineable rocks and deposits: copper, tin, silver, obsidian, mud piles, tar...</summary>
    Ore,
    /// <summary>Bushes, shrubs, ferns, vines and cloudberries.</summary>
    Bushes,
    /// <summary>Things picked by hand: Pickable_ prefabs, mushrooms, dandelions, thistles.</summary>
    Pickables,
    /// <summary>Bones, skulls, carcasses, ragdolls and the giants' remains.</summary>
    Remains,
    /// <summary>Creatures a player tamed; wild ones are <see cref="Creatures"/>.</summary>
    Animals,
    /// <summary>Wild creatures, traders and other characters, and birds.</summary>
    Creatures,
    /// <summary>Invisible spawn points (CreatureSpawner, TriggerSpawner): each spawns a creature where it stands.</summary>
    SpawnPoints,
    /// <summary>Nests and piles (SpawnArea) that spawn creatures around themselves.</summary>
    Nests,
    /// <summary>Runestones (by the prefab catalogue's runestone names).</summary>
    Runestones,
    /// <summary>Dungeons (DG_ objects) and their rooms.</summary>
    Dungeons,
    /// <summary>Effects and the game's markers: mist, music, boss event zones, location proxies...</summary>
    Effects,
    /// <summary>Everything that fits no other kind, tombstones among them.</summary>
    Other,
}
