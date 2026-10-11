namespace TerrainEditor.Editing;

/// <summary>
/// What generated dungeons are built from: wall materials, biome palettes (lights, monsters, loot,
/// clutter) and styles (which rooms a crypt, a keep or a Dvergr hold has, and how they are laid out).
/// Every name is a prefab of the game; the tests check they all exist. Sizes come from the game's models
/// (metres; Bottom: how far a piece's foot is below its position).
/// </summary>
public static class DungeonKit
{
    /// <summary>A wall piece.</summary>
    /// <param name="Prefab">The piece's prefab name.</param>
    /// <param name="W">How wide it is along the wall (metres).</param>
    /// <param name="H">How high it is (metres).</param>
    /// <param name="Bottom">How far its foot is below its position (metres).</param>
    public sealed record Part(string Prefab, float W, float H, float Bottom);

    /// <summary>A pillar or post segment, stacked to the height wanted.</summary>
    /// <param name="Prefab">The segment's prefab name.</param>
    /// <param name="H">How high one segment is (metres).</param>
    /// <param name="Bottom">How far its foot is below its position (metres).</param>
    public sealed record Column(string Prefab, float H, float Bottom);

    /// <summary>A wall material and the pieces that go with it.</summary>
    /// <param name="Name">As picked in the settings (matched ignoring case).</param>
    /// <param name="Walls">Pieces largest first.</param>
    /// <param name="Thick">How deep they are (metres).</param>
    /// <param name="Column">Inside rooms.</param>
    /// <param name="Post">At corners (none for thick walls, which meet anyway).</param>
    /// <param name="Arch">Over an open doorway (2 m wide, 1 m high).</param>
    /// <param name="Floor">A 2 m finish over the marble (none: the marble is the floor).</param>
    /// <param name="FloorTop">How far the finish's top is above the marble (metres).</param>
    /// <param name="Beam">Under the ceiling, 4 m.</param>
    /// <param name="BeamTop">How far the beam's position is below the ceiling (metres).</param>
    public sealed record Material(string Name, Part[] Walls, float Thick, Column[] Column, Column? Post, string? Arch, string? Floor, float FloorTop,
        string? Beam, float BeamTop);

    /// <summary>The wall materials; the first (Stone) is the fallback.</summary>
    public static readonly Material[] Materials =
    [
        new("Stone", [new("stone_wall_4x2", 4, 2, 1), new("stone_wall_2x1", 2, 1, 0.5f), new("stone_wall_1x1", 1, 1, 0.5f)], 1,
            [new("stone_pillar", 2, 1)], null, null, null, 0, null, 0),
        new("Marble", [new("blackmarble_tile_wall_2x4", 2, 4, 0), new("blackmarble_tile_wall_2x2", 2, 2, 0), new("blackmarble_tile_wall_1x1", 1, 1, 0)], 0.2f,
            [new("blackmarble_column_3", 8, 4), new("blackmarble_tip", 2, 1.02f)], null, "blackmarble_arch", "blackmarble_tile_floor_2x2", 0.19f, null, 0),
        new("Wood", [new("woodwall", 2, 2, 1), new("wood_wall_half", 2, 1, 0.5f), new("wood_wall_quarter", 1, 1, 0)], 0.3f,
            [new("wood_pole_log_4", 4.44f, 2.22f)], new("wood_pole_log_4", 4.44f, 2.22f), null, "wood_floor", 0.1f, "darkwood_beam4x4", 0.46f),
        new("Stave", [new("stave_wall_2x2", 2, 2, 1), new("wood_wall_half", 2, 1, 0.5f), new("wood_wall_quarter", 1, 1, 0)], 0.7f,
            [new("stave_pole_4m", 4.09f, 1.02f)], new("stave_pole_4m", 4.09f, 1.02f), null, "wood_floor", 0.1f, "stave_beam_4m", 0.36f),
        new("Grausten", [new("Piece_grausten_wall_4x2", 4, 2, 0), new("Piece_grausten_wall_2x2", 2, 2, 0), new("Piece_grausten_wall_1x2", 1, 2, 0)], 0.4f,
            [new("Ashlands_Pillar4", 4, 0)], null, "Piece_grausten_wall_arch", null, 0, null, 0),
        new("Dvergr", [new("piece_dvergr_metal_wall_2x2", 2, 2, 1), new("iron_wall_1x1", 1, 1, 0.09f)], 0.1f,
            [new("dvergrprops_wood_pole", 4, 2)], new("piece_dvergr_pole", 2, 2), null, "dvergrprops_wood_floor", 0.13f, "dvergrtown_wood_beam", 0.46f),
        new("Goblin", [new("goblin_woodwall_2m", 2, 2, 0), new("goblin_woodwall_1m", 1, 2, 0)], 0.34f,
            [new("goblin_pole", 4.95f, 0.49f)], new("goblin_pole_small", 4.95f, 0.49f), null, null, 0, null, 0),
    ];

