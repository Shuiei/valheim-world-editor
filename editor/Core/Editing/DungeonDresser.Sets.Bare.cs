
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// A nearly bare room: one piece on its axis (a hero piece when one is left, else the style's lone thing: a
    /// sarcophagus, a statue, a totem, a table, a dead fire), a single light by it, cobwebs, a few bones; the dark
    /// elsewhere, a foe or two.
    /// </summary>
    private void Bare(Frame f)
    {
        float v = Chance(0.5) ? 0 : f.HalfD - 2.2f;
        if (!PutHero(f, 0, v, 180))
        {
            string lone = _style.Name switch
            {
                "Crypt" or "Catacombs" => One("crypt_skeleton_chest", "StatueEvil", "MountainGraveStone01", "bone_stack"),
                "Temple" => One("StatueFreya", "StatueThor", "StatueSeed"),
                "Goblin warren" => One("goblin_totempole", "lox_ribs", "goblin_strawpile"),
                "Dvergr hold" => One("dvergrprops_crate_long", "dvergrprops_table", "blackmarble_altar_crystal"),
                _ => One("piece_table", "Morkhalla_firepit", "StatueThor_broken_bottom", "piece_logbench01"),
            };
            Put(f, lone, 0, v, lone == "crypt_skeleton_chest" ? 90 : 180 + R(-15, 15), 1.2f, force: true);
        }
        if (Chance(0.6))
        {
            Brazier(f, R(-1, 1) + 2.2f, v - 0.4f);
        }
        else
        {
            Put(f, TorchOf(Local(f, 5)), -2.2f, v + 0.6f, 0, 0.4f);
        }
        Webs(f, 0.5f);
        Clutter(f, 0.006f);
        Foes(f, 0.4f);
        Decay();
    }

    /// <summary>
    /// A dead end's cache at its far end: the biome's chest against the end wall between a skull or a candle and
    /// a brazier, loot lying about; sometimes a guard.
    /// </summary>
    private void Cache(Frame f)
    {
        // Forced: a short dead end lies wholly within its doorway's clear space.
        float v = f.HalfD - 0.9f;
        Put(f, _b.Chest, 0, v, 180, 0.8f, force: true);
        Put(f, _s.Respawn ? "skull_pile" : "Candle_resin", -1.0f, v + 0.1f, R(0, 360), 0.3f, force: true);
        Put(f, _b.Brazier, 1.1f, v - 0.3f, 0, 0.6f, force: true);
        if (Chance(0.5))
        {
            Cores(f, 1);
        }
        Pickables(f, 1.2f);
        if (Chance(0.5))
        {
            Elite(f, 0, v - 2.4f, 0);
        }
    }
}
