namespace TerrainEditor.Editing;

/// <summary>
/// Facts about the props generated dungeons use, from the game's models: how far a prop's position is
/// above its foot, which way it faces, how it hangs on a wall; and the game's way of writing a chest's
/// contents.
/// </summary>
internal static class DungeonProps
{
    /// <summary>Position above foot (metres), where it is not 0: the lowest point of the model, negated.</summary>
    private static readonly Dictionary<string, float> Feet = new()
    {
        ["Ashlands_Pillar4_tip_broken1"] = 1.01f,
        ["Beehive"] = 0.74f,
        ["BlackIceShard_01"] = 0.32f,
        ["BlackIceShard_02"] = 0.51f,
        ["BonePileSpawner"] = 0.06f,
        ["BonePileSpawner_swamp"] = 0.06f,
        ["CastleKit_brazier"] = 1.06f,
        ["CastleKit_groundtorch"] = 0.05f,
        ["CastleKit_groundtorch_blue"] = 0.05f,
        ["CastleKit_groundtorch_green"] = 0.05f,
        ["CastleKit_groundtorch_unlit"] = 0.65f,
        ["CreepProp_entrance1"] = 1.74f,
        ["FrozenSkeleton_Pose2"] = 0.03f,
        ["GlowingMushroom"] = 0.19f,
        ["Goblin_Gem"] = 0.54f,
        ["GraveStone_Broken_World"] = 0.37f,
        ["GuckSack_small"] = 0.93f,
        ["LargeBone"] = 0.91f,
        ["LargeBone_half01"] = 0.51f,
        ["MemorialStone_Large"] = 0.33f,
        ["MemorialStone_Medium"] = 0.20f,
        ["Morkhalla_Bedroll1"] = 0.13f,
        ["Morkhalla_Bedroll2"] = 0.10f,
        ["Morkhalla_Rubble1"] = 0.32f,
        ["Morkhalla_Rubble2"] = 0.17f,
        ["Morkhalla_Rubble3"] = 0.22f,
        ["Morkhalla_Rubble4"] = 0.30f,
        ["Morkhalla_StatuePieceArmL"] = -4.57f,
        ["Morkhalla_StatuePieceFace"] = -6.15f,
        ["Morkhalla_StatuePieceFeet"] = 0.04f,
        ["Morkhalla_StatuePieceTorso"] = -4.37f,
        ["Morkhalla_StatueSword"] = 0.25f,
        ["Morkhalla_Stool"] = 0.03f,
        ["Morkhalla_WoodBoards"] = 0.13f,
        ["Morkhalla_coal_pile_memorial"] = 0.23f,
        ["Morkhalla_giant_railing_deco"] = 0.50f,
        ["Morkhalla_giant_railing_torch"] = 0.50f,
        ["Morkhalla_rubble_trashpile"] = 0.27f,
        ["MountainGraveStone01"] = 0.48f,
        ["MountainKit_brazier"] = 1.06f,
        ["MountainKit_brazier_blue"] = 1.06f,
        ["MountainKit_brazier_purple"] = 1.06f,
        ["Pickable_Barley"] = 0.11f,
        ["Pickable_BlackCoreStand"] = 0.06f,
        ["Pickable_Charredskull"] = 0.07f,
        ["Pickable_DvergrMineTreasure"] = -0.04f,
        ["Pickable_ForestCryptRemains01"] = 0.24f,
        ["Pickable_ForestCryptRemains02"] = 0.09f,
        ["Pickable_ForestCryptRemains04"] = 0.14f,
        ["Pickable_MeatPile"] = 0.22f,
        ["Pickable_MoltenCoreStand"] = 0.04f,
        ["Pickable_MountainCaveCrystal"] = 0.31f,
        ["Pickable_MountainCaveObsidian"] = 0.44f,
        ["Pickable_StoneRock"] = 0.11f,
        ["Pickable_SurtlingCoreStand"] = 0.04f,
        ["Pickable_Tar"] = 0.34f,
        ["RockDolmen_1"] = 0.32f,
        ["RockDolmen_2"] = 0.28f,
        ["RockDolmen_3"] = 0.41f,
        ["StatueEvil"] = 0.50f,
        ["StatueSeed"] = 0.32f,
        ["StatueThor_broken_top"] = -0.44f,
        ["WallSnow2"] = 0.79f,
        ["WallSnow3"] = 0.79f,
        ["barrell"] = 0.58f,
        ["blackmarble_altar_crystal"] = 0.90f,
        ["bone_stack"] = 0.06f,
        ["caverock_ice_stalagmite"] = 0.89f,
        ["deepnorth_lantern_standing"] = 0.22f,
        ["dvergrprops_crate_long"] = 0.09f,
        ["dvergrprops_pickaxe"] = 0.13f,
        ["dvergrtown_wood_crane"] = 3.00f,
        ["dvergrtown_wood_stakewall"] = 0.80f,
        ["elaking_trashpile"] = 0.36f,
        ["goblin_banner"] = 0.13f,
        ["goblin_strawpile"] = -0.11f,
        ["goblin_trashpile"] = 0.13f,
        ["highstone"] = 0.86f,
        ["highstone_2"] = 0.86f,
        ["lox_ribs"] = 0.08f,
        ["mudpile"] = 0.07f,
        ["offeraltar_FrozenKing_bossroom"] = 0.03f,
        ["piece_Charred_Balista"] = 0.13f,
        ["prop_ashwood_bed"] = 0.08f,
        ["prop_bonfire"] = 0.50f,
        ["prop_cauldron_ext6_rollingpins"] = 0.17f,
        ["prop_hearth"] = 0.05f,
        ["prop_piece_cookingstation"] = 0.18f,
        ["prop_piece_workbench_ext2"] = 0.04f,
        ["prop_piece_workbench_ext3"] = 0.36f,
        ["prop_piece_workbench_ext4"] = 0.37f,
        ["prop_preptable"] = 0.03f,
        ["prop_wood_stack"] = 0.57f,
        ["root07"] = 0.86f,
        ["root08"] = 0.84f,
        ["skull_pile"] = 0.07f,
        ["stone_wall_1x1_ruin"] = 0.58f,
        ["stone_wall_2x1_ruin"] = 0.58f,
        ["veg_skull_Ashlands"] = 0.06f,
        ["widestone"] = 0.44f,
        ["widestone_2"] = 0.44f,
    };

