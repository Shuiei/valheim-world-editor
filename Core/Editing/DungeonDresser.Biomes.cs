namespace TerrainEditor.Editing;

/// <summary>
/// What each biome's own dungeons are full of, from the whole of the game's prefabs (measured through the editor's
/// models): the growths on their walls, floors and roofs (guck sacks and roots in the swamp, ice and snow in the
/// mountains, seeker creep and eggs in the mines, charred skulls and spikes in the Ashlands, frost cores and drifts in
/// the north), their standing lights, their chests, the cores on stands, and the pieces worth standing once.
/// </summary>
internal sealed partial class DungeonDresser
{
    /// <summary>A growth: what, where it clings (a wall, the floor, the roof), its height above the floor on a wall or below the roof, how much room it keeps, and how often.</summary>
    private sealed record Growth(string Prefab, char On, float Height, float R, float Weight);

    /// <summary>The biome's growths (On: 'w' a wall at Height up, 'f' the floor by a wall, 'c' a corner of the floor, 'r' the roof, hanging Height down).</summary>
    private Growth[] BiomeGrowths => _b.Name switch
    {
        "Meadows" or "Black Forest" =>
        [
            new("vines", 'w', 1.6f, 0, 2), new("root11", 'f', 0, 0.9f, 1), new("root07", 'f', 0, 0.5f, 1),
            new("Pickable_SurtlingCoreStand", 'c', 0, 0.3f, 0.7f), new("Beehive", 'r', 0.9f, 0, 0.3f),
        ],
        "Swamp" =>
        [
            new("GuckSack_small", 'w', 2.6f, 0, 2), new("GuckSack", 'r', 2.3f, 0, 1), new("root08", 'f', 0, 1.6f, 0.8f), new("root12", 'f', 0, 1.2f, 1),
            new("vines", 'w', 1.6f, 0, 1.5f), new("mudpile", 'c', 0, 1.6f, 1.2f), new("Pickable_Tar", 'f', 0, 0.5f, 0.6f),
        ],
        "Mountain" =>
        [
            new("caverock_ice_stalagmite", 'c', 0, 0.8f, 1.5f), new("caverock_ice_stalagtite", 'r', 3.4f, 0, 2), new("BlackIceShard_01", 'f', 0, 0.3f, 1.5f),
            new("BlackIceShard_02", 'c', 0, 0.9f, 0.6f), new("WallSnow3", 'c', 0, 1.3f, 1.2f), new("WallSnow2", 'c', 0, 1.6f, 0.8f),
            new("Pickable_MountainCaveCrystal", 'f', 0, 0.4f, 1.5f), new("Pickable_MountainCaveObsidian", 'f', 0, 0.6f, 0.8f),
            new("Pickable_MeatPile", 'c', 0, 0.8f, 0.6f), new("dead_deer", 'w', 0, 0.6f, 0.5f), new("Pickable_Hairstrands02", 'r', 3.1f, 0, 0.8f), new("Pickable_Hairstrands01", 'r', 2.1f, 0, 0.8f),
        ],
        "Plains" =>
        [
            new("goblin_trashpile", 'c', 0, 1.4f, 1), new("lox_ribs", 'f', 0, 0.9f, 0.8f), new("Pickable_Barley", 'f', 0, 0.2f, 0.5f), new("vines", 'w', 1.6f, 0, 0.5f),
        ],
        "Mistlands" =>
        [
            new("CreepProp_wall01", 'w', 1.6f, 0, 2), new("CreepProp_hanging01", 'r', 2.3f, 0, 1.2f), new("CreepProp_egg_hanging01", 'r', 2.8f, 0, 0.8f),
            new("CreepProp_egg_hanging02", 'r', 2.6f, 0, 0.8f), new("CreepProp_entrance1", 'c', 0, 1.6f, 0.8f), new("SeekerEgg", 'f', 0, 0.3f, 1),
            new("Pickable_RoyalJelly", 'c', 0, 1.3f, 0.8f), new("Pickable_DvergrLantern", 'f', 0, 0.2f, 0.6f), new("GlowingMushroom", 'f', 0, 0.3f, 0.6f),
        ],
        "Ashlands" =>
        [
            new("veg_skull_Ashlands", 'f', 0, 0.15f, 1.5f), new("Pickable_Charredskull", 'f', 0, 0.2f, 1), new("Pickable_Ashstone", 'f', 0, 0.3f, 0.8f),
            new("Morkhalla_rubble_trashpile", 'c', 0, 1.7f, 0.8f), new("Ashlands_Fortress_Wall_Spikes", 'w', 1.9f, 0, 0.8f), new("Morkhalla_Eye1", 'w', 1.9f, 0, 0.5f),
            new("Morkhalla_Eye3", 'w', 1.9f, 0, 0.5f), new("Morkhalla_WoodBoards", 'f', 0, 1.2f, 0.5f), new("VineAsh", 'w', 1.6f, 0, 0.8f),
        ],
        "Deep North" =>
        [
            new("WallSnow3", 'c', 0, 1.3f, 1.5f), new("WallSnow2", 'c', 0, 1.6f, 1), new("BlackIceShard_01", 'f', 0, 0.3f, 1.2f), new("BlackIceShard_02", 'c', 0, 0.9f, 0.6f),
            new("Pickable_FrostCoreHanger", 'w', 1.5f, 0, 1), new("Morkhalla_Eye2", 'w', 1.9f, 0, 0.5f), new("Morkhalla_Eye4", 'w', 1.9f, 0, 0.5f),
            new("FrozenSkeleton_Pose2", 'c', 0, 0.7f, 0.6f), new("Pickable_MeatPile", 'c', 0, 0.8f, 0.4f), new("caverock_ice_stalagtite", 'r', 3.4f, 0, 1),
        ],
        _ => [],
    };

