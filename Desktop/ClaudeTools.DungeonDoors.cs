using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
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
