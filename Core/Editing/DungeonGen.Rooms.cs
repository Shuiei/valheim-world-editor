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
        var kind = ChooseRoomKind(s, notes);
        var rnd = new Random(s.Seed);
        var all = Dungeons.RoomsFor(kind).ToList();
        var entrances = all.Where(r => r.Entrance && !r.EndCap).ToList();
        var normal = all.Where(r => !r.Entrance && !r.EndCap && !r.Divider && r.Openings.Length > 0).ToList();
        var caps = all.Where(r => r.EndCap && r.Openings.Length == 1).ToList();
        var origin = dungeon?.Position ?? Vector3.Zero;
        var rooms = JoinRooms(s, kind, rnd, dungeon, origin, entrances, normal);
        CapOpenEnds(rooms, caps, rnd, notes);
        var items = CollectItems(rooms, kind, origin, worldSeed, rnd);
        AdjustSpawners(items, s, rnd);
        AdjustChests(items, s, rnd);
        // Arriving in the entrance room, near its middle (the floor is found where it is placed).
        var arrival = rooms[0].Position;
        notes.Add(Said.Of("dungeon.msg.madeRooms", Describe(kind), rooms.Count, items.Count));
        string name = string.IsNullOrWhiteSpace(s.Name) ? NameFor(s.Seed, null, s.Biome) : s.Name!;
        return new Result(items, arrival, kind, rooms, notes, name, Dungeon: dungeon);
    }
}
