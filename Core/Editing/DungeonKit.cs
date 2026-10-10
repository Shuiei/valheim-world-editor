namespace TerrainEditor.Editing;

// What generated dungeons are built from: wall materials, biome palettes (lights, monsters, loot,
// clutter) and styles (which rooms a crypt, a keep or a Dvergr hold has, and how they are laid out).
// Every name is a prefab of the game; the tests check they all exist. Sizes come from the game's models
// (metres; Bottom: how far a piece's foot is below its position).
public static class DungeonKit
{
	// A wall piece: W wide (along the wall), H high.
	public sealed record Part(string Prefab, float W, float H, float Bottom);

	// A pillar or post segment, stacked to the height wanted.
	public sealed record Column(string Prefab, float H, float Bottom);

	// Walls: pieces largest first; Thick: how deep they are; Column: inside rooms; Post: at corners
	// (none for thick walls, which meet anyway); Arch: over an open doorway (2 m wide, 1 m high);
	// Floor: a 2 m finish over the marble (none: the marble is the floor); Beam: under the ceiling, 4 m.
	public sealed record Material(string Name, Part[] Walls, float Thick, Column[] Column, Column? Post, string? Arch, string? Floor, float FloorTop,
		string? Beam, float BeamTop);

	public static readonly Material[] Materials =
	{
		new("Stone", new Part[] { new("stone_wall_4x2", 4, 2, 1), new("stone_wall_2x1", 2, 1, 0.5f), new("stone_wall_1x1", 1, 1, 0.5f) }, 1,
			new Column[] { new("stone_pillar", 2, 1) }, null, null, null, 0, null, 0),
		new("Marble", new Part[] { new("blackmarble_tile_wall_2x4", 2, 4, 0), new("blackmarble_tile_wall_2x2", 2, 2, 0), new("blackmarble_tile_wall_1x1", 1, 1, 0) }, 0.2f,
			new Column[] { new("blackmarble_column_3", 8, 4), new("blackmarble_tip", 2, 1.02f) }, null, "blackmarble_arch", "blackmarble_tile_floor_2x2", 0.19f, null, 0),
		new("Wood", new Part[] { new("woodwall", 2, 2, 1), new("wood_wall_half", 2, 1, 0.5f), new("wood_wall_quarter", 1, 1, 0) }, 0.3f,
			new Column[] { new("wood_pole_log_4", 4.44f, 2.22f) }, new("wood_pole_log_4", 4.44f, 2.22f), null, "wood_floor", 0.1f, "darkwood_beam4x4", 0.46f),
		new("Stave", new Part[] { new("stave_wall_2x2", 2, 2, 1), new("wood_wall_half", 2, 1, 0.5f), new("wood_wall_quarter", 1, 1, 0) }, 0.7f,
			new Column[] { new("stave_pole_4m", 4.09f, 1.02f) }, new("stave_pole_4m", 4.09f, 1.02f), null, "wood_floor", 0.1f, "stave_beam_4m", 0.36f),
		new("Grausten", new Part[] { new("Piece_grausten_wall_4x2", 4, 2, 0), new("Piece_grausten_wall_2x2", 2, 2, 0), new("Piece_grausten_wall_1x2", 1, 2, 0) }, 0.4f,
			new Column[] { new("Ashlands_Pillar4", 4, 0) }, null, "Piece_grausten_wall_arch", null, 0, null, 0),
		new("Dvergr", new Part[] { new("piece_dvergr_metal_wall_2x2", 2, 2, 1), new("iron_wall_1x1", 1, 1, 0.09f) }, 0.1f,
			new Column[] { new("dvergrprops_wood_pole", 4, 2) }, new("piece_dvergr_pole", 2, 2), null, "dvergrprops_wood_floor", 0.13f, "dvergrtown_wood_beam", 0.46f),
		new("Goblin", new Part[] { new("goblin_woodwall_2m", 2, 2, 0), new("goblin_woodwall_1m", 1, 2, 0) }, 0.34f,
			new Column[] { new("goblin_pole", 4.95f, 0.49f) }, new("goblin_pole_small", 4.95f, 0.49f), null, null, 0, null, 0),
	};

	public static Material MaterialOf(string? name) => Materials.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Materials[0];

	// A monster: its spawner (comes back) and the creature itself (placed once, can have stars).
	public sealed record Foe(string Spawner, string Creature);

