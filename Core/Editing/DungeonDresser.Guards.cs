using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    // ---- Who guards it.
    /// <summary>
    /// A foe at (u, v): its spawner, or the creature itself when once or the settings do not respawn (only
    /// then is level written: 2 is one star).
    /// </summary>
    private void Spawn(Frame f, Foe foe, float u, float v, float yaw, int level = 1, bool once = false, bool force = false)
    {
        bool creature = once || !_s.Respawn;
        var data = creature && level > 1 ? new[] { ("ints", "level", level.ToString(System.Globalization.CultureInfo.InvariantCulture)) } : null;
        Put(f, creature ? foe.Creature : foe.Spawner, u, v, yaw, 0.6f, force: force, data: data);
    }

    /// <summary>Foes about the room, perArea for every 75 square metres times the settings' Monsters; from pool, else the dungeon's.</summary>
    private void Foes(Frame f, float perArea, Foe[]? pool = null, int level = 1)
    {
        int n = Count(f.Area * perArea / 75f * _s.Monsters * Depth(f));
        pool ??= _foes;
        for (int i = 0; i < n && pool.Length > 0; i++)
        {
            // Away from the way in, facing it.
            if (Spot(f, 0.7f, 1.2f) is var (u, v))
            {
                Spawn(f, One(pool), u, v, 180 + R(-40, 40), level);
            }
        }
    }

    /// <summary>
    /// One of the elites (the biome's, or the chosen foes) at (u, v), level 2 when Monsters is 2 or more; none when there
    /// are no monsters.
    /// </summary>
    private void Elite(Frame f, float u, float v, float yaw)
    {
        if (_s.Monsters > 0 && _elites.Length > 0)
        {
            Spawn(f, One(_elites), u, v, yaw, _s.Monsters >= 2 ? 2 : 1);
        }
    }
}
