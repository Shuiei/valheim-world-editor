using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia;
using TerrainEditor.App;
using Xunit;
using static TerrainEditor.Desktop.Tests.SeedMapRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The seed map's pointer and window: the wheel, a drag, a double click, and a resize that keeps the whole world in view
/// until the view is moved.
/// </summary>
public class SeedMapInputTests
{
    [AvaloniaFact]
    public void TheWheelZoomsADragMovesAndTheMouseIsTold()
    {
        using var open = Open(400, 300);
        var (w, map) = open;
        map.Show(Preview());
        map.LookAt(0, 0, 20);
        (float X, float Z)? hovered = null;
        map.Hovered += (x, z) => hovered = (x, z);
        // The wheel up: closer, around the pointer.
        var at = new Point(300, 100);
        var (ux, _) = map.WorldAt(at);
        w.MouseWheel(at, new Vector(0, 1));
        Assert.Equal(20 / 1.25f, map.MetersPerPixel, 3);
        Assert.Equal(ux, map.WorldAt(at).X, 1);
        // The pointer's world point is told.
        w.MouseMove(new Point(200, 150));
        Assert.NotNull(hovered);
        Assert.Equal(map.WorldAt(new Point(200, 150)).X, hovered.Value.X, 1);
        // A left drag slides the world with the pointer: 40 px right and 20 down moves the middle west and north.
        float x0 = map.CenterX, z0 = map.CenterZ, mpp = map.MetersPerPixel;
        w.MouseDown(new Point(200, 150), MouseButton.Left);
        w.MouseMove(new Point(240, 170));
        Assert.Equal(x0 - 40 * mpp, map.CenterX, 1);
        Assert.Equal(z0 + 20 * mpp, map.CenterZ, 1);
        w.MouseUp(new Point(240, 170), MouseButton.Left);
        // Released: moving no longer drags.
        float x1 = map.CenterX;
        w.MouseMove(new Point(100, 100));
        Assert.Equal(x1, map.CenterX);
    }

    [AvaloniaFact]
    public void ADoubleClickShowsTheWholeWorldAgain()
    {
        using var open = Open(400, 300);
        var (w, map) = open;
        map.Show(Preview());
        map.LookAt(3000, 3000, 2);
        var p = new Point(200, 150);
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Assert.Equal((0f, 0f), (map.CenterX, map.CenterZ));
        Assert.Equal(2 * SeedPreview.Radius / 300, map.MetersPerPixel, 3);
    }

    [AvaloniaFact]
    public void AResizeKeepsTheWholeWorldInViewUntilTheViewIsMoved()
    {
        using var open = Open(400, 300);
        var (w, map) = open;
        map.Show(Preview());
        w.Height = 200;
        Settle();
        Assert.Equal(2 * SeedPreview.Radius / 200, map.MetersPerPixel, 3);
        // Moved: a resize keeps the zoom.
        map.LookAt(100, 100, 8);
        w.Height = 250;
        Settle();
        Assert.Equal(8, map.MetersPerPixel);
        // The same seed shown again keeps the view; another seed shows all of it.
        map.Show(Preview(), keepView: true);
        Assert.Equal((100f, 100f, 8f), (map.CenterX, map.CenterZ, map.MetersPerPixel));
        map.Show(Preview("abd"));
        Assert.Equal((0f, 0f), (map.CenterX, map.CenterZ));
    }
}
