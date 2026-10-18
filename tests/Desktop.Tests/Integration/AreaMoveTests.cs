using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Moving the edited area by one zone with the arrows, growing and shrinking it, and the note about the locations
/// inside it.
/// </summary>
[Collection("World files")]
public class AreaMoveTests
{
    /// <summary>
    /// Raises the button's click (no posted work run).
    /// </summary>
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public async Task TheArrowsMoveTheAreaOneZoneKeepingTheViewAndTheEdits()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        Assert.True(r.W.AreaNav.IsVisible);
        r.Raise();
        var (yaw, pitch, distance, world) = r.W.View.WorldCamera!.Value;
        Click(r.W.AreaEast);
        await TestWindows.Until(() => r.W.View.Scene!.X0 == 1);
        Assert.Equal(0, r.W.View.Scene!.Z0);
        var (_, _, nowDistance, nowWorld) = r.W.View.WorldCamera!.Value;
        Assert.Equal(world.X, nowWorld.X, 3);
        Assert.Equal(world.Z, nowWorld.Z, 3);
        Assert.Equal(distance, nowDistance);
        Assert.Equal(1, r.W.World!.Pending.Zones);
        await r.W.MoveArea(0, 1);
        Assert.Equal((1, 1), (r.W.View.Scene!.X0, r.W.View.Scene.Z0));
        Click(r.W.AreaSouth);
        await TestWindows.Until(() => r.W.View.Scene!.Z0 == 0);
        Click(r.W.AreaWest);
        await TestWindows.Until(() => r.W.View.Scene!.X0 == 0);
        // Back where the change was made: it is still waiting.
        Assert.Equal(1, r.W.World.Pending.Zones);
        Click(r.W.AreaNorth);
        await TestWindows.Until(() => r.W.View.Scene!.Z0 == 1);
    }

    [AvaloniaFact]
    public async Task ALargerAreaMovesByOneZoneToo()
    {
        using var r = new WorldWindowRun();
        await r.Open(0, 0, 3);
        Assert.Equal(-1, r.W.View.Scene!.X0);
        await r.W.MoveArea(-1, 0);
        Assert.Equal(-2, r.W.View.Scene!.X0);
        Assert.Equal((3, 3), (r.W.View.Scene.ZonesX, r.W.View.Scene.ZonesZ));
    }

    // A path drawn (or anything else unfinished, as for a resize) keeps the area where it is, and says why.
    [AvaloniaFact]
    public async Task TheAreaDoesNotMoveWithAPathDrawn()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        r.W.View.Path.Points.Add(new Vector2(20, 20));
        await r.W.MoveArea(1, 0);
        Assert.Equal(0, r.W.View.Scene!.X0);
        Assert.Equal("The area cannot move now: a path is drawn (apply or clear it).", r.W.MessageText.Text);
        Assert.Single(r.W.View.Path.Points);
    }

    // The area made bigger (or smaller) on the fly, around the same middle zone, keeping the view and
    // what waits to be saved; not while something unfinished would be lost.
    [AvaloniaFact]
    public async Task TheAreaGrowsAndShrinksOnTheFly()
    {
        using var r = new WorldWindowRun();
        await r.Open(0, 0, 3);
        Assert.Equal(0, r.W.AreaSizeBox.SelectedIndex);
        r.Raise();
        var (yaw, pitch, distance, world) = r.W.View.WorldCamera!.Value;
        r.W.AreaSizeBox.SelectedIndex = 2;
        // 49 zones to make: over 15 s under coverage on CI's two cores.
        await TestWindows.Until(() => r.W.View.Scene!.ZonesX == 7, 60000);
        Assert.Equal((-3, -3), (r.W.View.Scene!.X0, r.W.View.Scene.Z0));
        Assert.Equal(world.X, r.W.View.WorldCamera!.Value.World.X, 3);
        Assert.Equal(1, r.W.World!.Pending.Zones);
        await r.W.ResizeArea(5);
        Assert.Equal((5, 5, -2), (r.W.View.Scene!.ZonesX, r.W.View.Scene.ZonesZ, r.W.View.Scene.X0));
        Assert.Equal(1, r.W.AreaSizeBox.SelectedIndex);
        // Something selected: it waits, and says why.
        r.W.View.Select([0]);
        if (r.W.View.Selected.Count > 0)
        {
            await r.W.ResizeArea(7);
            Assert.Equal(5, r.W.View.Scene!.ZonesX);
            Assert.StartsWith("The area cannot change size now", r.W.MessageText.Text);
            Assert.Equal(1, r.W.AreaSizeBox.SelectedIndex);
        }
    }

    [AvaloniaFact]
    public async Task NoAreaNoMove()
    {
        using var r = new WorldWindowRun();
        await r.W.MoveArea(1, 0);
        Assert.Null(r.W.View.Scene);
        Assert.Null(await r.W.CheckFollow());
    }

    [Fact]
    public void LocationsInOrNearTheAreaAreCounted()
    {
        var s = TestScenes.FlatScene();
        var world = new WorldSave { Directory = "none", SaveNumber = 1 };
        // Area 0, 0 spans -32..33 m; 40 m more around it counts.
        world.Locations.Add((new Vector3(0, 30, 0), 1));
        world.Locations.Add((new Vector3(-70, 30, 70), 2));
        world.Locations.Add((new Vector3(-73, 30, 0), 3));
        world.Locations.Add((new Vector3(500, 30, 0), 4));
        s.World = world;
        Assert.Equal(2, Overlays.LocationsNear(s));
        Assert.Equal(Overlays.Build(s, null, _ => null).Locations, Overlays.LocationsNear(s));
        Assert.Equal(0, Overlays.LocationsNear(TestScenes.FlatScene()));
    }

    [AvaloniaFact]
    public async Task TheLocationsNoteShowsWhenTheAreaHasSome()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        Assert.False(r.W.LocationNote.IsVisible);
        // Two locations in the area (the test world has none of its own).
        r.W.World!.World.Locations.Add((new Vector3(5, 30, 5), 1));
        r.W.World.World.Locations.Add((new Vector3(-20, 30, 10), 2));
        await r.W.EditArea(0, 0, 1);
        int n = Overlays.LocationsNear(r.W.View.Scene!);
        Assert.Equal(2, n);
        Assert.True(r.W.LocationNote.IsVisible);
        Assert.Equal($"{n} location(s) here. The ground already includes the flattening the game does around them (turn on Location markers in View to see where).", r.W.LocationText.Text);
        // Closed with its ×.
        var close = ((Grid)r.W.LocationNote.Child!).Children.OfType<Button>().Single();
        Click(close);
        Assert.False(r.W.LocationNote.IsVisible);
        // An area far from any: no note.
        await r.W.EditArea(5, 5, 1);
        Assert.False(r.W.LocationNote.IsVisible);
        Assert.Equal("", r.W.LocationText.Text);
    }
}