	// A biome's dungeon: its walls and style when none is picked; Torch: a light standing by walls;
	// Brazier (its position is its top: Bottom below); Flame: a light on top of pillars; Foes, Elites
	// (guards, wardens) and Boss; Chest: its treasure chest, Pickable: loot lying about; Bars: the metal
	// stacks of a treasury (taken by deconstructing); Clutter: on the ground; Hanging: on the walls;
	// Loot: items for the key chest (name, most of a stack).
	public sealed record Biome(string Name, string Material, string Style, string Torch, string Brazier, float BrazierBottom, string Flame,
		Foe[] Foes, Foe[] Elites, Foe Boss, string Chest, string Pickable, string Bars, string[] Clutter, string[] Hanging, (string Item, int Max)[] Loot);

	public static readonly Biome[] Biomes =
	{
		new("Meadows", "Stone", "Crypt", "CastleKit_groundtorch", "CastleKit_brazier", 1.06f, "Morkhalla_giant_railing_torch",
			new Foe[] { new("Spawner_Skeleton_Meadows", "Skeleton_Meadows"), new("Spawner_Skeleton", "Skeleton"), new("Spawner_Ghost", "Ghost") },
			new Foe[] { new("Spawner_Greydwarf_Elite", "Greydwarf_Elite") }, new("Spawner_Skeleton_hildir", "Skeleton_Hildir"),
			"TreasureChest_meadows", "Pickable_DolmenTreasure", "bar_copper_stack",
			new[] { "Pickable_ForestCryptRemains01", "Pickable_ForestCryptRemains02", "Skull1", "LargeBone", "piece_pot1_cracked", "piece_pot3_cracked" },
			new[] { "cloth_hanging_door", "Morkhalla_WallChain1" }, new[] { ("Coins", 60), ("Amber", 4), ("Flint", 10), ("LeatherScraps", 10) }),
		new("Black Forest", "Stone", "Crypt", "CastleKit_groundtorch_green", "CastleKit_brazier", 1.06f, "Morkhalla_giant_railing_torch",
			new Foe[] { new("Spawner_Skeleton", "Skeleton"), new("Spawner_Greydwarf", "Greydwarf"), new("Spawner_Greydwarf_Shaman", "Greydwarf_Shaman"), new("Spawner_Ghost", "Ghost") },
			new Foe[] { new("Spawner_Greydwarf_Elite", "Greydwarf_Elite"), new("Spawner_Skeleton", "Skeleton_NoArcher") }, new("Spawner_Troll", "Troll"),
			"TreasureChest_forestcrypt", "Pickable_ForestCryptRandom", "bar_bronze_stack",
			new[] { "Pickable_ForestCryptRemains01", "Pickable_ForestCryptRemains02", "Pickable_ForestCryptRemains03", "Skull1", "LargeBone", "LargeBone_half01", "piece_pot2_cracked" },
			new[] { "cloth_hanging_door", "Morkhalla_WallChain1", "hanging_hairstrands" }, new[] { ("Coins", 120), ("Amber", 6), ("AmberPearl", 3), ("Ruby", 2), ("SurtlingCore", 3) }),
		new("Swamp", "Stone", "Crypt", "CastleKit_groundtorch_green", "MountainKit_brazier", 1.06f, "Morkhalla_giant_railing_torch",
			new Foe[] { new("Spawner_Draugr", "Draugr"), new("Spawner_Draugr_Ranged", "Draugr_Ranged"), new("Spawner_Skeleton_poison", "Skeleton_Poison"), new("Spawner_Blob", "Blob") },
			new Foe[] { new("Spawner_Draugr_Elite", "Draugr_Elite"), new("Spawner_Wraith", "Wraith") }, new("Spawner_Draugr_Elite", "Draugr_Elite"),
			"TreasureChest_sunkencrypt", "Pickable_SunkenCryptRandom", "bar_iron_stack",
			new[] { "Pickable_ForestCryptRemains02", "Pickable_ForestCryptRemains04", "Skull1", "LargeBone", "lox_ribs", "piece_pot1_cracked", "piece_pot2_cracked" },
			new[] { "Morkhalla_WallChain1", "hanging_hairstrands", "cloth_hanging_door" }, new[] { ("Coins", 200), ("Amber", 8), ("AmberPearl", 5), ("Ruby", 4), ("Iron", 6) }),
		new("Mountain", "Stone", "Fortress", "CastleKit_groundtorch_blue", "MountainKit_brazier_blue", 1.06f, "Morkhalla_giant_railing_torch",
			new Foe[] { new("Spawner_Skeleton_Mountains", "Skeleton_Mountains"), new("Spawner_Ulv", "Ulv"), new("Spawner_Cultist", "Fenring_Cultist"), new("Spawner_Bat", "Bat") },
			new Foe[] { new("Spawner_Fenring", "Fenring"), new("Spawner_Cultist", "Fenring_Cultist") }, new("Spawner_StoneGolem", "StoneGolem"),
			"TreasureChest_mountaincave", "Pickable_MountainCaveRandom", "bar_silver_stack",
			new[] { "lox_ribs", "Skull1", "LargeBone", "Pickable_MountainCaveCrystal", "piece_pot3_cracked" },
			new[] { "fenrirhide_hanging", "Morkhalla_WallChain1", "hanging_hairstrands" }, new[] { ("Coins", 300), ("Ruby", 6), ("Silver", 6), ("Obsidian", 10), ("FreezeGland", 4) }),
		new("Plains", "Goblin", "Goblin warren", "CastleKit_groundtorch", "CastleKit_brazier", 1.06f, "Morkhalla_giant_railing_torch",
			new Foe[] { new("Spawner_Goblin", "Goblin"), new("Spawner_GoblinArcher", "GoblinArcher"), new("Spawner_GoblinShaman", "GoblinShaman") },
			new Foe[] { new("Spawner_GoblinBrute", "GoblinBrute") }, new("Spawner_GoblinBrute", "GoblinBrute"),
			"TreasureChest_plains_stone", "Pickable_DolmenTreasure", "bar_blackmetal_stack",
			new[] { "goblin_trashpile", "goblin_strawpile", "lox_ribs", "Skull1", "LargeBone", "piece_pot3_cracked" },
			new[] { "goblin_banner", "hanging_hairstrands" }, new[] { ("Coins", 400), ("BlackMetalScrap", 10), ("Barley", 20), ("Flax", 20), ("GoblinTotem", 2) }),
		new("Mistlands", "Dvergr", "Dvergr hold", "dvergrprops_lantern_standing", "MountainKit_brazier_purple", 1.06f, "Morkhalla_giant_railing_torch",
			new Foe[] { new("Spawner_Seeker", "Seeker"), new("Spawner_Tick", "Tick"), new("Spawner_Seeker", "Seeker") },
			new Foe[] { new("Spawner_SeekerBrute", "SeekerBrute") }, new("Spawner_SeekerBrute", "SeekerBrute"),
			"TreasureChest_dvergrtown", "Pickable_DvergrMineTreasure", "bar_iron_stack",
			new[] { "dvergrprops_crate", "dvergrprops_barrel", "dvergrprops_pickaxe", "Skull1", "LargeBone" },
			new[] { "dvergrprops_banner", "dvergrprops_curtain" }, new[] { ("Coins", 400), ("Softtissue", 10), ("BlackCore", 2), ("DvergrNeedle", 6), ("Copper", 10) }),
		new("Ashlands", "Grausten", "Fortress", "Morkhalla_giant_railing_torch", "CastleKit_brazier", 1.06f, "Morkhalla_giant_railing_torch",
			new Foe[] { new("Spawner_Charred", "Charred_Melee"), new("Spawner_Charred_Archer", "Charred_Archer"), new("Spawner_Charred_Mage", "Charred_Mage") },
			new Foe[] { new("Spawner_FallenValkyrie", "FallenValkyrie"), new("Spawner_Morgen", "Morgen_NonSleeping") }, new("Spawner_Charred_Dyrnwyn", "Charred_Melee_Dyrnwyn"),
			"TreasureChest_charredfortress", "Pickable_MorkHallaTreasure", "bar_flametal_stack",
			new[] { "Morkhalla_Rubble2", "ashland_pot1_red", "ashland_pot2_red", "ashland_pot3_green", "Skull1", "LargeBone" },
			new[] { "CharredBanner3", "Morkhalla_WallChain1" }, new[] { ("Coins", 500), ("CharredBone", 10), ("Grausten", 20), ("MoltenCore", 2), ("FlametalOreNew", 6) }),
		new("Deep North", "Stave", "Temple", "deepnorth_lantern_standing", "MountainKit_brazier_blue", 1.06f, "Morkhalla_giant_railing_torch",
			new Foe[] { new("Spawner_JotunWarrior", "JotunWarrior"), new("Spawner_JotunWitch", "JotunWitch"), new("Spawner_Frysling", "Frysling"), new("Spawner_Skeleton", "Skeleton_DeepNorth") },
			new Foe[] { new("Spawner_JotunWarrior", "JotunWarrior") }, new("Spawner_JotunDualWield", "JotunWarrior"),
			"TreasureChest_deepnorth_village", "Pickable_DolmenTreasure", "bar_gold_stack",
			new[] { "FrozenSkeleton_Pose1", "FrozenSkeleton_Pose2", "Skull1", "LargeBone", "piece_pot3_cracked" },
			new[] { "fenrirhide_hanging", "cloth_hanging_door" }, new[] { ("Coins", 600), ("Ruby", 8), ("AmberPearl", 8) }),
	};

