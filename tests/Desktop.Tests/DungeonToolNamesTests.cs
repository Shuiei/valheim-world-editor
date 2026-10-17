using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Dungeon tool's history names: each change is named as asked, and adding a room says what its objects and doors are.
/// </summary>
[Collection("World files")]
public class DungeonToolNamesTests
{
    [AvaloniaFact]
    public void ChangesAreNamedAsAsked()
    {
        using var r = new SharedDungeonToolRun();
        var s = r.Scene.Session!;
        Assert.True(r.T.Add(r.T.PlaceAt(0, "cave_new_corridor03", 0)!, "Mine: a corridor"));
        Assert.Equal("Mine: a corridor", s.UndoLabel);
        var joint = r.T.DoorJoints[0];
        if (r.T.DoorsAt(joint).Count > 0)
        {
            r.T.ToggleDoor(r.T.Dungeon!, joint);
        }
        r.T.ToggleDoor(r.T.Dungeon!, joint, "Mine: a door");
        Assert.Equal("Mine: a door", s.UndoLabel);
        Assert.True(r.T.Remove(1, "Mine: no corridor"));
        Assert.Equal("Mine: no corridor", s.UndoLabel);
        Assert.False(r.T.Remove(9));
        r.T.CloseOpenEnds("Mine: capped");
        Assert.Equal("Mine: capped", s.UndoLabel);
    }

    // A room added says what came with it: no objects or some, no door, one, or how many.
    [Theory]
    [InlineData(0, 0, "Added cave_room.")]
    [InlineData(0, 1, "Added cave_room with a door.")]
    [InlineData(0, 3, "Added cave_room with 3 doors.")]
    [InlineData(2, 0, "Added cave_room with 2 object(s).")]
    [InlineData(2, 1, "Added cave_room with 2 object(s) and a door.")]
    [InlineData(2, 2, "Added cave_room with 2 object(s) and 2 doors.")]
    public void AddingARoomSaysItsObjectsAndDoors(int objects, int doors, string said) =>
        Assert.Equal(said, DungeonTool.Added("cave_room", objects, doors).English);
}
