using System.Text.RegularExpressions;
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
    /// Building pieces without a builder and the game's own structures (goblin camps, wrecks, statues...).
    /// </summary>
    Ruins,
    /// <summary>Trees, stumps, logs, roots and saplings.</summary>
    Trees,
    /// <summary>Rocks, stones, ice, boulders, cliffs and pillars.</summary>
    Rocks,
    /// <summary>Mineable rocks and deposits: copper, tin, silver, obsidian, mud piles, tar...</summary>
    Ore,
    /// <summary>Bushes, shrubs, ferns, vines and cloudberries.</summary>
    Bushes,
    /// <summary>Things picked by hand: Pickable_ prefabs, mushrooms, dandelions, thistles.</summary>
    Pickables,
    /// <summary>Creatures a player tamed; wild ones are <see cref="Other"/>.</summary>
    Animals,
    /// <summary>Runestones (by the prefab catalogue's runestone names).</summary>
    Runestones,
    /// <summary>Dungeons (DG_ objects) and their rooms.</summary>
    Dungeons,
    /// <summary>Everything else, tombstones and wild creatures among them.</summary>
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
        ObjectKind.Trees => "Trees & logs",
        ObjectKind.Rocks => "Rocks",
        ObjectKind.Ore => "Ore & deposits",
        ObjectKind.Bushes => "Bushes & shrubs",
        ObjectKind.Pickables => "Pickables",
        ObjectKind.Animals => "Tamed animals",
        ObjectKind.Runestones => "Runestones",
        ObjectKind.Dungeons => "Dungeon rooms",
        _ => "Other objects",
    };

    /// <summary>Shown when the editor opens, as in the web editor's View panel (spoilers stay hidden).</summary>
    public static bool ShownAtFirst(ObjectKind k) => k is ObjectKind.Buildings or ObjectKind.Trees or ObjectKind.Rocks or ObjectKind.Animals or ObjectKind.Runestones or ObjectKind.Dungeons;

    /// <summary>The game's structures: goblin camps, wrecks, statues, dungeon parts, chests...</summary>
    private static readonly Regex Ruin = new(@"^goblin_|^shipwreck_|^Statue|^BossStone_|^dungeon_|crypt_gate|^CastleKit_|^StartPlatform|^Beehive|^CargoCrate|^barrell|^RockDolmen|^TreasureChest", RegexOptions.IgnoreCase);
    /// <summary>Things picked by hand: Pickable_ prefabs, mushrooms, dandelions, thistles.</summary>
    private static readonly Regex Pickable = new(@"^Pickable_|^Pickable|mushroom|Dandelion|Thistle", RegexOptions.IgnoreCase);
    /// <summary>Trees, stumps, logs, roots and saplings.</summary>
    private static readonly Regex Tree = new(@"tree|beech|birch|oak|^fir|pine|stub|log|root|yggdrasil|ashwood|sapling", RegexOptions.IgnoreCase);
    /// <summary>Players' tombstones, kept out of Rocks (their name has "stone").</summary>
    private static readonly Regex Tomb = new(@"tombstone", RegexOptions.IgnoreCase);
    /// <summary>Mineable rocks and deposits: copper, tin, silver, obsidian, mud piles, tar...</summary>
    private static readonly Regex Ore = new(@"minerock|copper|tin|silver|vein|obsidian|mudpile|guck|leviathan|tar|flametal", RegexOptions.IgnoreCase);
    /// <summary>Rocks, stones, ice, boulders, cliffs and pillars.</summary>
    private static readonly Regex Rock = new(@"rock|stone|ice|boulder|cliff|pillar", RegexOptions.IgnoreCase);
    /// <summary>Bushes, shrubs, ferns, vines and cloudberries.</summary>
    private static readonly Regex Bush = new(@"bush|shrub|fern|ormbunke|vines|cloudberry", RegexOptions.IgnoreCase);

    /// <summary>The kind of an object.</summary>
    /// <param name="name">Its prefab name; null makes it <see cref="ObjectKind.Other"/>.</param>
    /// <param name="builtPiece">
    /// A building piece a player placed (it has a builder); the same piece without one is part of a ruin.
    /// </param>
    /// <param name="tamed">A creature a player tamed (wild ones stay in Other objects).</param>
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

        if (PieceCatalog.Has(name) || Ruin.IsMatch(name))
        {
            return ObjectKind.Ruins;
        }

        if (Pickable.IsMatch(name))
        {
            return ObjectKind.Pickables;
        }

        if (Tree.IsMatch(name))
        {
            return ObjectKind.Trees;
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