    /// <summary>The biome's standing lights, one kind per room: its kit torch, the hammer's torch of its colour, its lanterns.</summary>
    private string[] BiomeLamps => _b.Name switch
    {
        "Meadows" => ["CastleKit_groundtorch", "piece_groundtorch_wood", "piece_groundtorch"],
        "Black Forest" => ["CastleKit_groundtorch_green", "piece_groundtorch_wood", "CastleKit_groundtorch"],
        "Swamp" => ["CastleKit_groundtorch_green", "piece_groundtorch_green"],
        "Mountain" => ["CastleKit_groundtorch_blue", "piece_groundtorch_blue", "piece_snowlantern"],
        "Plains" => ["CastleKit_groundtorch", "piece_groundtorch_wood", "piece_groundtorch"],
        "Mistlands" => ["dvergrprops_lantern_standing", "piece_groundtorch_mist", "Pickable_DvergrLantern"],
        "Ashlands" => ["Morkhalla_giant_railing_torch", "piece_Lavalantern", "piece_groundtorch"],
        "Deep North" => ["deepnorth_lantern_standing", "piece_snowlantern", "piece_groundtorch_blue"],
        _ => [_b.Torch],
    };

    /// <summary>
    /// The room's standing light, chosen once per room from the biome's (by rnd, the room's light stream): the kit's
    /// torch most often; a castle torch is now and then unlit in a decayed dungeon.
    /// </summary>
    private string Lamp(Random rnd)
    {
        string[] lamps = BiomeLamps;
        string lamp = rnd.NextDouble() < 0.55 ? lamps[0] : lamps[rnd.Next(lamps.Length)];
        return _decay > 0 && lamp.StartsWith("CastleKit_groundtorch", StringComparison.Ordinal) && rnd.NextDouble() < _decay * 0.5 ? "CastleKit_groundtorch_unlit" : lamp;
    }

    /// <summary>The biome's chests (all containers), for caches, treasuries and hidden stores: the kit's first.</summary>
    private string[] BiomeChests => _b.Name switch
    {
        "Meadows" => ["TreasureChest_meadows", "stonechest", "piece_chest_treasure"],
        "Black Forest" => ["TreasureChest_forestcrypt", "TreasureChest_blackforest", "stonechest", "TreasureChest_trollcave"],
        "Swamp" => ["TreasureChest_sunkencrypt", "TreasureChest_swamp", "stonechest"],
        "Mountain" => ["TreasureChest_mountaincave", "TreasureChest_mountains", "piece_chest_private"],
        "Plains" => ["TreasureChest_plains_stone", "TreasureChest_heath", "piece_chest_treasure"],
        "Mistlands" => ["TreasureChest_dvergrtown", "TreasureChest_dvergrtower", "TreasureChest_dvergr_loose_stone", "piece_chest_blackmetal"],
        "Ashlands" => ["TreasureChest_charredfortress", "TreasureChest_ashland_stone", "piece_chest_grausten", "Morkhalla_ChestAncient"],
        "Deep North" => ["TreasureChest_deepnorth_village", "Morkhalla_ChestAncient", "piece_chest_grausten"],
        _ => [_b.Chest],
    };
}
