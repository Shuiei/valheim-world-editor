using System.Text.RegularExpressions;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

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

/// <summary>Sorting objects into <see cref="ObjectKind"/>s, with their labels for the View panel.</summary>
public static class ObjectKinds
{
    /// <summary>Every kind, in the View panel's order.</summary>
    public static readonly ObjectKind[] All = Enum.GetValues<ObjectKind>();

    /// <summary>The kind's name in the View panel.</summary>
    public static string Label(ObjectKind k) => k switch
    {
        ObjectKind.Buildings => "Your buildings",
        ObjectKind.Ruins => "Ruins & structures",
        ObjectKind.Furniture => "Furniture & props",
        ObjectKind.Trees => "Trees & logs",
        ObjectKind.Rocks => "Rocks",
        ObjectKind.Ore => "Ore & deposits",
        ObjectKind.Bushes => "Bushes & shrubs",
        ObjectKind.Pickables => "Pickables",
        ObjectKind.Remains => "Bones & remains",
        ObjectKind.Animals => "Tamed animals",
        ObjectKind.Creatures => "Creatures & NPCs",
        ObjectKind.SpawnPoints => "Spawn points",
        ObjectKind.Nests => "Creature nests",
        ObjectKind.Runestones => "Runestones",
        ObjectKind.Dungeons => "Dungeon rooms",
        ObjectKind.Effects => "Effects & markers",
        _ => "Other objects",
    };

    /// <summary>Shown when the editor opens, as in the web editor's View panel (spoilers stay hidden).</summary>
    public static bool ShownAtFirst(ObjectKind k) => k is ObjectKind.Buildings or ObjectKind.Trees or ObjectKind.Rocks or ObjectKind.Animals or ObjectKind.Runestones or ObjectKind.Dungeons;

    /// <summary>The game's structures: goblin camps, wrecks, statues, dungeon parts, chests...</summary>
    private static readonly Regex Ruin = new(@"^goblin_|^shipwreck_|^Statue|^BossStone_|^dungeon_|crypt_gate|^CastleKit_|^StartPlatform|^Beehive|^CargoCrate|^barrell|^RockDolmen|^TreasureChest", RegexOptions.IgnoreCase);
    /// <summary>The game's other structures: Ashlands and Deep North ruins, dverger towns, the Charred's ballista...</summary>
    private static readonly Regex Structure = new(@"^piece_Charred|_LOD$|^Ashlands?_|^Morkhalla_|^Morkborg|^dvergrtown_|^MountainKit_|^SunkenKit|^blackmarble|^frozenship|_path$|^Stump(Hut|Hole)", RegexOptions.IgnoreCase);
    /// <summary>Things picked by hand: Pickable_ prefabs, mushrooms, dandelions, thistles, loot spawners.</summary>
    private static readonly Regex Pickable = new(@"^LootSpawner|^Pickable_|^Pickable|mushroom|Dandelion|Thistle", RegexOptions.IgnoreCase);
    /// <summary>Trees, stumps, logs, roots, branches, shoots and saplings.</summary>
    private static readonly Regex Tree = new(@"tree|beech|birch|oak|^fir(?!e)|pine|stub|stump|log|root|yggdrasil|ashwood|sapling|shoot|branch", RegexOptions.IgnoreCase);
    /// <summary>Nests found by name when the prefab catalogue does not tell (greydwarf nests, bone piles, holes, eggs).</summary>
    private static readonly Regex Nest = new(@"^Spawner_.*(Nest|Pile|Hole)|^BonePileSpawner|^SeekerEgg", RegexOptions.IgnoreCase);
    /// <summary>Spawn points found by name when the prefab catalogue does not tell.</summary>
    private static readonly Regex SpawnPoint = new(@"^Spawner_|^TriggerSpawner_", RegexOptions.IgnoreCase);
    /// <summary>Traders, other characters and birds that are no creatures of the game's (no Character).</summary>
    private static readonly Regex Npc = new(@"^(Haldor|Hildir|BogWitch|odin|Valkyrie_End|Crow|AshCrow|Seagal|LuredWisp|LuredFaderEmber)$", RegexOptions.IgnoreCase);
    /// <summary>Effects and markers: mist, clouds, music, boss event zones, location proxies, debris effects.</summary>
    private static readonly Regex Effect = new(@"^vfx_|^vx_|^fx_|_destruction$|^Music_|Music$|^eventzone_|^_|^LocationProxy$|^mistvolume|^marker\d|^Flies$|^HouseFire$|^WaterLiquid$|Reveal$", RegexOptions.IgnoreCase);
    /// <summary>Bones, skulls, carcasses, ragdolls and the giants' remains.</summary>
    private static readonly Regex Remain = new(@"ragdoll|^giant_|bone|ribs|skull|^dead_|carrion|^Frozen(Skeleton|GD)|^TrollFrost_", RegexOptions.IgnoreCase);
    /// <summary>Furniture and props ("table" but not in "Unstable" or "plantable").</summary>
    private static readonly Regex Prop = new(@"^prop_|props_|^Prop|chair|(?<![sn])table|stool|bench|bed(roll)?\d?$|^bed|rug|^bucket$|chest|_pot\d|lantern|brazier|banner|curtain|^cloth_|hanging|fenrirhide|dummy|WeaponStand|crate|barrel|web|^CreepProp_|cup$|trashpile|strawpile|_pile$|wood_stack|cauldron|Tankard|Feast|UpgradeStation|wagon|demister|holder|_Chain$|WoodBoards|^digg", RegexOptions.IgnoreCase);
    /// <summary>Players' tombstones, kept out of Rocks (their name has "stone").</summary>
    private static readonly Regex Tomb = new(@"tombstone", RegexOptions.IgnoreCase);
    /// <summary>Mineable rocks and deposits: copper, tin, silver, obsidian, mud piles, tar...</summary>
    private static readonly Regex Ore = new(@"minerock|copper|tin|silver|vein|obsidian|mudpile|guck|leviathan|tar|flametal", RegexOptions.IgnoreCase);
    /// <summary>Rocks, stones, ice, boulders, cliffs, pillars and snow walls.</summary>
    private static readonly Regex Rock = new(@"rock|stone|ice|boulder|cliff|pillar|^WallSnow", RegexOptions.IgnoreCase);
    /// <summary>Bushes, shrubs, ferns, vines and cloudberries.</summary>
    private static readonly Regex Bush = new(@"bush|shrub|fern|ormbunke|vine|cloudberry", RegexOptions.IgnoreCase);