    /// <summary>The material of that name (ignoring case), or Stone when there is none.</summary>
    public static Material MaterialOf(string? name) => Materials.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Materials[0];

    /// <summary>A monster.</summary>
    /// <param name="Spawner">Its spawner (comes back).</param>
    /// <param name="Creature">The creature itself (placed once, can have stars).</param>
    public sealed record Foe(string Spawner, string Creature);

    /// <summary>A biome's dungeon palette.</summary>
    /// <param name="Name">The biome's name (matched ignoring case).</param>
    /// <param name="Material">Its walls when none is picked.</param>
    /// <param name="Style">Its style when none is picked.</param>
    /// <param name="Torch">A light standing by walls.</param>
    /// <param name="Brazier">A brazier (its position is its top: BrazierBottom below).</param>
    /// <param name="BrazierBottom">How far the brazier's foot is below its position (metres).</param>
    /// <param name="Flame">A light on top of pillars.</param>
    /// <param name="Foes">The usual monsters.</param>
    /// <param name="Elites">Guards, wardens.</param>
    /// <param name="Boss">The boss of the arena.</param>
    /// <param name="Chest">Its treasure chest.</param>
    /// <param name="Pickable">Loot lying about.</param>
    /// <param name="Bars">The metal stacks of a treasury (taken by deconstructing).</param>
    /// <param name="Clutter">On the ground.</param>
    /// <param name="Hanging">On the walls.</param>
    /// <param name="Loot">Items for the key chest (name, most of a stack).</param>
    public sealed record Biome(string Name, string Material, string Style, string Torch, string Brazier, float BrazierBottom, string Flame,
        Foe[] Foes, Foe[] Elites, Foe Boss, string Chest, string Pickable, string Bars, string[] Clutter, string[] Hanging, (string Item, int Max)[] Loot);