	public static Biome BiomeOf(string? name) => Biomes.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Biomes[1];

	// The kinds of room. Their purpose decides their size, their furniture, who guards them and what
	// they hold.
	public enum Room
	{
		Entrance, Hall, Hub, Gallery, Burial, Ossuary, Chapel, Shrine, Tomb, Throne, Barracks, Armory, Feast, Kitchen, Store, Forge,
		Library, Cells, Torture, Guard, Treasury, Boss, Antechamber, Den, Totem, Mine, Workshop, Quarters, Secret, StairTop, StairBottom,
	}

	// Its size in 2 m tiles: across (W) and deep (D, along the way in), each a range; Tall: may rise
	// through two floors; Unique: at most once a dungeon.
	public sealed record RoomSpec(Room Room, string Name, int WMin, int WMax, int DMin, int DMax, bool Tall = false, bool Unique = false);

	public static readonly RoomSpec[] Rooms =
	{
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
	};

	public static RoomSpec SpecOf(Room r) => Rooms.First(s => s.Room == r);

	// The kinds of doorway: Open (an arch), Wood (a crypt's wooden door), Gate (iron bars), Grand (two
	// tall gates, 4 m), Grate (a cell's bars), Curtain (cloth, to cut through), Key (needs the crypt key),
	// Secret (a cracked wall to break, or a tapestry hiding the way).
	public enum Door { Open, Wood, Gate, Grand, Grate, Curtain, Key, Secret }

