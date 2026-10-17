using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
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
}
