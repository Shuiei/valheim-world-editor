
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    /// <summary>
    /// The arena, one of several: a ring of columns and braziers round a great rug with the boss upon it; the boss
    /// enthroned on a dais at the far end over heaps of skulls, braziers in rows before it; a crypt arena of columns
    /// on a grid, sarcophagi between them, chains above. Its followers about it.
    /// </summary>
    private void Boss(Frame f)
    {
        float r = Math.Min(f.HalfA, f.HalfD) - 3.5f;
        int variant = _rnd.Next(_mat.Name is "Stone" or "Grausten" or "Marble" ? 4 : 3);
        switch (variant)
        {
            case 3:
                Pit(f);
                break;
            case 0:
                Ring(f, r);
                break;
            case 1:
                Dais(f);
                break;
            default:
                CryptGrid(f);
                break;
        }
        DressWalls(f, variant);
        DressFloor(f, variant, r);
        Followers(f);
    }

    /// <summary>The boss and its foes, the loose skulls and bones, and the room's lights.</summary>
    /// <param name="f">The room's frame.</param>
    private void Followers(Frame f)
    {
        f.Take(0, 0, 1.5f);
        if (_boss != null)
        {
            Spawn(f, _boss, 0, 0.5f, 180, Math.Clamp(1 + (int)MathF.Round(_s.Monsters), 1, 3), once: true, force: true);
        }
        if (_s.Monsters > 0)
        {
            for (int i = 0, times = Count(2 * _s.Monsters); i < times; i++)
            {
                if (Spot(f, 0.7f, 2f) is var (u, v))
                {
                    Spawn(f, One(_foes), u, v, 180, 1, once: true);
                }
            }
        }
        Clutter(f, 0.012f, "Skull1", "LargeBone", "lox_ribs", "skull_pile");
        Lights(important: true);
    }
}
