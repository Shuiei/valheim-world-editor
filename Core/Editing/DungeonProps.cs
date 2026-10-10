namespace TerrainEditor.Editing;

// Facts about the props generated dungeons use, from the game's models: how far a prop's position is
// above its foot, which way it faces, how it hangs on a wall; and the game's way of writing a chest's
// contents.
internal static class DungeonProps
{
	// Position above foot (metres), where it is not 0: the lowest point of the model, negated.
	private static readonly Dictionary<string, float> Feet = new()
	{
		["BonePileSpawner"] = 0.06f,
		["BonePileSpawner_swamp"] = 0.06f,
		["CastleKit_brazier"] = 1.06f,
		["CastleKit_groundtorch"] = 0.05f,
		["CastleKit_groundtorch_blue"] = 0.05f,
		["CastleKit_groundtorch_green"] = 0.05f,
		["CastleKit_groundtorch_unlit"] = 0.65f,
		["LargeBone"] = 0.91f,
		["LargeBone_half01"] = 0.51f,
		["Morkhalla_Rubble1"] = 0.32f,
		["Morkhalla_Rubble2"] = 0.17f,
		["Morkhalla_giant_railing_deco"] = 0.50f,
		["Morkhalla_giant_railing_torch"] = 0.50f,
		["MountainGraveStone01"] = 0.48f,
		["MountainKit_brazier"] = 1.06f,
		["MountainKit_brazier_blue"] = 1.06f,
		["MountainKit_brazier_purple"] = 1.06f,
		["Pickable_ForestCryptRemains01"] = 0.24f,
		["Pickable_ForestCryptRemains02"] = 0.09f,
		["Pickable_ForestCryptRemains04"] = 0.14f,
		["Pickable_MountainCaveCrystal"] = 0.31f,
		["StatueEvil"] = 0.50f,
		["StatueSeed"] = 0.32f,
		["StatueThor_broken_top"] = -0.44f,
		["barrell"] = 0.58f,
		["blackmarble_altar_crystal"] = 0.90f,
		["bone_stack"] = 0.06f,
		["deepnorth_lantern_standing"] = 0.22f,
		["dvergrprops_crate_long"] = 0.09f,
		["dvergrprops_pickaxe"] = 0.13f,
		["goblin_banner"] = 0.13f,
		["goblin_strawpile"] = -0.11f,
		["goblin_trashpile"] = 0.13f,
		["lox_ribs"] = 0.08f,
		["prop_ashwood_bed"] = 0.08f,
		["prop_cauldron_ext6_rollingpins"] = 0.17f,
		["prop_hearth"] = 0.05f,
		["prop_piece_cookingstation"] = 0.18f,
		["prop_piece_workbench_ext2"] = 0.04f,
		["prop_piece_workbench_ext3"] = 0.36f,
		["prop_piece_workbench_ext4"] = 0.37f,
		["prop_wood_stack"] = 0.57f,
		["skull_pile"] = 0.07f,
		["stone_wall_1x1_ruin"] = 0.58f,
		["stone_wall_2x1_ruin"] = 0.58f,
	};

	public static float Foot(string prefab) => Feet.GetValueOrDefault(prefab);

	// Turned this much more (degrees), so its front faces where it is turned to.
	// The models' fronts are +z (seats' backs, beds' heads at -z) but for these, measured from them.
	private static readonly Dictionary<string, float> Turns = new()
	{
		["dvergrprops_shelf"] = 180,
		["prop_piece_workbench_ext2"] = 180,
	};

	public static float Turn(string prefab) => Turns.GetValueOrDefault(prefab);

	// How a hanging is put on a wall in a room h metres high: its position above the floor, how far in
	// from the wall's face, and its turn from facing into the room. None: it does not fit.
	public static (float Y, float Inward, float Turn)? Hanging(string prefab, float h) => prefab switch
	{
		_ when prefab.StartsWith("piece_banner", StringComparison.Ordinal) => (h - 0.1f, 0.2f, 90),
		"cloth_hanging_door" => (-0.25f, 0.12f, 0),
		"cloth_hanging_long" => h >= 8 ? (-0.25f, 0.1f, 0) : null,
		"Morkhalla_WallChain1" => (3.5f, 1.0f, 0),
		"hanging_hairstrands" => (2.4f, 0.2f, 0),
		"CharredBanner3" => (-0.2f, 0.12f, 0),
		"dvergrprops_banner" => (1.95f, 0.2f, 90),
		"dvergrprops_curtain" => (h, 0.2f, 90),
		"fenrirhide_hanging" => (h >= 6 ? 0.29f : -0.45f, 0.3f, 0),
		"goblin_banner" => (0.13f, 0.6f, 0),
		_ => null,
	};

	// A chest's contents as the game writes them (the inspector's own writer).
	public static byte[] Inventory(IReadOnlyList<(string Item, int Stack)> items) =>
		App.ObjectData.BuildInventory(items.Select((it, i) => new App.ItemUpload(it.Item, null, it.Stack, X: i % 4, Y: i / 4)).ToList()).Write();
}