    /// <summary>The biomes' palettes; the second (Black Forest) is the fallback.</summary>
    public static readonly Biome[] Biomes =
    [
        new("Meadows", "Stone", "Crypt", "CastleKit_groundtorch", "CastleKit_brazier", 1.06f, "Morkhalla_giant_railing_torch",
            [new("Spawner_Skeleton_Meadows", "Skeleton_Meadows"), new("Spawner_Skeleton", "Skeleton"), new("Spawner_Ghost", "Ghost")],
            [new("Spawner_Greydwarf_Elite", "Greydwarf_Elite")], new("Spawner_Skeleton_hildir", "Skeleton_Hildir"),
            "TreasureChest_meadows", "Pickable_DolmenTreasure", "bar_copper_stack",
            ["Pickable_ForestCryptRemains01", "Pickable_ForestCryptRemains02", "Skull1", "LargeBone", "piece_pot1_cracked", "piece_pot3_cracked"],
            ["cloth_hanging_door", "Morkhalla_WallChain1"], [("Coins", 60), ("Amber", 4), ("Flint", 10), ("LeatherScraps", 10)]),
        new("Black Forest", "Stone", "Crypt", "CastleKit_groundtorch_green", "CastleKit_brazier", 1.06f, "Morkhalla_giant_railing_torch",
            [new("Spawner_Skeleton", "Skeleton"), new("Spawner_Greydwarf", "Greydwarf"), new("Spawner_Greydwarf_Shaman", "Greydwarf_Shaman"), new("Spawner_Ghost", "Ghost")],
            [new("Spawner_Greydwarf_Elite", "Greydwarf_Elite"), new("Spawner_Skeleton", "Skeleton_NoArcher")], new("Spawner_Troll", "Troll"),
            "TreasureChest_forestcrypt", "Pickable_ForestCryptRandom", "bar_bronze_stack",
            ["Pickable_ForestCryptRemains01", "Pickable_ForestCryptRemains02", "Pickable_ForestCryptRemains03", "Skull1", "LargeBone", "LargeBone_half01", "piece_pot2_cracked"],
            ["cloth_hanging_door", "Morkhalla_WallChain1", "hanging_hairstrands"], [("Coins", 120), ("Amber", 6), ("AmberPearl", 3), ("Ruby", 2), ("SurtlingCore", 3)]),
        new("Swamp", "Stone", "Crypt", "CastleKit_groundtorch_green", "MountainKit_brazier", 1.06f, "Morkhalla_giant_railing_torch",
            [new("Spawner_Draugr", "Draugr"), new("Spawner_Draugr_Ranged", "Draugr_Ranged"), new("Spawner_Skeleton_poison", "Skeleton_Poison"), new("Spawner_Blob", "Blob")],
            [new("Spawner_Draugr_Elite", "Draugr_Elite"), new("Spawner_Wraith", "Wraith")], new("Spawner_Draugr_Elite", "Draugr_Elite"),
            "TreasureChest_sunkencrypt", "Pickable_SunkenCryptRandom", "bar_iron_stack",
            ["Pickable_ForestCryptRemains02", "Pickable_ForestCryptRemains04", "Skull1", "LargeBone", "lox_ribs", "piece_pot1_cracked", "piece_pot2_cracked"],
            ["Morkhalla_WallChain1", "hanging_hairstrands", "cloth_hanging_door"], [("Coins", 200), ("Amber", 8), ("AmberPearl", 5), ("Ruby", 4), ("Iron", 6)]),
        new("Mountain", "Stone", "Fortress", "CastleKit_groundtorch_blue", "MountainKit_brazier_blue", 1.06f, "Morkhalla_giant_railing_torch",
            [new("Spawner_Skeleton_Mountains", "Skeleton_Mountains"), new("Spawner_Ulv", "Ulv"), new("Spawner_Cultist", "Fenring_Cultist"), new("Spawner_Bat", "Bat")],
            [new("Spawner_Fenring", "Fenring"), new("Spawner_Cultist", "Fenring_Cultist")], new("Spawner_StoneGolem", "StoneGolem"),
            "TreasureChest_mountaincave", "Pickable_MountainCaveRandom", "bar_silver_stack",
            ["lox_ribs", "Skull1", "LargeBone", "Pickable_MountainCaveCrystal", "piece_pot3_cracked"],
            ["fenrirhide_hanging", "Morkhalla_WallChain1", "hanging_hairstrands"], [("Coins", 300), ("Ruby", 6), ("Silver", 6), ("Obsidian", 10), ("FreezeGland", 4)]),
        new("Plains", "Goblin", "Goblin warren", "CastleKit_groundtorch", "CastleKit_brazier", 1.06f, "Morkhalla_giant_railing_torch",
            [new("Spawner_Goblin", "Goblin"), new("Spawner_GoblinArcher", "GoblinArcher"), new("Spawner_GoblinShaman", "GoblinShaman")],
            [new("Spawner_GoblinBrute", "GoblinBrute")], new("Spawner_GoblinBrute", "GoblinBrute"),
            "TreasureChest_plains_stone", "Pickable_DolmenTreasure", "bar_blackmetal_stack",
            ["goblin_trashpile", "goblin_strawpile", "lox_ribs", "Skull1", "LargeBone", "piece_pot3_cracked"],
            ["goblin_banner", "hanging_hairstrands"], [("Coins", 400), ("BlackMetalScrap", 10), ("Barley", 20), ("Flax", 20), ("GoblinTotem", 2)]),
        new("Mistlands", "Dvergr", "Dvergr hold", "dvergrprops_lantern_standing", "MountainKit_brazier_purple", 1.06f, "Morkhalla_giant_railing_torch",
            [new("Spawner_Seeker", "Seeker"), new("Spawner_Tick", "Tick"), new("Spawner_Seeker", "Seeker")],
            [new("Spawner_SeekerBrute", "SeekerBrute")], new("Spawner_SeekerBrute", "SeekerBrute"),
            "TreasureChest_dvergrtown", "Pickable_DvergrMineTreasure", "bar_iron_stack",
            ["dvergrprops_crate", "dvergrprops_barrel", "dvergrprops_pickaxe", "Skull1", "LargeBone"],
            ["dvergrprops_banner", "dvergrprops_curtain"], [("Coins", 400), ("Softtissue", 10), ("BlackCore", 2), ("DvergrNeedle", 6), ("Copper", 10)]),
        new("Ashlands", "Grausten", "Fortress", "Morkhalla_giant_railing_torch", "CastleKit_brazier", 1.06f, "Morkhalla_giant_railing_torch",
            [new("Spawner_Charred", "Charred_Melee"), new("Spawner_Charred_Archer", "Charred_Archer"), new("Spawner_Charred_Mage", "Charred_Mage")],
            [new("Spawner_FallenValkyrie", "FallenValkyrie"), new("Spawner_Morgen", "Morgen_NonSleeping")], new("Spawner_Charred_Dyrnwyn", "Charred_Melee_Dyrnwyn"),
            "TreasureChest_charredfortress", "Pickable_MorkHallaTreasure", "bar_flametal_stack",
            ["Morkhalla_Rubble2", "ashland_pot1_red", "ashland_pot2_red", "ashland_pot3_green", "Skull1", "LargeBone"],
            ["CharredBanner3", "Morkhalla_WallChain1"], [("Coins", 500), ("CharredBone", 10), ("Grausten", 20), ("MoltenCore", 2), ("FlametalOreNew", 6)]),
        new("Deep North", "Stave", "Temple", "deepnorth_lantern_standing", "MountainKit_brazier_blue", 1.06f, "Morkhalla_giant_railing_torch",
            [new("Spawner_JotunWarrior", "JotunWarrior"), new("Spawner_JotunWitch", "JotunWitch"), new("Spawner_Frysling", "Frysling"), new("Spawner_Skeleton", "Skeleton_DeepNorth")],
            [new("Spawner_JotunWarrior", "JotunWarrior")], new("Spawner_JotunDualWield", "JotunWarrior"),
            "TreasureChest_deepnorth_village", "Pickable_DolmenTreasure", "bar_gold_stack",
            ["FrozenSkeleton_Pose1", "FrozenSkeleton_Pose2", "Skull1", "LargeBone", "piece_pot3_cracked"],
            ["fenrirhide_hanging", "cloth_hanging_door"], [("Coins", 600), ("Ruby", 8), ("AmberPearl", 8)]),
    ];

