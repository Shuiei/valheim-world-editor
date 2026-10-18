
namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    // ---- What it holds.
    /// <summary>The biome's treasure chest at (u, v), unless there is no loot (and it is not forced).</summary>
    private void Chest(Frame f, float u, float v, float yaw, bool force = false)
    {
        if (force || _s.Loot > 0)
        {
            Put(f, One(BiomeChests), u, v, yaw, 0.8f);
        }
    }

    /// <summary>A chest at a free spot along the walls, tried in random order; none when no spot is free.</summary>
    private void ChestAgainstWall(Frame f)
    {
        foreach (var s in f.WallSpots(2, 3f).OrderBy(_ => _rnd.Next()))
        {
            var (u, v) = In(s, 0.6f);
            if (f.Free(u, v, 0.8f))
            {
                Chest(f, u, v, s.Yaw);
                return;
            }
        }
    }

    /// <summary>
    /// A chest holding the boss's key (the crypt key opens the arena's gate) and some loot. Its contents
    /// are written in, and the game told it already has them, or it would fill it with its own.
    /// </summary>
    private void KeyChest(Frame f)
    {
        var items = new List<(string, int)> { ("CryptKey", 1) };
        foreach (var (item, max) in _b.Loot.OrderBy(_ => _rnd.Next()).Take(3))
        {
            items.Add((item, Math.Max(1, (int)(max * R(0.4f, 1)))));
        }
        var data = new[]
        {
            ("ints", "addedDefaultItems", "1"),
            ("bytes", "items", Convert.ToBase64String(DungeonProps.Inventory(items))),
        };
        foreach (var s in f.WallSpots(2, 3f).OrderBy(s => Math.Abs(s.V - f.HalfD)))
        {
            var (u, v) = In(s, 0.6f);
            if (Put(f, _b.Chest, u, v, s.Yaw, 0.8f, data: data))
            {
                Brazier(f, u + 1.6f, v);
                return;
            }
        }
        Put(f, _b.Chest, 0, 0, 0, 0.8f, force: true, data: data);
    }

    /// <summary>About n of the biome's pickable loot lying about, more with the settings' Loot.</summary>
    private void Pickables(Frame f, float n)
    {
        for (int i = 0, times = Count(n * (0.5f + 0.5f * _s.Loot) * Depth(f)); i < times; i++)
        {
            if (Spot(f, 0.5f) is var (u, v))
            {
                Put(f, _b.Pickable, u, v, R(0, 360), 0.5f);
            }
        }
    }
}
