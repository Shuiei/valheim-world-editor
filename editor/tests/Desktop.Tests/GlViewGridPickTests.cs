using System.Numerics;
using Avalonia.Headless.XUnit;
using Xunit;
using static TerrainEditor.Desktop.Tests.BareGlView;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A grid point's place on the view and back (GlView.ScreenOfGrid and GridAt): the point picked at its own screen place
/// is the point, and a point behind the camera has no place on the view.
/// </summary>
public class GlViewGridPickTests
{
    [AvaloniaFact]
    public void AGridPointPicksBackFromItsScreenPlace()
    {
        var b = new BareGlView(TestScenes.Flat(2));
        var grid = new Vector2(40, 50);
        // On the ground itself (no lift): a point lifted above it picks back a little off, along the view's slant.
        var at = b.V.ScreenOfGrid(grid, 0);
        Assert.NotNull(at);
        var back = b.V.GridAt(at.Value, ViewSize);
        Assert.NotNull(back);
        Assert.Equal(grid.X, back.Value.X, 1);
        Assert.Equal(grid.Y, back.Value.Y, 1);
    }

    [AvaloniaFact]
    public void AGridPointBehindTheCameraHasNoPlaceOnTheView()
    {
        var b = new BareGlView(TestScenes.Flat(2));
        // Looking up from under the ground: the ground lies behind the eye.
        b.V.SetCamera(new Vector3(0, 20, 0), new Vector3(0, 0, 1), (float)(ViewSize.Width / ViewSize.Height));
        Assert.Null(b.V.ScreenOfGrid(new Vector2(64, 64)));
    }
}
