using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The area following the view: it grows toward the edge the view nears, and waits or does nothing when it cannot.
/// </summary>
[Collection("World files")]
public class AreaFollowTests
{
    /// <summary>
    /// Raises the button's click (no posted work run).
    /// </summary>
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>
    /// Moves the view's target to world point (wx, wz), keeping its turn, pitch and distance.
    /// </summary>
    private static void LookAt(MainWindow w, float wx, float wz)
    {
        var (yaw, pitch, distance, _) = w.View.WorldCamera!.Value;
        w.View.WorldCamera = (yaw, pitch, distance, new Vector3(wx, 30, wz));
    }

    [AvaloniaFact]
    public async Task FollowGrowsTheAreaWhenTheViewNearsItsEdge()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        // Off: nothing changes.
        LookAt(r.W, 28, 0);
        Assert.Null(await r.W.CheckFollow());
        r.W.SetFollow(true);
        Assert.True(r.W.Settings.AreaFollow);
        Assert.Equal(1, r.Saves);
        Assert.Contains("on", r.W.FollowButton.Classes);
        Assert.Contains("Follow: the area grows", r.W.MessageText.Text);
        // In the middle: stays.
        LookAt(r.W, 0, 0);
        Assert.Null(await r.W.CheckFollow());
        // Within 12 m of the east edge: a zone more east, zone 0 kept, the view where it was.
        LookAt(r.W, 25, 3);
        Assert.Equal((0, 0, 2, 1), await r.W.CheckFollow());
        Assert.Equal((0, 0, 2, 1), (r.W.View.Scene!.X0, r.W.View.Scene.Z0, r.W.View.Scene.ZonesX, r.W.View.Scene.ZonesZ));
        Assert.Equal(25, r.W.View.WorldCamera!.Value.World.X, 2);
        // Near the south-east corner (the area is x -32..96, z -32..32): both ways.
        LookAt(r.W, 90, -27);
        Assert.Equal((0, -1, 3, 2), await r.W.CheckFollow());
        Assert.Equal(90, r.W.View.WorldCamera!.Value.World.X, 2);
        // Off again (the button): stays.
        Click(r.W.FollowButton);
        Assert.False(r.W.Settings.AreaFollow);
        Assert.Contains("Follow is off", r.W.MessageText.Text);
        LookAt(r.W, -25, -64);
        Assert.Null(await r.W.CheckFollow());
    }

    [AvaloniaFact]
    public async Task FollowInALargerAreaGrowsItByOneRow()
    {
        using var r = new WorldWindowRun();
        await r.Open(0, 0, 3);
        r.W.SetFollow(true);
        // West edge of zones -1..1 is at -96: the view at -88 adds zone column -2.
        LookAt(r.W, -88, 0);
        Assert.Equal((-2, -1, 4, 3), await r.W.CheckFollow());
        Assert.Equal(4, r.W.View.Scene!.ZonesX);
    }

    [Fact]
    public void FollowGrowsUpToTheMostZonesAndKeepsThem()
    {
        int first = 0, count = MainWindow.MaxFollowZones - 1;
        Assert.True(MainWindow.Grow(ref first, ref count, up: false));
        Assert.Equal((-1, MainWindow.MaxFollowZones), (first, count));
        // At the most: it stops, no zone left behind.
        Assert.False(MainWindow.Grow(ref first, ref count, up: true));
        Assert.Equal((-1, MainWindow.MaxFollowZones), (first, count));
        Assert.False(MainWindow.Grow(ref first, ref count, up: false));
        Assert.Equal((-1, MainWindow.MaxFollowZones), (first, count));
    }

    [Fact]
    public void FollowNearACornerGrowsTheWaysThatAreNotFull()
    {
        int most = MainWindow.MaxFollowZones;
        // Both ways near a corner: one row more each (west and north here).
        Assert.Equal((-1, 0, 3, 3), MainWindow.Grown((0, 0, 2, 2), upX: false, upZ: true));
        // East at the most: only north grows, east keeps its zones.
        Assert.Equal((0, 0, most, 3), MainWindow.Grown((0, 0, most, 2), upX: true, upZ: true));
        // North at the most: only west grows.
        Assert.Equal((-1, 0, 3, most), MainWindow.Grown((0, 0, 2, most), upX: false, upZ: true));
        // Both at the most, or near no edge: nothing.
        Assert.Null(MainWindow.Grown((0, 0, most, most), upX: true, upZ: false));
        Assert.Null(MainWindow.Grown((0, 0, 2, 2), upX: null, upZ: null));
    }

    [AvaloniaFact]
    public async Task FollowWaitsWhileSomethingWouldBeLost()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        r.W.SetFollow(true);
        LookAt(r.W, 26, 0);
        // A selection.
        int i = r.W.View.Scene!.Things.FindIndex(t => !t.Gone);
        Assert.True(i >= 0);
        r.W.View.Select([i]);
        Assert.Null(await r.W.CheckFollow());
        Assert.Equal("Follow waits: objects are selected (Alt + A in Select clears them).", r.W.MessageText.Text);
        // Said once while the reason stays.
        r.W.MessageText.Text = "other";
        Assert.Null(await r.W.CheckFollow());
        Assert.Equal("other", r.W.MessageText.Text);
        r.W.View.Select([]);
        // A path drawn.
        r.W.View.Path.Points.Add(new Vector2(10, 10));
        Assert.Null(await r.W.CheckFollow());
        Assert.Equal("Follow waits: a path is drawn (apply or clear it).", r.W.MessageText.Text);
        r.W.View.Path.Points.Clear();
        // A stroke on.
        r.W.Session!.BeginStroke(BrushTool.Raise, 32, 32);
        Assert.Equal("a stroke is on", r.W.MoveBlocker()?.English);
        r.W.Session.EndStroke();
        Assert.Null(r.W.MoveBlocker());
        // Nothing in the way: it grows.
        Assert.Equal((0, 0, 2, 1), await r.W.CheckFollow());
    }

    [AvaloniaFact]
    public async Task FollowWaitsWhileChangesAreSent()
    {
        using var r = new WorldWindowRun();
        await r.OpenLive();
        r.Raise();
        var apply = r.W.ApplyNow();
        Assert.Equal("changes are still being sent", r.W.MoveBlocker()?.English);
        await apply;
        Assert.Null(r.W.MoveBlocker());
    }

    [AvaloniaFact]
    public async Task FollowDoesNothingOnTheMap()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        r.W.SetFollow(true);
        LookAt(r.W, 26, 0);
        r.W.ShowMap();
        Assert.Null(await r.W.CheckFollow());
    }
}