    /// <summary>The biome of that name (ignoring case), or Black Forest when there is none.</summary>
    public static Biome BiomeOf(string? name) => Biomes.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Biomes[1];

    /// <summary>
    /// The kinds of room. Their purpose decides their size, their furniture, who guards them and what
    /// they hold.
    /// </summary>
    public enum Room
    {
        /// <summary>The entrance hall, where the way in starts.</summary>
        Entrance,
        /// <summary>A great hall; may rise through two floors.</summary>
        Hall,
        /// <summary>A crossing, where ways branch.</summary>
        Hub,
        /// <summary>A long, narrow gallery.</summary>
        Gallery,
        /// <summary>A burial chamber.</summary>
        Burial,
        /// <summary>An ossuary.</summary>
        Ossuary,
        /// <summary>A chapel; may rise through two floors.</summary>
        Chapel,
        /// <summary>A shrine.</summary>
        Shrine,
        /// <summary>A tomb.</summary>
        Tomb,
        /// <summary>The throne room; at most one, may rise through two floors.</summary>
        Throne,
        /// <summary>Barracks.</summary>
        Barracks,
        /// <summary>An armory.</summary>
        Armory,
        /// <summary>The feast hall; at most one.</summary>
        Feast,
        /// <summary>The kitchen; at most one.</summary>
        Kitchen,
        /// <summary>A storeroom.</summary>
        Store,
        /// <summary>The forge; at most one.</summary>
        Forge,
        /// <summary>A study.</summary>
        Library,
        /// <summary>A cell block.</summary>
        Cells,
        /// <summary>A torture chamber.</summary>
        Torture,
        /// <summary>A guard room.</summary>
        Guard,
        /// <summary>The treasury, past the boss; at most one.</summary>
        Treasury,
        /// <summary>The boss's arena, past the antechamber; at most one.</summary>
        Boss,
        /// <summary>The antechamber before the boss's arena; at most one.</summary>
        Antechamber,
        /// <summary>A den.</summary>
        Den,
        /// <summary>A totem hall.</summary>
        Totem,
        /// <summary>A mine gallery.</summary>
        Mine,
        /// <summary>A workshop.</summary>
        Workshop,
        /// <summary>Living quarters.</summary>
        Quarters,
        /// <summary>A hidden cache behind a secret door.</summary>
        Secret,
        /// <summary>The top of a stairwell, going down to the next level.</summary>
        StairTop,
        /// <summary>The foot of a stairwell, on the level below.</summary>
        StairBottom,
    }

