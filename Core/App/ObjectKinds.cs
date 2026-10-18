using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

/// <summary>Sorting objects into <see cref="ObjectKind"/>s, with their labels for the View panel.</summary>
public static partial class ObjectKinds
{
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
        int generation = GameData.Generation;
        Memo memo = _memo;
        if (memo.Generation != generation)
        {
            memo = new Memo(generation, new(StringComparer.Ordinal));
            _memo = memo;
        }
        return memo.Kinds.GetOrAdd(name, Find);
    }

    /// <summary>The kinds found by name, for the game data of one <see cref="GameData.Generation"/>.</summary>
    /// <param name="Generation">The game data's generation they were found with.</param>
    /// <param name="Kinds">The kind of each name asked.</param>
    private sealed record Memo(int Generation, ConcurrentDictionary<string, ObjectKind> Kinds);

    /// <summary>The kinds found so far; started again when the game data changes.</summary>
    private static volatile Memo _memo = new(-1, new(StringComparer.Ordinal));

    /// <summary>How many names this thread sorted by their rules rather than from those found before (the tests).</summary>
    [field: ThreadStatic]
    internal static int Finds { get; private set; }

    /// <summary>Name rules for the kinds found by name, checked in this order; the first that matches wins.</summary>
    private static readonly (Func<string, bool> Matches, ObjectKind Kind)[] NameRules =
    [
        (Nest.IsMatch, ObjectKind.Nests),
        (SpawnPoint.IsMatch, ObjectKind.SpawnPoints),
        (IsCreatureName, ObjectKind.Creatures),
        (Effect.IsMatch, ObjectKind.Effects),
        (IsPieceName, ObjectKind.Ruins),
        (Pickable.IsMatch, ObjectKind.Pickables),
        (Remain.IsMatch, ObjectKind.Remains),
        (Prop.IsMatch, ObjectKind.Furniture),
        (Tree.IsMatch, ObjectKind.Trees),
        (Structure.IsMatch, ObjectKind.Ruins),
        (Tomb.IsMatch, ObjectKind.Other),
        (Ore.IsMatch, ObjectKind.Ore),
        (Rock.IsMatch, ObjectKind.Rocks),
        (Bush.IsMatch, ObjectKind.Bushes),
    ];

    /// <summary>The kind of an object that is neither a placed piece nor tamed, by its prefab name.</summary>
    private static ObjectKind Find(string name)
    {
        Finds++;
        if (PrefabCatalog.IsRunestoneName(name))
        {
            return ObjectKind.Runestones;
        }
        if (IsDungeonName(name))
        {
            return ObjectKind.Dungeons;
        }
        if (SpawnKindOf(name) is { } spawnKind)
        {
            return spawnKind;
        }
        foreach ((Func<string, bool> matches, ObjectKind kind) in NameRules)
        {
            if (matches(name))
            {
                return kind;
            }
        }
        return ObjectKind.Other;
    }

    /// <summary>Whether a name is a dungeon (DG_ objects) or one of its rooms.</summary>
    private static bool IsDungeonName(string name) => name.StartsWith("DG_", StringComparison.Ordinal) || Editing.Dungeons.RoomOf(name) != null;

    /// <summary>The kind of a spawner the game's data describes (nest or spawn point); null when it is none.</summary>
    private static ObjectKind? SpawnKindOf(string name)
    {
        if (PrefabCatalog.SpawnOf(name) is { } spawn)
        {
            return spawn.Nest ? ObjectKind.Nests : ObjectKind.SpawnPoints;
        }
        return null;
    }

    /// <summary>Whether a name is a creature of the game or one of the traders, characters and birds.</summary>
    private static bool IsCreatureName(string name) => PrefabCatalog.IsCreature(StableHash.Of(name)) || Npc.IsMatch(name);

    /// <summary>Whether a name is a building piece without a builder, or one of the game's structures.</summary>
    private static bool IsPieceName(string name) => PieceCatalog.Has(name) || Ruin.IsMatch(name);
}