	// A style: Main: the rooms along the way through a level, in order; Side: the rooms off it (weights:
	// how often, any number); Symmetry: how often a side room gets a twin on the other side; Corridor:
	// shortest and longest way between rooms (tiles; 0: wall to wall); Doors: what stands in doorways;
	// TallChance: how often a room that may rise through two floors does.
	public sealed record Style(string Name, Room[] Main, (Room Room, float Weight)[] Side, float Symmetry, int CorridorMin, int CorridorMax,
		(Door Door, float Weight)[] Doors, float TallChance, float Decay);

	public static readonly Style[] Styles =
	{
		new("Crypt", new[] { Room.Hall, Room.Hub, Room.Gallery, Room.Chapel },
			new[] { (Room.Burial, 3f), (Room.Ossuary, 2f), (Room.Tomb, 2f), (Room.Shrine, 1f), (Room.Chapel, 0.7f), (Room.Store, 0.5f), (Room.Guard, 0.7f) },
			0.75f, 1, 4, new[] { (Door.Wood, 5f), (Door.Gate, 4f), (Door.Open, 1.5f), (Door.Curtain, 0.5f) }, 0.6f, 0.15f),
		new("Catacombs", new[] { Room.Gallery, Room.Hub, Room.Gallery, Room.Ossuary },
			new[] { (Room.Burial, 2f), (Room.Ossuary, 3f), (Room.Tomb, 1.5f), (Room.Shrine, 0.7f), (Room.Gallery, 1f) },
			0.3f, 2, 7, new[] { (Door.Gate, 3f), (Door.Open, 3f), (Door.Wood, 2f), (Door.Curtain, 1f) }, 0.2f, 0.3f),
		new("Temple", new[] { Room.Hall, Room.Hub, Room.Chapel },
			new[] { (Room.Chapel, 2f), (Room.Shrine, 2f), (Room.Library, 1.5f), (Room.Quarters, 1f), (Room.Store, 0.7f), (Room.Tomb, 1f) },
			0.9f, 1, 3, new[] { (Door.Wood, 3f), (Door.Grand, 2f), (Door.Open, 2f), (Door.Curtain, 1f) }, 0.8f, 0.1f),
		new("Fortress", new[] { Room.Guard, Room.Hall, Room.Hub, Room.Throne },
			new[] { (Room.Barracks, 2.5f), (Room.Armory, 1.5f), (Room.Feast, 1f), (Room.Kitchen, 1f), (Room.Store, 2f), (Room.Forge, 1f), (Room.Library, 0.7f),
				(Room.Quarters, 1f), (Room.Cells, 0.8f), (Room.Guard, 1f) },
			0.5f, 1, 4, new[] { (Door.Wood, 4f), (Door.Grand, 1.5f), (Door.Gate, 2f), (Door.Open, 1f) }, 0.5f, 0.1f),
		new("Prison", new[] { Room.Guard, Room.Gallery, Room.Hub },
			new[] { (Room.Cells, 4f), (Room.Torture, 2f), (Room.Guard, 1.5f), (Room.Barracks, 1f), (Room.Feast, 0.7f), (Room.Store, 1f), (Room.Kitchen, 0.5f) },
			0.4f, 1, 5, new[] { (Door.Gate, 5f), (Door.Wood, 2f), (Door.Grate, 1f) }, 0.2f, 0.2f),
		new("Dvergr hold", new[] { Room.Mine, Room.Hub, Room.Hall },
			new[] { (Room.Workshop, 2f), (Room.Store, 2f), (Room.Quarters, 2f), (Room.Forge, 1f), (Room.Library, 1f), (Room.Feast, 0.8f), (Room.Mine, 1f) },
			0.4f, 1, 5, new[] { (Door.Gate, 3f), (Door.Open, 3f), (Door.Curtain, 1f) }, 0.5f, 0.15f),
		new("Goblin warren", new[] { Room.Den, Room.Hub, Room.Totem },
			new[] { (Room.Den, 3f), (Room.Totem, 1.5f), (Room.Store, 1.5f), (Room.Feast, 1f), (Room.Cells, 0.7f) },
			0.2f, 1, 5, new[] { (Door.Open, 4f), (Door.Curtain, 2f), (Door.Gate, 1f) }, 0.3f, 0.35f),
		new("Ruins", new[] { Room.Hall, Room.Hub, Room.Gallery, Room.Chapel },
			new[] { (Room.Burial, 1.5f), (Room.Ossuary, 1f), (Room.Barracks, 1f), (Room.Store, 1.5f), (Room.Shrine, 1f), (Room.Library, 1f), (Room.Feast, 0.5f) },
			0.5f, 1, 6, new[] { (Door.Open, 4f), (Door.Gate, 1.5f), (Door.Wood, 1.5f) }, 0.4f, 0.7f),
	};