    /// <summary>A kind of room's name and size.</summary>
    /// <param name="Room">The kind.</param>
    /// <param name="Name">As shown on the plan's map and in the notes.</param>
    /// <param name="WMin">Fewest tiles across (2 m tiles, perpendicular to the way in).</param>
    /// <param name="WMax">Most tiles across.</param>
    /// <param name="DMin">Fewest tiles deep (along the way in).</param>
    /// <param name="DMax">Most tiles deep.</param>
    /// <param name="Tall">May rise through two floors.</param>
    /// <param name="Unique">At most once a dungeon.</param>
    public sealed record RoomSpec(Room Room, string Name, int WMin, int WMax, int DMin, int DMax, bool Tall = false, bool Unique = false);

    /// <summary>Every kind of room's spec, one each.</summary>
    public static readonly RoomSpec[] Rooms =
    [
        new(Room.Entrance, "Entrance hall", 6, 8, 6, 8),
        new(Room.Hall, "Great hall", 6, 10, 10, 16, Tall: true),
        new(Room.Hub, "Crossing", 6, 8, 6, 8),
        new(Room.Gallery, "Gallery", 4, 4, 10, 16),
        new(Room.Burial, "Burial chamber", 6, 8, 6, 10),
        new(Room.Ossuary, "Ossuary", 4, 6, 6, 8),
        new(Room.Chapel, "Chapel", 6, 8, 8, 12, Tall: true),
        new(Room.Shrine, "Shrine", 6, 6, 6, 6),
        new(Room.Tomb, "Tomb", 6, 6, 6, 8),
        new(Room.Throne, "Throne room", 8, 10, 10, 14, Tall: true, Unique: true),
        new(Room.Barracks, "Barracks", 6, 8, 6, 8),
        new(Room.Armory, "Armory", 4, 6, 6, 6),
        new(Room.Feast, "Feast hall", 6, 8, 10, 12, Unique: true),
        new(Room.Kitchen, "Kitchen", 6, 6, 6, 6, Unique: true),
        new(Room.Store, "Storeroom", 4, 6, 4, 6),
        new(Room.Forge, "Forge", 6, 6, 6, 6, Unique: true),
        new(Room.Library, "Study", 6, 6, 6, 8),
        new(Room.Cells, "Cell block", 8, 8, 8, 12),
        new(Room.Torture, "Torture chamber", 6, 6, 6, 6),
        new(Room.Guard, "Guard room", 4, 6, 4, 6),
        new(Room.Treasury, "Treasury", 6, 6, 6, 6, Unique: true),
        new(Room.Boss, "Arena", 12, 12, 12, 12, Tall: true, Unique: true),
        new(Room.Antechamber, "Antechamber", 6, 6, 4, 4, Unique: true),
        new(Room.Den, "Den", 6, 8, 6, 8),
        new(Room.Totem, "Totem hall", 6, 8, 6, 8),
        new(Room.Mine, "Mine gallery", 4, 4, 10, 14),
        new(Room.Workshop, "Workshop", 6, 6, 6, 6),
        new(Room.Quarters, "Quarters", 6, 6, 6, 8),
        new(Room.Secret, "Hidden cache", 4, 4, 4, 4),
        new(Room.StairTop, "Stairs down", 4, 4, 12, 12),
        new(Room.StairBottom, "Stairs foot", 4, 4, 8, 8),
    ];

