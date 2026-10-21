namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>Quarters: two beds with a wardrobe and a table; or one bed in a corner, a rug, a bath and a chest.</summary>
    private void Quarters(Frame f)
    {
        if (Chance(0.5))
        {
            Put(f, Bed, -(f.HalfA - 1.2f), f.HalfD - 1.6f, 90, 1f);
            Put(f, Bed, f.HalfA - 1.2f, f.HalfD - 1.6f, -90, 1f);
            Put(f, "prop_chest_warderobe", 0, f.HalfD - 0.9f, 180, 0.9f);
            Put(f, One("rug_wolf", "rug_fur", "rug_deer"), 0, -0.5f, 0, 0, force: true);
            Put(f, "dvergrprops_table", 0, -0.5f, 0, 1.1f);
            Put(f, "dvergrprops_chair", -1.1f, -0.5f, 90, 0.4f);
            Put(f, "dvergrprops_chair", 1.1f, -0.5f, -90, 0.4f);
        }
        else
        {
            Put(f, Bed, -(f.HalfA - 1.2f), f.HalfD - 1.2f, 90, 1f);
            Put(f, "piece_chest_wood", -(f.HalfA - 1.2f), f.HalfD - 3.1f, 90, 0.5f);
            Put(f, One("jute_carpet", "jute_carpet_blue", "rug_fur"), 0.8f, 0, 90, 0, force: true);
            Put(f, "piece_table_round", 1.2f, -0.5f, 0, 0.9f);
            Put(f, "piece_chair", 1.2f, -1.6f, 0, 0.3f);
            Put(f, "piece_hoodedlantern", 1.2f, -0.5f, 0, 0, y: 0.82f, force: true);
            Put(f, "prop_chest_warderobe", f.HalfA - 0.9f, f.HalfD - 1.2f, -90, 0.9f);
            if (_mat.Name is not ("Goblin" or "Dvergr"))
            {
                Put(f, "piece_bathtub", f.HalfA - 1.6f, -(f.HalfD - 2), -90, 1.6f);
            }
        }
        Trophies(f, 1);
        if (Chance(0.5))
        {
            ChestAgainstWall(f);
        }
        Clusters(f, 1, "scribe", "camp");
        Lights();
        Foes(f, 0.6f);
        Clutter(f, 0.01f);
        Decay();
    }
}
