using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class DungeonGen
{
    /// <summary>What the game puts in the rooms, then the doors where rooms meet.</summary>
    /// <param name="rooms">The placed rooms.</param>
    /// <param name="kind">The kind of dungeon.</param>
    /// <param name="origin">Where the dungeon's rooms are laid out from.</param>
    /// <param name="worldSeed">The world's seed, for the game's contents.</param>
    /// <param name="rnd">The stream of the dungeon's own random numbers, for the doors.</param>
    /// <returns>The items to place.</returns>
    private static List<Item> CollectItems(List<Dungeons.Placed> rooms, Dungeons.Kind kind, Vector3 origin, int worldSeed, Random rnd)
    {
        var items = new List<Item>();
        foreach (var r in rooms)
        {
            foreach (var m in Dungeons.Contents(r, kind, origin, worldSeed))
            {
                items.Add(new Item(m.Prefab, m.Position, m.Rotation));
            }
        }
        foreach (var j in Dungeons.Joints(rooms))
        {
            if (Dungeons.RollDoor(kind, j, () => (float)rnd.NextDouble()) is string door)
            {
                items.Add(new Item(door, j.Position, j.Rotation));
            }
        }
        return items;
    }

    /// <summary>Monsters: the rooms' own spawners, fewer or more, or others picked from the settings' foes.</summary>
    /// <param name="items">The items to place, changed in place.</param>
    /// <param name="s">The settings (the monsters and the foes).</param>
    /// <param name="rnd">The stream of the dungeon's own random numbers.</param>
    private static void AdjustSpawners(List<Item> items, Settings s, Random rnd)
    {
        var spawners = items.Where(i => i.Prefab.StartsWith("Spawner_", StringComparison.Ordinal)).ToList();
        foreach (var sp in spawners)
        {
            items.Remove(sp);
            int copies = (int)MathF.Floor(s.Monsters) + (rnd.NextDouble() < s.Monsters - MathF.Floor(s.Monsters) ? 1 : 0);
            for (int c = 0; c < copies; c++)
            {
                string prefab = s.Foes is { Count: > 0 } foes ? foes[rnd.Next(foes.Count)] : sp.Prefab;
                items.Add(sp with { Prefab = prefab, Position = sp.Position + (c == 0 ? Vector3.Zero : new Vector3((float)rnd.NextDouble() * 2 - 1, 0, (float)rnd.NextDouble() * 2 - 1)) });
            }
        }
    }

    /// <summary>Loot: the rooms' chests kept by chance (fewer), or more chests beside them.</summary>
    /// <param name="items">The items to place, changed in place.</param>
    /// <param name="s">The settings (the loot).</param>
    /// <param name="rnd">The stream of the dungeon's own random numbers.</param>
    private static void AdjustChests(List<Item> items, Settings s, Random rnd)
    {
        var chests = items.Where(i => i.Prefab.StartsWith("TreasureChest_", StringComparison.Ordinal)).ToList();
        foreach (var ch in chests)
        {
            if (s.Loot < 1 && rnd.NextDouble() > s.Loot)
            {
                items.Remove(ch);
            }
            else if (s.Loot > 1 && rnd.NextDouble() < s.Loot - 1)
            {
                items.Add(ch with { Position = ch.Position + Vector3.Transform(new Vector3(1.2f, 0, 0), ch.Rotation) });
            }
        }
    }
}
