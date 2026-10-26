using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.SharedDungeonToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Dungeon tool's build and delete actions: Build here sets the place tool for building and gives its settings back on leaving, keeps the cut across areas, and deleting a room takes its objects with it.
/// </summary>
[Collection("World files")]
public class DungeonToolBuildTests
{
    /// <summary>
    /// Clicks a button.
    /// </summary>
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void BuildHereSetsThePlaceToolForBuildingAndLeavingGivesItsSettingsBack()
    {
        using var r = new SharedDungeonToolRun();
        var t = r.W.PlaceTool;
        var before = (t.Mode, t.Building, t.OneAtATime);
        Click(r.P.BuildHere);
        Assert.Equal(ToolMode.Place, r.W.Tools.Mode);
        Assert.True(r.W.BuildingInDungeon);
        Assert.True(t.Building);
        Assert.True(r.W.BuildPanel.Card.IsVisible);
        Assert.False(r.W.PlacePanel.Card.IsVisible);
        // Back to the Dungeon tool: the Place tool as it was.
        r.W.Tools.ChooseMode(ToolMode.Dungeon);
        Assert.False(r.W.BuildingInDungeon);
        Assert.Equal(before, (t.Mode, t.Building, t.OneAtATime));
        Assert.False(r.W.BuildPanel.Card.IsVisible);
    }

    [AvaloniaFact]
    public void BuildHereKeepsTheCutAndAnotherAreaEndsIt()
    {
        using var r = new SharedDungeonToolRun();
        Click(r.P.GoInside);
        float? cut = r.W.View.CutY;
        Assert.NotNull(cut);
        Click(r.P.BuildHere);
        // Building under the dungeon's cut: the roofs stay away, the pieces go on the floors.
        Assert.Equal(cut, r.W.View.CutY);
        Assert.Equal(cut, r.W.PlaceTool.CutY);
        // Another area opened: building in this dungeon ends, its cut with it.
        r.W.Edit(r.Scene.Session!);
        Assert.False(r.W.BuildingInDungeon);
        Assert.Null(r.W.PlaceTool.CutY);
    }

    [AvaloniaFact]
    public void DeletingARoomTakesTheObjectsInItWithIt()
    {
        using var r = new SharedDungeonToolRun();
        Click(r.P.CloseEnds);
        // A chest in a cap, and one in the entrance room.
        var cap = r.Rooms[1];
        int chest = StableHash.Of("piece_chest_wood");
        r.Scene.Session!.Commit("Chests", null, [],
        [
            (new NewObject(0, chest, cap.Position + new Vector3(0, -2, 0), Vector3.Zero, 0), true),
            (new NewObject(0, chest, At + new Vector3(4, -2, 0), Vector3.Zero, 0), true),
        ]);
        int before = r.Scene.Things.Count(t => !t.Gone && t.Prefab == chest);
        r.T.Hover(cap.Position + new Vector3(0, 1, 0), -Vector3.UnitY);
        r.T.Click();
        Assert.True(r.T.Delete());
        Assert.Equal(before - 1, r.Scene.Things.Count(t => !t.Gone && t.Prefab == chest));
        Assert.Contains("and the 1 object(s) in it", r.Said);
        // One step back: the room and its chest.
        r.Scene.Session.Undo();
        Assert.Equal(before, r.Scene.Things.Count(t => !t.Gone && t.Prefab == chest));
        Assert.Equal(4, r.Rooms.Count);
    }
}
