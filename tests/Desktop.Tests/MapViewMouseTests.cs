using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The map with the mouse, through the surface laid over it (no world needed): the point under the pointer told, a
/// left drag sliding the map, a click picking a point (not a right click, nor a drag), and the wheel zooming around the
/// pointer.
/// </summary>
public class MapViewMouseTests
{
    /// <summary>
    /// A map and the surface over it alone in a shown 400 × 300 window, laid out and drawn once.
    /// </summary>
    private static (Window W, MapView Map) Open()
    {
        var map = new MapView();
        var surface = new Border { Background = Brushes.Transparent };
        map.Attach(surface);
        var w = new Window { Width = 400, Height = 300, Content = new Grid { Children = { map, surface } } };
        w.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        map.LookAt(1000, -500, 4);
        return (w, map);
    }

    [AvaloniaFact]
    public void ThePointerIsToldAndADragSlidesTheMap()
    {
        var (w, map) = Open();
        Vector2? hovered = null;
        int changed = 0;
        map.Hovered += (x, z) => hovered = new Vector2(x, z);
        map.ViewChanged += () => changed++;
        var at = new Point(250, 100);
        w.MouseMove(at);
        Assert.NotNull(hovered);
        Assert.Equal(map.WorldAt(at), hovered.Value);
        Assert.Equal(0, changed);
        // 40 px right and 20 down: the map's middle 160 m west and 80 m north.
        w.MouseDown(at, MouseButton.Left);
        w.MouseMove(at + new Point(40, 20));
        Assert.Equal(new Vector2(1000 - 160, -500 + 80), map.Center);
        Assert.True(changed > 0);
        bool picked = false;
        map.Picked += (_, _) => picked = true;
        w.MouseUp(at + new Point(40, 20), MouseButton.Left);
        // A drag is not a click; released, a move no longer slides.
        Assert.False(picked);
        w.MouseMove(at);
        Assert.Equal(new Vector2(1000 - 160, -500 + 80), map.Center);
        w.Close();
    }

    [AvaloniaFact]
    public void ALeftClickPicksAPointAndARightClickDoesNot()
    {
        var (w, map) = Open();
        (float X, float Z)? picked = null;
        map.Picked += (x, z) => picked = (x, z);
        var at = new Point(100, 200);
        w.MouseDown(at, MouseButton.Right);
        w.MouseUp(at, MouseButton.Right);
        Assert.Null(picked);
        w.MouseDown(at, MouseButton.Left);
        w.MouseUp(at + new Point(2, 1), MouseButton.Left);
        var expected = map.WorldAt(at + new Point(2, 1));
        Assert.NotNull(picked);
        Assert.Equal((expected.X, expected.Y), picked.Value);
        w.Close();
    }

    [AvaloniaFact]
    public void TheWheelZoomsAroundThePointer()
    {
        var (w, map) = Open();
        int changed = 0;
        map.ViewChanged += () => changed++;
        var at = new Point(300, 60);
        var under = map.WorldAt(at);
        w.MouseWheel(at, new Avalonia.Vector(0, 1));
        Assert.Equal(4 * 0.8f, map.MetersPerPixel, 4);
        Assert.Equal(under.X, map.WorldAt(at).X, 1);
        Assert.Equal(under.Y, map.WorldAt(at).Y, 1);
        Assert.Equal(1, changed);
        // Not closer than a quarter metre a pixel, nor farther than 40.
        w.MouseWheel(at, new Avalonia.Vector(0, 40));
        Assert.Equal(0.25f, map.MetersPerPixel);
        w.MouseWheel(at, new Avalonia.Vector(0, -60));
        Assert.Equal(40f, map.MetersPerPixel);
        w.Close();
    }
}