    /// <summary>The spec of a kind of room.</summary>
    public static RoomSpec SpecOf(Room r) => Rooms.First(s => s.Room == r);

    /// <summary>
    /// The kinds of doorway: Open (an arch), Wood (a crypt's wooden door), Gate (iron bars), Grand (two
    /// tall gates, 4 m), Grate (a cell's bars), Curtain (cloth, to cut through), Key (needs the crypt key),
    /// Secret (a cracked wall to break, or a tapestry hiding the way).
    /// </summary>
    public enum Door
    {
        /// <summary>An open arch.</summary>
        Open,
        /// <summary>A crypt's wooden door.</summary>
        Wood,
        /// <summary>Iron bars.</summary>
        Gate,
        /// <summary>Two tall gates, 4 m.</summary>
        Grand,
        /// <summary>A cell's bars.</summary>
        Grate,
        /// <summary>Cloth, to cut through.</summary>
        Curtain,
        /// <summary>A door that needs the crypt key.</summary>
        Key,
        /// <summary>A cracked wall to break, or a tapestry hiding the way.</summary>
        Secret,
    }

    /// <summary>A dungeon style: which rooms it has and how they are laid out.</summary>
    /// <param name="Name">As picked in the settings (matched ignoring case).</param>
    /// <param name="Main">The rooms along the way through a level, in order.</param>
    /// <param name="Side">The rooms off it (weights: how often, any number).</param>
    /// <param name="Symmetry">How often a side room gets a twin on the other side (0 to 1).</param>
    /// <param name="CorridorMin">Shortest way between rooms (tiles; 0: wall to wall).</param>
    /// <param name="CorridorMax">Longest way between rooms (tiles).</param>
    /// <param name="Doors">What stands in doorways (weights: how often).</param>
    /// <param name="TallChance">How often a room that may rise through two floors does (0 to 1).</param>
    /// <param name="Decay">How ruined it is when the settings do not say (0 kept to 1 ruined).</param>
    public sealed record Style(string Name, Room[] Main, (Room Room, float Weight)[] Side, float Symmetry, int CorridorMin, int CorridorMax,
        (Door Door, float Weight)[] Doors, float TallChance, float Decay);

