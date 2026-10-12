using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's tools for the game's dungeons in the open area (Frost Caves, crypts, mines…), as the Dungeon tool edits
/// them: their rooms, open ends and doors listed; rooms added at an open end or taken away, open ends capped, doors
/// put in or taken out. Each change rewrites the dungeon object (a new id) in one pending step.
/// </summary>
public sealed partial class ClaudeTools
{
    // ---- The game's dungeons.

    /// <summary>
    /// A Dungeon tool of its own on the open area's dungeon of that id (the view's tool is left as the user has it),
    /// telling its messages to said; or why there is none.
    /// </summary>
    internal (DungeonTool? Tool, string? Problem) DungeonOf(int id, Action<string>? said = null, int seed = 1)
    {
        if (_w.Session?.Scene is not { } scene || _w.InWorkshop)
        {
            return (null, "Dungeons are edited in a world's area: open_area where one is (list_dungeons).");
        }
        var tool = new DungeonTool { Scene = scene, Random = new Random(seed).NextSingle };
        var d = tool.All.FirstOrDefault(x => x.Index == id);
        if (d == null)
        {
            return (null, $"No dungeon with id {id} in the area (list_dungeons).");
        }
        if (d.Problem != null)
        {
            return (null, d.Problem);
        }
        tool.Open(d);
        if (said != null)
        {
            tool.Message += said;
        }
        return (tool, null);
    }

    /// <summary>A rotation's turn round the vertical, in degrees.</summary>
    private static float YawOf(System.Numerics.Quaternion q) => MathF.Round(TerrainEditor.App.BlueprintFormats.ToEuler(q).Y, 1);

    /// <summary>list_dungeons: the dungeons of the open area.</summary>
    [McpServerTool(Name = "list_dungeons", ReadOnly = true, Title = "The dungeons of the area")]
    [Description("The game's dungeons in the open area (Frost Caves, Burial Chambers, Sunken Crypts, Infested Mines…): the dungeon object's id, its kind, where it is (dungeons are 5000 m up, above their entrance), how many rooms and open ends it has. A dungeon nobody has been near is not laid out yet and has no rooms. Open the area round a dungeon's entrance (list_locations) to find it. dungeon_rooms gives one's rooms; generate_dungeon makes new ones of pieces instead.")]
    public Task<string> ListDungeons() => OnUi(() =>
    {
        if (_w.Session?.Scene is not { } scene || _w.InWorkshop)
        {
            return "Dungeons are listed in a world's area: open_area first.";
        }
        var tool = new DungeonTool { Scene = scene };
        return ToJson(tool.All.Select(d =>
        {
            tool.Open(d);
            return new
            {
                id = d.Index,
                kind = DungeonTool.KindName(d.Kind),
                x = MathF.Round(d.Thing.Position.X, 1),
                y = MathF.Round(d.Thing.Position.Y, 1),
                z = MathF.Round(d.Thing.Position.Z, 1),
                rooms = d.Rooms.Count,
                openEnds = d.Problem == null ? tool.FreeEnds.Count : 0,
                problem = d.Problem,
            };
        }).ToList());
    });

    /// <summary>dungeon_rooms: a dungeon's rooms, free openings, door joints and the rooms it can take.</summary>
    [McpServerTool(Name = "dungeon_rooms", ReadOnly = true, Title = "A dungeon's rooms")]
    [Description("A dungeon of the open area (by id, list_dungeons): its rooms (index, name, place, yaw), its free openings where a room can join (index, type, place, whether it is the entrance's), the joints between rooms that can take a door (index, the two rooms, whether a door is there), how many pairs of rooms overlap, and the rooms of its kind it can take (name, the types of their openings, end caps: rooms of one opening that close a dead end). A room joins a free opening through one of its own openings of the same type.")]
    public Task<string> DungeonRoomList(
        [Description("The dungeon's id (list_dungeons).")] int id) => OnUi(() =>
    {
        var (tool, problem) = DungeonOf(id);
        if (tool?.Dungeon is not { } d)
        {
            return problem!;
        }
        return ToJson(new
        {
            id,
            kind = DungeonTool.KindName(d.Kind),
            rooms = d.Rooms.Select((r, i) => new { index = i, name = r.Name, x = MathF.Round(r.Position.X, 2), y = MathF.Round(r.Position.Y, 2), z = MathF.Round(r.Position.Z, 2), yaw = YawOf(r.Rotation) }),
            openEnds = tool.FreeEnds.Select((e, i) => new { index = i, type = e.Type, room = e.Room, x = MathF.Round(e.Position.X, 2), y = MathF.Round(e.Position.Y, 2), z = MathF.Round(e.Position.Z, 2), entrance = e.Entrance }),
            doorJoints = tool.DoorJoints.Select((j, i) => new { index = i, rooms = new[] { j.First, j.Second }, type = j.Type, door = tool.DoorsAt(j).Count > 0 }),
            overlappingPairs = tool.OverlappingPairs,
            canTake = tool.Palette.Select(r => new { name = r.Name, openings = r.Openings.Select(o => o.Type).Distinct(), endCap = r.EndCap, entrance = r.Entrance }),
        });
    });

    /// <summary>The answer after a dungeon change: what the tool said, the dungeon's new id and its room count.</summary>
    private string AfterDungeon(DungeonTool tool, string said)
    {
        _w.AfterClaudeEdit(said);
        return tool.Dungeon is { } d ? $"{said} The dungeon's id is now {d.Index} ({d.Rooms.Count} rooms). Now pending: {Pending()}" : said;
    }

