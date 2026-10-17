using System.Numerics;
using TerrainEditor.App;
using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

public static partial class DungeonGen
{
    /// <summary>The game's own dungeon of a biome (a DG_ prefab name), for Made.Rooms and for its entrance.</summary>
    public static string KindFor(string biome) => biome switch
    {
        "Meadows" or "Black Forest" => "DG_ForestCrypt",
        "Swamp" => "DG_SunkenCrypt",
        "Plains" => "DG_Hildir_PlainsFortress",
        "Mistlands" => "DG_DvergrTown",
        "Ashlands" => "DG_MorkHalla",
        _ => "DG_Cave",
    };

    /// <summary>A name for a dungeon of this style (null: the biome's); the same seed gives the same name.</summary>
    public static string NameFor(int seed, string? style = null, string? biome = null)
    {
        var r = new Random(seed * 7919 + 13);
        string[] starts = NameStarts(style ?? BiomeOf(biome).Style);
        return $"{starts[r.Next(starts.Length)]} of {NameEnds[r.Next(NameEnds.Length)]}";
    }

    // ---- From the game's rooms.
    /// <summary>
    /// Joins the game's rooms of the settings' kind (or the biome's) from an entrance, caps the open ends,
    /// fills them with the game's contents and doors, then thins or adds spawners and chests.
    /// </summary>
    private static Result MakeRooms(Settings s, int worldSeed, DungeonEntrances.Pose? dungeon)
    {
        var notes = new List<Said>();
        static bool Joinable(Dungeons.Kind k) => Dungeons.RoomsFor(k).Any(r => !r.Entrance && !r.EndCap && !r.Divider && r.Openings.Length > 0);
        var cave = Dungeons.KindOf(Save.StableHash.Of("DG_Cave"));
        string wanted = s.Kind ?? KindFor(s.Biome);
        var kind = Dungeons.Kinds.FirstOrDefault(k => k.Name == wanted) ?? cave;
        // Camps and villages are laid out on the ground, not joined room to room: a cave instead.
        if (kind == null || !Joinable(kind))
        {
            if (cave == null || !Joinable(cave))
            {
                throw new InvalidDataException($"The game's data has no rooms to join for {wanted.Replace("DG_", "", StringComparison.Ordinal)}, and no cave to make instead "
                    + "(a game update may have changed its dungeons).");
            }
            // (No kind at all only when there is no cave either.)
            notes.Add(Said.Of("dungeon.msg.caveInstead", Describe(kind!)));
            kind = cave;
        }
        var rnd = new Random(s.Seed);
        var all = Dungeons.RoomsFor(kind).ToList();
        var entrances = all.Where(r => r.Entrance && !r.EndCap).ToList();
        var normal = all.Where(r => !r.Entrance && !r.EndCap && !r.Divider && r.Openings.Length > 0).ToList();
        var caps = all.Where(r => r.EndCap && r.Openings.Length == 1).ToList();
        var rooms = new List<Dungeons.Placed>();
        // From the dungeon object, as the game lays its rooms out (the first room's way in at it); without one, the
        // first room at the origin, facing +z.
        var origin = dungeon?.Position ?? Vector3.Zero;
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
        // End caps on every open end; the entrance's way in too (no location's tunnel joins it here).
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
        // What the game puts in the rooms, then doors where rooms meet.
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
        // Monsters: the rooms' own spawners, fewer or more, or others picked.
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
        // Loot: the rooms' chests kept by chance (fewer), or more chests beside them.
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
        // Arriving in the entrance room, near its middle (the floor is found where it is placed).
        var arrival = rooms[0].Position;
        notes.Add(Said.Of("dungeon.msg.madeRooms", Describe(kind), rooms.Count, items.Count));
        string name = string.IsNullOrWhiteSpace(s.Name) ? NameFor(s.Seed, null, s.Biome) : s.Name!;
        return new Result(items, arrival, kind, rooms, notes, name, Dungeon: dungeon);
    }
}