	public static Style StyleOf(string? name) => Styles.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Styles[0];

	// Names for the dungeon (on the sign at its entrance).
	public static string[] NameStarts(string style) => style switch
	{
		"Crypt" => new[] { "Barrow", "Crypt", "Tomb", "Halls" },
		"Catacombs" => new[] { "Catacombs", "Barrow", "Deep", "Ossuary" },
		"Temple" => new[] { "Sanctum", "Temple", "Halls", "Shrine" },
		"Fortress" => new[] { "Keep", "Hold", "Halls", "Fort" },
		"Prison" => new[] { "Gaol", "Pit", "Dungeons", "Cells" },
		"Dvergr hold" => new[] { "Hold", "Deep", "Vault", "Delve" },
		"Goblin warren" => new[] { "Warren", "Pit", "Den", "Lair" },
		_ => new[] { "Ruins", "Halls", "Vault", "Deep" },
	};

	public static readonly string[] NameEnds =
	{
		"Hrafnkel", "the Drowned King", "Sigrun", "the Grey Jarl", "Ulfhild", "Bones", "Ash and Iron", "the Forgotten", "Thorgrim", "the Nine Oaths",
		"Eyvind", "Ragnfrid", "the Hollow Crown", "Svartulf", "Black Waters", "the Last Watch", "Hjalmar", "Gunnhild", "the Long Night", "Kettil",
	};
}