    /// <summary>How far the prop's position is above its foot, in metres (0 for props not listed).</summary>
    public static float Foot(string prefab) => Feet.GetValueOrDefault(prefab);

    /// <summary>
    /// Turned this much more (degrees), so its front faces where it is turned to.
    /// The models' fronts are +z (seats' backs, beds' heads at -z) but for these, measured from them.
    /// </summary>
    private static readonly Dictionary<string, float> Turns = new()
    {
        ["dvergrprops_shelf"] = 180,
        ["prop_piece_workbench_ext2"] = 180,
    };

    /// <summary>The extra turn (degrees) the prop needs so its front faces where it is turned to; 0 for most.</summary>
    public static float Turn(string prefab) => Turns.GetValueOrDefault(prefab);

    /// <summary>
    /// How a hanging is put on a wall in a room <paramref name="h"/> metres high: its position above the floor,
    /// how far in from the wall's face, and its turn from facing into the room.
    /// </summary>
    /// <returns>Null when the prop is not a known hanging or does not fit a room that high.</returns>
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

    /// <summary>A chest's contents as the game writes them (the inspector's own writer).</summary>
    /// <param name="items">Item prefabs and stack sizes, laid out four to a row in order.</param>
    public static byte[] Inventory(IReadOnlyList<(string Item, int Stack)> items) =>
        App.ObjectData.BuildInventory([.. items.Select((it, i) => new App.ItemUpload(it.Item, null, it.Stack, X: i % 4, Y: i / 4))]).Write();
}
