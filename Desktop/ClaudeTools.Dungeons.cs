using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

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
    internal (DungeonTool? Tool, string? Problem) DungeonOf(int id, Action<Said>? said = null, int seed = 1)
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
            return (null, d.Problem.English);
        }
        tool.Open(d);
        if (said != null)
        {
            tool.Message += said;
        }
        return (tool, null);
    }

    /// <summary>
    /// The start of a change to a dungeon: why none may be made now, else the dungeon's tool (see <see cref="DungeonOf"/>),
    /// with the messages its change says collected for the answer.
    /// </summary>
    /// <param name="id">The dungeon's id (list_dungeons).</param>
    /// <param name="seed">Any number: other contents and door rolls.</param>
    /// <returns>The tool (null when none), the messages it said, and why there is no tool or no edit.</returns>
    private (DungeonTool? Tool, Said Said, string? Problem) DungeonEdit(int id, int seed = 1)
    {
        if (CannotEdit() is string why)
        {
            return (null, Said.Empty, why);
        }
        var said = Said.Empty;
        var (tool, problem) = DungeonOf(id, m => said = m, seed);
        return (tool, said, problem);
    }

    /// <summary>An opening's type as Claude reads it: the game's plain openings have none ("plain").</summary>
    internal static string TypeName(string type) => type.Length == 0 ? "plain" : type;

    /// <summary>A rotation's turn round the vertical, in degrees.</summary>
    private static float YawOf(System.Numerics.Quaternion q) => MathF.Round(BlueprintFormats.ToEuler(q).Y, 1);

    /// <summary>list_dungeons: the dungeons of the open area.</summary>
    [McpServerTool(Name = "list_dungeons", ReadOnly = true, Title = "The dungeons of the area")]
    [Description("The game's dungeons in the open area (Frost Caves, Burial Chambers, Sunken Crypts, Infested Mines…): the dungeon object's id, its kind, where it is (dungeons are 5000 m up, above their entrance), how many rooms and open ends it has. A dungeon nobody has been near is not laid out yet and has no rooms. Open the area round a dungeon's entrance (list_locations) to find it. dungeon_rooms gives one's rooms; generate_dungeon makes new ones of pieces instead.")]
    public Task<string> ListDungeons() => OnUi(() =>
    {
        if (_w.Session?.Scene is not { } scene || _w.InWorkshop)
        {
            return "Dungeons are listed in a world's area: open_area first.";
        }
        int reads = scene.Reads;
        var tool = new DungeonTool { Scene = scene };
        var dungeons = tool.All.Select(d =>
        {
            tool.Open(d);
            return new
            {
                id = d.Index,
                kind = DungeonTool.KindName(d.Kind).English,
                x = MathF.Round(d.Thing.Position.X, 1),
                y = MathF.Round(d.Thing.Position.Y, 1),
                z = MathF.Round(d.Thing.Position.Z, 1),
                rooms = d.Rooms.Count,
                openEnds = d.Problem == null ? tool.FreeEnds.Count : 0,
                problem = d.Problem?.English,
            };
        }).ToList();
        // A dungeon's id is its object's, one of the area's ids like list_objects gives (get_object, select_objects).
        if (dungeons.Count > 0)
        {
            GaveIds(scene, reads);
        }
        return ToJson(dungeons);
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
            kind = DungeonTool.KindName(d.Kind).English,
            rooms = d.Rooms.Select((r, i) => new { index = i, name = r.Name, x = MathF.Round(r.Position.X, 2), y = MathF.Round(r.Position.Y, 2), z = MathF.Round(r.Position.Z, 2), yaw = YawOf(r.Rotation) }),
            openEnds = tool.FreeEnds.Select((e, i) => new { index = i, type = TypeName(e.Type), room = e.Room, x = MathF.Round(e.Position.X, 2), y = MathF.Round(e.Position.Y, 2), z = MathF.Round(e.Position.Z, 2), entrance = e.Entrance }),
            doorJoints = tool.DoorJoints.Select((j, i) => new { index = i, rooms = new[] { j.First, j.Second }, type = TypeName(j.Type), door = tool.DoorsAt(j).Count > 0 }),
            overlappingPairs = tool.OverlappingPairs,
            canTake = tool.Palette.Select(r => new { name = r.Name, openings = r.Openings.Select(o => TypeName(o.Type)).Distinct(), endCap = r.EndCap, entrance = r.Entrance }),
        });
    });

    /// <summary>The session's last step, to tell afterwards whether a tool made one (it may also make none and say why).</summary>
    private EditSession.Change? LastStep() => _w.Session?.UndoList is { Count: > 0 } steps ? steps[^1] : null;

    /// <summary>Whether a step was made since <see cref="LastStep"/> gave this one.</summary>
    private bool Stepped(EditSession.Change? before) => !ReferenceEquals(LastStep(), before);

    /// <summary>The answer when a dungeon tool made no step: what the tool said, else that nothing changed.</summary>
    private static string Unchanged(Said said) => said.English.Length > 0 ? said.English : "Nothing changed.";

    /// <summary>
    /// The answer after a dungeon change: what the tool said (the status bar shows it in the language in use, Claude is
    /// answered in English), the dungeon's new id and its room count.
    /// </summary>
    private string AfterDungeon(DungeonTool tool, Said said)
    {
        _w.AfterClaudeEdit(said);
        return tool.Dungeon is { } d ? $"{said.English} The dungeon's id is now {d.Index} ({d.Rooms.Count} rooms). Now pending: {Pending()}" : said.English;
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
        var (tool, said, problem) = DungeonEdit(id, seed);
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
            return $"{room} is not a room this {DungeonTool.KindName(d.Kind).English} can take (dungeon_rooms' canTake).";
        }
        if (tool.PlaceAt(end, room, join) is not { } placed)
        {
            return $"{room} has no opening of type {TypeName(tool.FreeEnds[end].Type)} to join there.";
        }
        tool.WithContents = contents;
        tool.WithDoors = doors;
        tool.Add(placed, ClaudesStep(Lang.T("claude.undo.added", room)));
        return AfterDungeon(tool, said);
    });

    /// <summary>remove_dungeon_room: a room taken out with what is in it and the doors at its joints.</summary>
    [McpServerTool(Name = "remove_dungeon_room", Title = "Take a room out of a dungeon")]
    [Description("Takes a room out of a dungeon of the open area, as the Dungeon tool's Delete does: with the objects in it (chests, spawners, torches) and the doors where it joins; its neighbours' openings are free then (close_dungeon_ends caps them). One pending step; the dungeon gets a new id.")]
    public Task<string> RemoveDungeonRoom(
        [Description("The dungeon's id (list_dungeons).")] int id,
        [Description("The room, its index in dungeon_rooms' rooms.")] int room) => OnUi(() =>
    {
        var (tool, said, problem) = DungeonEdit(id);
        if (tool?.Dungeon is not { } d)
        {
            return problem!;
        }
        if (room < 0 || room >= d.Rooms.Count)
        {
            return $"The dungeon has {d.Rooms.Count} room(s): room is 0 to {d.Rooms.Count - 1}.";
        }
        tool.Remove(room, ClaudesStep(Lang.T("claude.undo.removed", d.Rooms[room].Name)));
        return AfterDungeon(tool, said);
    });

    /// <summary>close_dungeon_ends: an end cap on every free opening.</summary>
    [McpServerTool(Name = "close_dungeon_ends", Title = "Close a dungeon's open ends")]
    [Description("Puts an end cap on every free opening of a dungeon of the open area, as the Dungeon tool's Close open ends does (the game's end caps for each opening's type), so that no corridor leads into nothing. One pending step; the dungeon gets a new id.")]
    public Task<string> CloseDungeonEnds(
        [Description("The dungeon's id (list_dungeons).")] int id) => OnUi(() =>
    {
        var (tool, said, problem) = DungeonEdit(id);
        if (tool == null)
        {
            return problem!;
        }
        var before = LastStep();
        tool.CloseOpenEnds(ClaudesStep(Lang.T("claude.undo.closedEnds")));
        return Stepped(before) ? AfterDungeon(tool, said) : Unchanged(said);
    });

    /// <summary>set_dungeon_door: the door at a joint put in or taken out.</summary>
    [McpServerTool(Name = "set_dungeon_door", Title = "A door in a dungeon")]
    [Description("Puts the dungeon's door for that kind of opening at a joint between two rooms (door: true), or takes the door there away (door: false), as the Dungeon tool does when a joint is clicked. The joint is its index in dungeon_rooms' doorJoints. One pending step.")]
    public Task<string> SetDungeonDoor(
        [Description("The dungeon's id (list_dungeons).")] int id,
        [Description("The joint, its index in dungeon_rooms' doorJoints.")] int joint,
        [Description("true: a door there; false: none.")] bool door = true) => OnUi(() =>
    {
        var (tool, said, problem) = DungeonEdit(id);
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
        var before = LastStep();
        tool.ToggleDoor(d, joints[joint], ClaudesStep(door ? Lang.T("claude.undo.addedDoor") : Lang.T("claude.undo.removedDoor")));
        if (!Stepped(before))
        {
            return Unchanged(said);
        }
        _w.AfterClaudeEdit(said);
        return $"{said.English} Now pending: {Pending()}";
    });
}
