using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    // ---- Hero pieces.
    /// <summary>
    /// The pieces a style's big room may be remembered by, each standing once in a dungeon: the fallen giant's statue
    /// in parts, a dolmen, a grave slab, a giant's skull or ribs in a warren, an altar of crystal in a hold, a cart in
    /// a keep. Null when the style's are all placed.
    /// </summary>
    private string? Hero()
    {
        // Each measured through the editor's models: nothing here is wider than 7 m or taller than a low room,
        // save the few marked for tall rooms.
        bool tall = _tallHero;
        string[] pool = _style.Name switch
        {
            "Crypt" or "Catacombs" => ["RockDolmen_1", "RockDolmen_2", "Morkhalla_StatuePieceFace", "GraveStone_Broken_World", "Morkhalla_StatuePieceTorso", "Morkhalla_coal_pile_memorial", "MemorialStone_Large", "stonechest", .. tall ? new[] { "highstone", "MemorialStone_Large" } : []],
            "Ruins" => ["Morkhalla_StatuePieceFace", "Morkhalla_StatuePieceTorso", "Morkhalla_StatuePieceFeet", "Morkhalla_StatuePieceArmL", "RockDolmen_3", "Ashlands_Pillar4_tip_broken1", "Ashlands_Ruins_twist_PillarBase", "Morkhalla_Rubble4", .. tall ? new[] { "Ashlands_Boss_Pillar" } : []],
            "Temple" => ["blackmarble_altar_crystal", "Morkhalla_StatuePieceFace", "RockDolmen_1", "Morkhalla_StatueSword", "piece_EternalPyre", "MemorialStone_Large", "widestone", .. tall ? new[] { "highstone" } : []],
            "Fortress" or "Prison" => ["Cart", "Morkhalla_StatueSword", "Morkhalla_StatuePieceSword", "Morkhalla_Trainingdummy2", "Sled", .. tall ? new[] { "piece_Charred_Balista" } : []],
            "Goblin warren" => ["Goblin_Gem", "piece_asksvinskeleton", "lox_ribs", "elaking_trashpile"],
            "Dvergr hold" => ["blackmarble_altar_crystal", "Pickable_BlackCoreStand", "blackmarble_head_big01", "blackmarble_head_big02", "piece_wisplure", "eitrrefinery", .. tall ? new[] { "dvergrtown_wood_crane" } : []],
            _ => ["RockDolmen_1", "Morkhalla_StatuePieceFace", "MemorialStone_Medium"],
        };
        var left = pool.Where(p => !_heroes.Contains(p)).ToList();
        if (left.Count == 0)
        {
            return null;
        }
        string hero = left[_rnd.Next(left.Count)];
        _heroes.Add(hero);
        return hero;
    }

    /// <summary>A hero piece at (u, v), turned yaw, keeping r metres; false when none is left or the spot is taken.</summary>
    private bool PutHero(Frame f, float u, float v, float yaw, float r = 2.2f)
    {
        _tallHero = f.S.Tall;
        if (!f.Free(u, v, r) || Hero() is not { } hero)
        {
            return false;
        }
        return Put(f, hero, u, v, yaw, r, force: true);
    }

    /// <summary>Whether the room a hero piece is being chosen for is tall (the tallest pieces only there).</summary>
    private bool _tallHero;

    // ---- Bare rooms and dead ends.
    /// <summary>
    /// Whether this room is left nearly bare: one thing in it and little else (a pacing beat between the full
    /// rooms, as the best dungeons keep a few). Never the rooms the way depends on, nor the key chest's.
    /// </summary>
    private bool Sparse(Frame f) =>
        f.S.Room is Room.Hall or Room.Gallery or Room.Burial or Room.Ossuary or Room.Tomb or Room.Store or Room.Den or Room.Barracks or Room.Hub or Room.Quarters
        && !f.S.KeyChest && Chance(0.08);
}
