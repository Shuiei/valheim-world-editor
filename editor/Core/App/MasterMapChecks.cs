using ValheimGen;
using K = TerrainEditor.App.MasterMap.Kind;

namespace TerrainEditor.App;

/// <summary>
/// What would not work in the world a master map draws, said before it is made: from the drawing alone (the start on
/// water or off the Meadows, a biome not drawn and what the game keeps there, no Black Forest coast for the tin), and
/// from the world the editor makes of it (the places and dungeons the game cannot lay out, or lays out few of: the
/// game's own rules, as the World view lays them out).
/// </summary>
public static class MasterMapChecks
{
    /// <summary>How near (in cells, about 30 m) Black Forest must be to water to count as its coast, where the tin is.</summary>
    private const int CoastCells = 3;

    /// <summary>Black Forest coast cells under which the tin is said to be scarce (about 1 km of coast).</summary>
    private const int ScarceTin = 100;

    /// <summary>The share of a dungeon kind's number the game lays out under which it is said to be few.</summary>
    private const float FewDungeons = 0.25f;

    /// <summary>The Fuling villages (the black metal's): the game's location kind.</summary>
    public const string FulingVillage = "GoblinCamp2";

    /// <summary>What the drawing alone tells: each a warning, in both languages; none when all is well.</summary>
    /// <param name="map">The drawing.</param>
    public static List<Said> OfMap(MasterMap map)
    {
        var said = new List<Said>();
        var (column, row) = map.Start ?? MasterMap.CellOf(0f, 0f);
        K start = map.At(column, row);
        if (MasterMap.IsWater(start))
        {
            said.Add(Said.Of(map.Start == null ? "masterMap.check.middleWater" : "masterMap.check.startWater"));
        }
        else if (start != K.Meadows)
        {
            said.Add(Said.Of(map.Start == null ? "masterMap.check.middleNotMeadows" : "masterMap.check.startNotMeadows", Lang.T(MasterMap.Legend[(int)start].Key)));
        }
        int[] counts = map.Counts();
        foreach (var e in MasterMap.Legend.Where(e => !MasterMap.IsWater(e.Kind) && e.Kind != K.DeepNorth))
        {
            if (counts[(int)e.Kind] == 0)
            {
                said.Add(Said.Of("masterMap.check.missing", Lang.T(e.Key), Lang.T(HoldsKey(e.Kind))));
            }
        }
        if (counts[(int)K.BlackForest] > 0)
        {
            int coast = Coast(map, K.BlackForest);
            if (coast == 0)
            {
                said.Add(Said.Of("masterMap.check.noTin"));
            }
            else if (coast < ScarceTin)
            {
                said.Add(Said.Of("masterMap.check.littleTin"));
            }
        }
        return said;
    }

    /// <summary>The text key of what the game keeps in a biome (its boss, trader, metal and dungeons).</summary>
    private static string HoldsKey(K kind) => kind switch
    {
        K.Meadows => "masterMap.check.holds.Meadows",
        K.BlackForest => "masterMap.check.holds.BlackForest",
        K.Swamp => "masterMap.check.holds.Swamp",
        K.Mountain => "masterMap.check.holds.Mountain",
        K.Plains => "masterMap.check.holds.Plains",
        K.Mistlands => "masterMap.check.holds.Mistlands",
        _ => "masterMap.check.holds.AshLands",
    };

    /// <summary>How many cells of a kind are within <see cref="CoastCells"/> of water (any of the legend's).</summary>
    internal static int Coast(MasterMap map, K kind)
    {
        int n = MasterMap.Size, coast = 0;
        for (int row = 0; row < n; row++)
        {
            for (int col = 0; col < n; col++)
            {
                if (map.At(col, row) == kind && NearWater(map, col, row))
                {
                    coast++;
                }
            }
        }
        return coast;
    }

    /// <summary>Whether a cell has water within <see cref="CoastCells"/>.</summary>
    private static bool NearWater(MasterMap map, int col, int row)
    {
        for (int dy = -CoastCells; dy <= CoastCells; dy++)
        {
            for (int dx = -CoastCells; dx <= CoastCells; dx++)
            {
                int x = col + dx, y = row + dy;
                if (x >= 0 && y >= 0 && x < MasterMap.Size && y < MasterMap.Size && MasterMap.IsWater(map.At(x, y)))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// What the world made of the drawing tells: the places the game cannot lay out (the start, a boss's altar, a
    /// trader), the dungeons it lays out none or few of, no Sunken Crypts (no iron), no Fuling villages (no black
    /// metal). None when all is well.
    /// </summary>
    /// <param name="placed">Every place laid out, by the game's location kind (prefab): the preview's landmarks and dungeons.</param>
    /// <param name="rules">The game's location rules (how many of each kind it lays out in a world).</param>
    public static List<Said> OfWorld(IReadOnlyCollection<(string Prefab, int Count)> placed, LocationRuleSet rules)
    {
        var said = new List<Said>();
        var count = placed.ToDictionary(p => p.Prefab, p => p.Count);
        int Of(string prefab) => count.GetValueOrDefault(prefab);
        foreach (var (name, _, prefabs) in SeedPreview.Dungeons)
        {
            int wanted = rules.Locations.Where(r => prefabs.Contains(r.Prefab) && r.Enable != 0).Sum(r => r.Quantity);
            int got = prefabs.Sum(Of);
            if (wanted == 0)
            {
                continue;
            }
            if (got == 0)
            {
                said.Add(Said.Of("masterMap.check.noDungeon", Lang.T(DungeonKey(name))));
            }
            else if (got < wanted * FewDungeons)
            {
                said.Add(Said.Of("masterMap.check.fewDungeons", Lang.T(DungeonKey(name)), got, wanted));
            }
        }
        if (Of("SunkenCrypt4") == 0 && rules.Locations.Any(r => r.Prefab == "SunkenCrypt4"))
        {
            said.Add(Said.Of("masterMap.check.noIron"));
        }
        if (Of(FulingVillage) == 0 && rules.Locations.Any(r => r.Prefab == FulingVillage))
        {
            said.Add(Said.Of("masterMap.check.noBlackMetal"));
        }
        return said;
    }

    /// <summary>The text key of a dungeon kind's name (SeedPreview.Dungeons names are ids).</summary>
    private static string DungeonKey(string name) => name switch
    {
        "Burial chambers" => "newWorld.dungeon.burialChambers",
        "Troll caves" => "newWorld.dungeon.trollCaves",
        "Sunken crypts" => "newWorld.dungeon.sunkenCrypts",
        "Frost caves" => "newWorld.dungeon.frostCaves",
        "Infested mines" => "newWorld.dungeon.infestedMines",
        "Charred fortresses" => "newWorld.dungeon.charredFortresses",
        _ => "newWorld.dungeon.hildirs",
    };
}
