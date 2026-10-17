using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Editing;

public static partial class DungeonGen
{
    /// <summary>
    /// Lays the dungeon's rooms out from the first one (the entrance room, at the dungeon object when there is one) and
    /// joins more of the kind's ordinary rooms to the open ends, while they fit inside the kind's bounds.
    /// </summary>
    /// <param name="s">The settings (the size asks for the room count).</param>
    /// <param name="kind">The kind of dungeon.</param>
    /// <param name="rnd">The stream of the dungeon's own random numbers.</param>
    /// <param name="dungeon">The dungeon object, or null for the first room at the origin.</param>
    /// <param name="origin">Where the dungeon's rooms are laid out from.</param>
    /// <param name="entrances">The kind's entrance rooms.</param>
    /// <param name="normal">The kind's ordinary rooms that can be joined.</param>
    /// <returns>The placed rooms, the first one the entrance room.</returns>
    private static List<Dungeons.Placed> JoinRooms(Settings s, Dungeons.Kind kind, Random rnd, DungeonEntrances.Pose? dungeon, Vector3 origin,
        List<Dungeons.Room> entrances, List<Dungeons.Room> normal)
    {
        var rooms = new List<Dungeons.Placed>();
        // From the dungeon object, as the game lays its rooms out (the first room's way in at it); without one, the
        // first room at the origin, facing +z.
        var first = entrances.Count > 0 ? entrances[rnd.Next(entrances.Count)] : normal[rnd.Next(normal.Count)];
        rooms.Add(dungeon is { } at ? Dungeons.PlaceStart(first, at.Position, at.Rotation) : new Dungeons.Placed(first.Hash, origin, Quaternion.Identity));
        int target = 2 + 6 * Math.Clamp(s.Size, 1, 4);
        for (int tries = 0; rooms.Count < target && tries < target * 40; tries++)
        {
            var ends = Dungeons.Openings(rooms, freeOnly: true);
            if (ends.Count == 0)
            {
                break;
            }
            var end = ends[rnd.Next(ends.Count)];
            var fits = normal.Where(r => Dungeons.Matching(r, end.Type).Any()).ToList();
            if (fits.Count == 0)
            {
                continue;
            }
            // Weighted as the game weighs its rooms (Room.m_weight).
#pragma warning disable S2245 // A dungeon must come out the same from its seed; nothing here is a secret.
            var room = Weighted.Pick(fits, r => r.Weight, rnd.NextDouble());
#pragma warning restore S2245
            var mine = Dungeons.Matching(room, end.Type).ToList();
            var p = Dungeons.Attach(room, mine[rnd.Next(mine.Count)], end.Position, end.Rotation);
            if (Dungeons.Overlaps(rooms, p).Count == 0 && Dungeons.Inside(kind, origin, p))
            {
                rooms.Add(p);
            }
        }
        return rooms;
    }

    /// <summary>
    /// Closes every open end of the rooms with an end cap that fits it, the entrance's way in too (no location's tunnel
    /// joins it here). An end no cap fits is left open and noted.
    /// </summary>
    /// <param name="rooms">The placed rooms, the caps added to it.</param>
    /// <param name="caps">The kind's end caps with one opening.</param>
    /// <param name="rnd">The stream of the dungeon's own random numbers.</param>
    /// <param name="notes">Where the note of the ends left open is added.</param>
    private static void CapOpenEnds(List<Dungeons.Placed> rooms, List<Dungeons.Room> caps, Random rnd, List<Said> notes)
    {
        var open = Dungeons.Openings(rooms, freeOnly: true).Concat(Dungeons.Openings(rooms, freeOnly: false).Where(e => e.Entrance)).ToList();
        int left = 0;
        foreach (var end in open)
        {
            var fit = caps.Where(c => c.Openings[0].Type == end.Type).ToList();
            if (fit.Count == 0)
            {
                left++;
                continue;
            }
            var cap = fit[rnd.Next(fit.Count)];
            rooms.Add(Dungeons.Attach(cap, cap.Openings[0], end.Position, end.Rotation));
        }
        if (left > 0)
        {
            notes.Add(Said.Of("dungeon.msg.capsMissing", left));
        }
    }
}