    /// <summary>add_dungeon_room: a room at a free opening, with what the game makes in it and its door.</summary>
    [McpServerTool(Name = "add_dungeon_room", Title = "Add a room to a dungeon")]
    [Description("Adds a room to a dungeon of the open area at one of its free openings, as the Dungeon tool does: the room (dungeon_rooms' canTake) joins through one of its own openings of that type (join picks which, when it has several: a corridor's other end, a room turned another way), with what the game makes in such a room (chests, spawners, torches; contents: false for none) and the door the game would roll where it joins (doors: false for none). Overlaps and rooms past the dungeon's space are said but built anyway. One pending step; the dungeon gets a new id (the answer gives it).")]
    public Task<string> AddDungeonRoom(
        [Description("The dungeon's id (list_dungeons).")] int id,
        [Description("The free opening, its index in dungeon_rooms' openEnds.")] int end,
        [Description("The room's name, e.g. cave_new_corridor03.")] string room,
        [Description("Which of the room's fitting openings joins (default 0).")] int join = 0,
        [Description("What the game makes in the room (default true).")] bool contents = true,
        [Description("The door the game would roll where it joins (default true).")] bool doors = true,
        [Description("Any number: other contents and door rolls.")] int seed = 1) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        string said = "";
        var (tool, problem) = DungeonOf(id, m => said = m, seed);
        if (tool?.Dungeon is not { } d)
        {
            return problem!;
        }
        if (end < 0 || end >= tool.FreeEnds.Count)
        {
            return $"The dungeon has {tool.FreeEnds.Count} free opening(s): end is 0 to {tool.FreeEnds.Count - 1} (dungeon_rooms).";
        }
        if (!tool.Palette.Any(r => r.Name == room))
        {
            return $"{room} is not a room this {DungeonTool.KindName(d.Kind)} can take (dungeon_rooms' canTake).";
        }
        if (tool.PlaceAt(end, room, join) is not { } placed)
        {
            return $"{room} has no opening of type {tool.FreeEnds[end].Type} to join there.";
        }
        tool.WithContents = contents;
        tool.WithDoors = doors;
        tool.Add(placed, $"Claude: added {room}");
        return AfterDungeon(tool, said);
    });

    /// <summary>remove_dungeon_room: a room taken out with what is in it and the doors at its joints.</summary>
    [McpServerTool(Name = "remove_dungeon_room", Title = "Take a room out of a dungeon")]
    [Description("Takes a room out of a dungeon of the open area, as the Dungeon tool's Delete does: with the objects in it (chests, spawners, torches) and the doors where it joins; its neighbours' openings are free then (close_dungeon_ends caps them). One pending step; the dungeon gets a new id.")]
    public Task<string> RemoveDungeonRoom(
        [Description("The dungeon's id (list_dungeons).")] int id,
        [Description("The room, its index in dungeon_rooms' rooms.")] int room) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        string said = "";
        var (tool, problem) = DungeonOf(id, m => said = m);
        if (tool?.Dungeon is not { } d)
        {
            return problem!;
        }
        if (room < 0 || room >= d.Rooms.Count)
        {
            return $"The dungeon has {d.Rooms.Count} room(s): room is 0 to {d.Rooms.Count - 1}.";
        }
        tool.Remove(room, $"Claude: removed {d.Rooms[room].Name}");
        return AfterDungeon(tool, said);
    });

    /// <summary>close_dungeon_ends: an end cap on every free opening.</summary>
    [McpServerTool(Name = "close_dungeon_ends", Title = "Close a dungeon's open ends")]
    [Description("Puts an end cap on every free opening of a dungeon of the open area, as the Dungeon tool's Close open ends does (the game's end caps for each opening's type), so that no corridor leads into nothing. One pending step; the dungeon gets a new id.")]
    public Task<string> CloseDungeonEnds(
        [Description("The dungeon's id (list_dungeons).")] int id) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        string said = "";
        var (tool, problem) = DungeonOf(id, m => said = m);
        if (tool == null)
        {
            return problem!;
        }
        return tool.CloseOpenEnds("Claude: closed open ends") > 0 ? AfterDungeon(tool, said) : said;
    });

    /// <summary>set_dungeon_door: the door at a joint put in or taken out.</summary>
    [McpServerTool(Name = "set_dungeon_door", Title = "A door in a dungeon")]
    [Description("Puts the dungeon's door for that kind of opening at a joint between two rooms (door: true), or takes the door there away (door: false), as the Dungeon tool does when a joint is clicked. The joint is its index in dungeon_rooms' doorJoints. One pending step.")]
    public Task<string> SetDungeonDoor(
        [Description("The dungeon's id (list_dungeons).")] int id,
        [Description("The joint, its index in dungeon_rooms' doorJoints.")] int joint,
        [Description("true: a door there; false: none.")] bool door = true) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        string said = "";
        var (tool, problem) = DungeonOf(id, m => said = m);
        if (tool?.Dungeon is not { } d)
        {
            return problem!;
        }
        var joints = tool.DoorJoints;
        if (joint < 0 || joint >= joints.Count)
        {
            return $"The dungeon has {joints.Count} joint(s) that take a door: joint is 0 to {joints.Count - 1}.";
        }
        if (tool.DoorsAt(joints[joint]).Count > 0 == door)
        {
            return door ? "There is a door there already." : "There is no door there.";
        }
        tool.ToggleDoor(d, joints[joint], door ? "Claude: added a dungeon door" : "Claude: removed a dungeon door");
        _w.AfterClaudeEdit(said);
        return $"{said} Now pending: {Pending()}";
    });
}