    /// <summary>The kind of an object.</summary>
    /// <param name="name">Its prefab name; null makes it <see cref="ObjectKind.Other"/>.</param>
    /// <param name="builtPiece">
    /// A building piece a player placed (it has a builder); the same piece without one is part of a ruin.
    /// </param>
    /// <param name="tamed">A creature a player tamed (wild ones are <see cref="ObjectKind.Creatures"/>).</param>
    public static ObjectKind Of(string? name, bool builtPiece, bool tamed = false)
    {
        if (builtPiece)
        {
            return ObjectKind.Buildings;
        }
        if (tamed)
        {
            return ObjectKind.Animals;
        }
        if (name == null)
        {
            return ObjectKind.Other;
        }
        if (PrefabCatalog.IsRunestoneName(name))
        {
            return ObjectKind.Runestones;
        }
        // A dungeon (its rooms are drawn from its data, see Dungeons) and the rooms themselves.
        if (name.StartsWith("DG_", StringComparison.Ordinal) || Editing.Dungeons.RoomOf(name) != null)
        {
            return ObjectKind.Dungeons;
        }

        // Spawners by what the game's data says of them, else by name.
        if (PrefabCatalog.SpawnOf(name) is { } spawn)
        {
            return spawn.Nest ? ObjectKind.Nests : ObjectKind.SpawnPoints;
        }
        if (Nest.IsMatch(name))
        {
            return ObjectKind.Nests;
        }
        if (SpawnPoint.IsMatch(name))
        {
            return ObjectKind.SpawnPoints;
        }
        if (PrefabCatalog.IsCreature(StableHash.Of(name)) || Npc.IsMatch(name))
        {
            return ObjectKind.Creatures;
        }
        if (Effect.IsMatch(name))
        {
            return ObjectKind.Effects;
        }

        if (PieceCatalog.Has(name) || Ruin.IsMatch(name))
        {
            return ObjectKind.Ruins;
        }

        if (Pickable.IsMatch(name))
        {
            return ObjectKind.Pickables;
        }

        if (Remain.IsMatch(name))
        {
            return ObjectKind.Remains;
        }

        if (Prop.IsMatch(name))
        {
            return ObjectKind.Furniture;
        }

        if (Tree.IsMatch(name))
        {
            return ObjectKind.Trees;
        }

        if (Structure.IsMatch(name))
        {
            return ObjectKind.Ruins;
        }

        if (Tomb.IsMatch(name))
        {
            return ObjectKind.Other;
        }

        if (Ore.IsMatch(name))
        {
            return ObjectKind.Ore;
        }

        if (Rock.IsMatch(name))
        {
            return ObjectKind.Rocks;
        }

        if (Bush.IsMatch(name))
        {
            return ObjectKind.Bushes;
        }

        return ObjectKind.Other;
    }
}