    /// <summary>The styles; the first (Crypt) is the fallback.</summary>
    public static readonly Style[] Styles =
    [
        new("Crypt", [Room.Hall, Room.Hub, Room.Gallery, Room.Chapel],
            [(Room.Burial, 3f), (Room.Ossuary, 2f), (Room.Tomb, 2f), (Room.Shrine, 1f), (Room.Chapel, 0.7f), (Room.Store, 0.5f), (Room.Guard, 0.7f)],
            0.75f, 1, 4, [(Door.Wood, 5f), (Door.Gate, 4f), (Door.Open, 1.5f), (Door.Curtain, 0.5f)], 0.6f, 0.15f),
        new("Catacombs", [Room.Gallery, Room.Hub, Room.Gallery, Room.Ossuary],
            [(Room.Burial, 2f), (Room.Ossuary, 3f), (Room.Tomb, 1.5f), (Room.Shrine, 0.7f), (Room.Gallery, 1f)],
            0.3f, 2, 7, [(Door.Gate, 3f), (Door.Open, 3f), (Door.Wood, 2f), (Door.Curtain, 1f)], 0.2f, 0.3f),
        new("Temple", [Room.Hall, Room.Hub, Room.Chapel],
            [(Room.Chapel, 2f), (Room.Shrine, 2f), (Room.Library, 1.5f), (Room.Quarters, 1f), (Room.Store, 0.7f), (Room.Tomb, 1f)],
            0.9f, 1, 3, [(Door.Wood, 3f), (Door.Grand, 2f), (Door.Open, 2f), (Door.Curtain, 1f)], 0.8f, 0.1f),
        new("Fortress", [Room.Guard, Room.Hall, Room.Hub, Room.Throne],
            [ (Room.Barracks, 2.5f), (Room.Armory, 1.5f), (Room.Feast, 1f), (Room.Kitchen, 1f), (Room.Store, 2f), (Room.Forge, 1f), (Room.Library, 0.7f),
                (Room.Quarters, 1f), (Room.Cells, 0.8f), (Room.Guard, 1f) ],
            0.5f, 1, 4, [(Door.Wood, 4f), (Door.Grand, 1.5f), (Door.Gate, 2f), (Door.Open, 1f)], 0.5f, 0.1f),
        new("Prison", [Room.Guard, Room.Gallery, Room.Hub],
            [(Room.Cells, 4f), (Room.Torture, 2f), (Room.Guard, 1.5f), (Room.Barracks, 1f), (Room.Feast, 0.7f), (Room.Store, 1f), (Room.Kitchen, 0.5f)],
            0.4f, 1, 5, [(Door.Gate, 5f), (Door.Wood, 2f), (Door.Grate, 1f)], 0.2f, 0.2f),
        new("Dvergr hold", [Room.Mine, Room.Hub, Room.Hall],
            [(Room.Workshop, 2f), (Room.Store, 2f), (Room.Quarters, 2f), (Room.Forge, 1f), (Room.Library, 1f), (Room.Feast, 0.8f), (Room.Mine, 1f)],
            0.4f, 1, 5, [(Door.Gate, 3f), (Door.Open, 3f), (Door.Curtain, 1f)], 0.5f, 0.15f),
        new("Goblin warren", [Room.Den, Room.Hub, Room.Totem],
            [(Room.Den, 3f), (Room.Totem, 1.5f), (Room.Store, 1.5f), (Room.Feast, 1f), (Room.Cells, 0.7f)],
            0.2f, 1, 5, [(Door.Open, 4f), (Door.Curtain, 2f), (Door.Gate, 1f)], 0.3f, 0.35f),
        new("Ruins", [Room.Hall, Room.Hub, Room.Gallery, Room.Chapel],
            [(Room.Burial, 1.5f), (Room.Ossuary, 1f), (Room.Barracks, 1f), (Room.Store, 1.5f), (Room.Shrine, 1f), (Room.Library, 1f), (Room.Feast, 0.5f)],
            0.5f, 1, 6, [(Door.Open, 4f), (Door.Gate, 1.5f), (Door.Wood, 1.5f)], 0.4f, 0.7f),
    ];

    /// <summary>The style of that name (ignoring case), or Crypt when there is none.</summary>
    public static Style StyleOf(string? name) => Styles.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Styles[0];

    /// <summary>First words for a dungeon's name of this style (on the sign at its entrance), as in "Barrow of ...".</summary>
    public static string[] NameStarts(string style) => style switch
    {
        "Crypt" => ["Barrow", "Crypt", "Tomb", "Halls"],
        "Catacombs" => ["Catacombs", "Barrow", "Deep", "Ossuary"],
        "Temple" => ["Sanctum", "Temple", "Halls", "Shrine"],
        "Fortress" => ["Keep", "Hold", "Halls", "Fort"],
        "Prison" => ["Gaol", "Pit", "Dungeons", "Cells"],
        "Dvergr hold" => ["Hold", "Deep", "Vault", "Delve"],
        "Goblin warren" => ["Warren", "Pit", "Den", "Lair"],
        _ => ["Ruins", "Halls", "Vault", "Deep"],
    };

    /// <summary>Last words for a dungeon's name, after "of": a person or a dark thing.</summary>
    public static readonly string[] NameEnds =
    [
        "Hrafnkel", "the Drowned King", "Sigrun", "the Grey Jarl", "Ulfhild", "Bones", "Ash and Iron", "the Forgotten", "Thorgrim", "the Nine Oaths",
        "Eyvind", "Ragnfrid", "the Hollow Crown", "Svartulf", "Black Waters", "the Last Watch", "Hjalmar", "Gunnhild", "the Long Night", "Kettil",
    ];
}
