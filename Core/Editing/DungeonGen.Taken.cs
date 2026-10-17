using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class DungeonGen
{
    /// <summary>An axis-aligned box of space (metres).</summary>
    /// <param name="Min">Its lowest corner.</param>
    /// <param name="Max">Its highest corner.</param>
    public readonly record struct Box(Vector3 Min, Vector3 Max)
    {
        /// <summary>Whether the two boxes share any space (touching faces do not count).</summary>
        public bool Overlaps(Box o) => Min.X < o.Max.X && o.Min.X < Max.X && Min.Y < o.Max.Y && o.Min.Y < Max.Y && Min.Z < o.Max.Z && o.Min.Z < Max.Z;
    }

    /// <summary>
    /// What is taken high up: objects above 3000 m (a dungeon object: the whole space its rooms keep),
    /// and the space over every location whose dungeon the game has not laid out yet.
    /// </summary>
    /// <param name="high">The objects high up (prefab hash, position).</param>
    /// <param name="locations">The world's locations (position, name hash); only those in DungeonLocations count.</param>
    public static List<Box> Taken(IEnumerable<(int Prefab, Vector3 Position)> high, IEnumerable<(Vector3 Position, int Location)> locations)
    {
        var boxes = new List<Box>();
        foreach (var (prefab, p) in high)
        {
            if (Dungeons.KindOf(prefab) is { } k)
            {
                var h = new Vector3(k.ZoneSize.X / 2 + 32, 60, k.ZoneSize.Z / 2 + 32);
                boxes.Add(new Box(p - h, p + h));
            }
            else
            {
                boxes.Add(new Box(p - Vector3.One, p + Vector3.One));
            }
        }
        foreach (var (p, location) in locations)
        {
            if (DungeonLocations.Contains(location))
            {
                boxes.Add(new Box(new Vector3(p.X - 96, p.Y + 4900, p.Z - 96), new Vector3(p.X + 96, p.Y + 5250, p.Z + 96)));
            }
        }
        return boxes;
    }

    /// <summary>
    /// Where a dungeon made at origin can go: there, or higher by 50 m steps until it is clear of what is
    /// taken (at most 80 steps, then it stays where the last step left it).
    /// </summary>
    public static Vector3 Clear(Vector3 origin, Result r, IReadOnlyList<Box> taken) =>
        Clear(origin, [.. r.Items.Select(i => i.Position), .. r.Rooms?.Select(p => p.Position) ?? []], r.Rooms != null ? 30 : 10, taken);

    /// <summary>
    /// Where a dungeon whose objects stand at these points (from its origin) can go: there, or higher by 50 m steps
    /// until it is clear of what is taken (at most 80 steps, then it stays where the last step left it).
    /// </summary>
    /// <param name="origin">Where it would go.</param>
    /// <param name="points">Its objects' places, from its origin.</param>
    /// <param name="pad">How much room to keep around them, metres.</param>
    /// <param name="taken">What is taken up there (Taken).</param>
    public static Vector3 Clear(Vector3 origin, IReadOnlyList<Vector3> points, float pad, IReadOnlyList<Box> taken)
    {
        if (points.Count == 0)
        {
            return origin;
        }
        var min = points.Aggregate(Vector3.Min) - new Vector3(pad, 6, pad);
        var max = points.Aggregate(Vector3.Max) + new Vector3(pad, 6, pad);
        for (int k = 0; k < 80 && taken.Any(t => t.Overlaps(new Box(origin + min, origin + max))); k++)
        {
            origin.Y += 50;
        }
        return origin;
    }
}
