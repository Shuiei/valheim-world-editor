
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>A point d metres along the wall from a spot facing yaw (to its right for d &gt; 0).</summary>
    private static (float U, float V) Beside((float U, float V, float Yaw) s, float d) =>
        (s.U + d * MathF.Cos(s.Yaw * MathF.PI / 180), s.V - d * MathF.Sin(s.Yaw * MathF.PI / 180));

    /// <summary>Two candles (skulls when the dungeon respawns) either side of a spot, a bit out from it.</summary>
    private void FlankingCandles(Frame f, (float U, float V, float Yaw) spot)
    {
        foreach (int side in new[] { -1, 1 })
        {
            var (cu, cv) = Beside(spot, side * 1.1f);
            Put(f, _s.Respawn ? "skull_pile" : "Candle_resin", cu, cv, R(0, 360), 0.25f);
        }
    }

    /// <summary>
    /// A wide alcove's furniture, by the style: a sarcophagus across it with a skull beside, a statue between two
    /// candles, a bed in its bay with a chest at its foot, a shelf and crates, a totem on straw. False when the
    /// style's choice has no place there (the single nook's piece is then tried).
    /// </summary>
    private bool Alcove(Frame f, float u, float v, float yaw)
    {
        // An alcove is a tile deep less its wall: the big piece stands at its middle, a touch out, and is not refused
        // for being near the walls (the planner keeps alcoves clear of doorways and other rooms).
        var spot = (u, v, yaw);
        var (bu, bv) = In(spot, 0.15f);
        return _style.Name switch
        {
            "Crypt" or "Catacombs" or "Ruins" => CryptAlcove(f, spot, bu, bv),
            "Temple" => TempleAlcove(f, spot, bu, bv),
            "Fortress" or "Prison" => FortressAlcove(f, spot, bu, bv),
            "Dvergr hold" => DvergrAlcove(f, spot, bu, bv),
            "Goblin warren" => GoblinAlcove(f, spot, bu, bv),
            _ => false,
        };
    }

    /// <summary>A temple's alcove: a statue at the middle, with a candle either side.</summary>
    private bool TempleAlcove(Frame f, (float U, float V, float Yaw) spot, float bu, float bv)
    {
        if (!Put(f, One("StatueFreya", "StatueThor", "StatueSeed"), bu, bv, spot.Yaw, 0.7f, force: true))
        {
            return false;
        }
        FlankingCandles(f, spot);
        return true;
    }

    /// <summary>A goblin warren's alcove: a straw pile on the spot, a totem, bed or trash pile at the middle.</summary>
    private bool GoblinAlcove(Frame f, (float U, float V, float Yaw) spot, float bu, float bv)
    {
        Put(f, "goblin_strawpile", spot.U, spot.V, R(0, 360), 0, force: true);
        return Put(f, One("goblin_totempole", "goblin_bed", "goblin_trashpile"), bu, bv, spot.Yaw, 0.7f, force: true);
    }
}
