using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;
using static TerrainEditor.Desktop.Tests.SharedDungeonToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Dungeon tool on the test world (zone 0, 0): the panel lists the dungeon and its rooms, a room picked and clicked onto an open end joins there, undo takes it away, turned joins by another opening, the open ends close with end caps, and a clicked room is selected and deleted.
/// </summary>
[Collection("World files")]
public class DungeonToolTests
{
    /// <summary>
    /// Clicks a button.
    /// </summary>
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void ThePanelListsTheAreasDungeonAndItsRooms()
    {
        using var r = new SharedDungeonToolRun();
        Assert.True(r.P.Card.IsVisible);
        Assert.Equal("Frost Cave, 1 rooms, at 10, 10", Assert.Single((IEnumerable<string>)r.P.DungeonBox.ItemsSource!));
        Assert.NotNull(r.T.Dungeon);
        Assert.Equal(3, r.T.FreeEnds.Count);
        var names = r.P.Rooms.Items.Cast<ListBoxItem>().Select(i => (string)i.Tag!).ToList();
        Assert.Contains("cave_new_corridor03", names);
        Assert.DoesNotContain(names, n => n.StartsWith("sunkencrypt", StringComparison.Ordinal));
        Assert.Contains("3 open end(s)", r.P.Info.Text);
        // The finder narrows the list.
        r.P.Filter.Text = "endcap_painting";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.NotEmpty(r.P.Rooms.Items);
        Assert.All(r.P.Rooms.Items.Cast<ListBoxItem>(), i => Assert.Contains("endcap_painting", (string)i.Tag!));
    }

    [AvaloniaFact]
    public void ARoomClickedOntoAnOpenEndJoinsThereAndUndoTakesItAway()
    {
        using var r = new SharedDungeonToolRun();
        r.T.Choose("cave_new_corridor03");
        var end = r.T.FreeEnds[0];
        r.PointAt(end);
        Assert.Equal(0, r.T.HoverEnd);
        var preview = r.T.Preview!;
        Assert.Null(r.T.Problem(preview));
        Assert.True(r.T.Click());
        Assert.Equal(2, r.Rooms.Count);
        Assert.Equal(2, Saved(r).Count);
        // The corridor's opening meets the entrance's: that one is not open any more, the corridor's
        // far end is.
        Assert.DoesNotContain(r.T.FreeEnds, e => Vector3.Distance(e.Position, end.Position) < 0.01f);
        Assert.Equal(3, r.T.FreeEnds.Count);
        Assert.Contains("Added cave_new_corridor03", r.Said);
        // One step back: the dungeon as it was.
        Assert.True(r.Scene.Session!.Undo());
        Assert.Single(r.Rooms);
        Assert.Single(Saved(r));
    }

    [AvaloniaFact]
    public void ASelectedRoomTakenAwayByUndoIsNotSelectedAnyMore()
    {
        using var r = new SharedDungeonToolRun();
        r.T.Choose("cave_new_corridor03");
        r.PointAt(r.T.FreeEnds[0]);
        Assert.True(r.T.Click());
        Assert.Equal(1, r.T.Selected);
        // Undo takes the corridor away: index 1 names no room, and Delete removes nothing.
        Assert.True(r.Scene.Session!.Undo());
        Assert.Null(r.T.Selected);
        Assert.False(r.T.Delete());
        Assert.Single(r.Rooms);
        // Redo brings it back where it was: selected again.
        Assert.True(r.Scene.Session.Redo());
        Assert.Equal(1, r.T.Selected);
    }

    [AvaloniaFact]
    public void TurnJoinsByAnotherOpeningAndAnOverlapIsSaid()
    {
        using var r = new SharedDungeonToolRun();
        // A crossroads has four openings: Turn tries the next one at the same open end.
        r.T.Choose("cave_new_crossroads01");
        r.PointAt(r.T.FreeEnds[0]);
        var first = r.T.Preview!;
        r.T.Turn();
        var second = r.T.Preview!;
        Assert.True(Vector3.Distance(first.Position, second.Position) > 1 || Quaternion.Dot(first.Rotation, second.Rotation) < 0.99f);
        // A big room at an end that faces the entrance's other side runs into it.
        r.T.Choose("cave_new_sloperoom05");
        string? problem = null;
        foreach (int e in Enumerable.Range(0, r.T.FreeEnds.Count))
        {
            r.PointAt(r.T.FreeEnds[e]);
            for (int k = 0; k < 8 && problem == null; k++, r.T.Turn())
            {
                problem = r.T.Preview is { } p ? r.T.Problem(p)?.Shown : null;
            }
        }
        Assert.NotNull(problem);
    }

    [AvaloniaFact]
    public void CloseOpenEndsCapsThemAllInOneStep()
    {
        using var r = new SharedDungeonToolRun();
        int steps = r.Scene.Session!.UndoList.Count;
        Click(r.P.CloseEnds);
        Assert.Equal(4, r.Rooms.Count);
        Assert.Empty(r.T.FreeEnds);
        Assert.All(r.Rooms.Skip(1), p => Assert.True(p.Room!.EndCap));
        Assert.Equal(steps + 1, r.Scene.Session.UndoList.Count);
        Assert.False(r.P.CloseEnds.IsEnabled);
        Assert.Contains("Closed 3 open end(s)", r.Said);
    }

    [AvaloniaFact]
    public void AClickedRoomIsSelectedAndDeleted()
    {
        using var r = new SharedDungeonToolRun();
        Click(r.P.CloseEnds);
        // From above the middle of the entrance room: its box.
        r.T.Hover(At + new Vector3(0, 40, 0), -Vector3.UnitY);
        Assert.Equal(0, r.T.HoverRoom);
        Assert.True(r.T.Click());
        Assert.Equal(0, r.T.Selected);
        // A cap, from inside it (the camera in a room): the smallest box around the eye wins.
        var cap = r.Rooms[1];
        r.T.Hover(cap.Position + new Vector3(0, 1, 0), -Vector3.UnitY);
        Assert.Equal(1, r.T.HoverRoom);
        r.T.Click();
        Assert.Equal(1, r.T.Selected);
        Assert.True(r.P.DeleteButton.IsEnabled);
        Click(r.P.DeleteButton);
        Assert.Equal(3, r.Rooms.Count);
        Assert.Single(r.T.FreeEnds);
        Assert.Null(r.T.Selected);
    }
}
