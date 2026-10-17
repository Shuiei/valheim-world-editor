using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;
using ValheimGen;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A seed's world from above (the New world page's map): the colours of its cells, the view's arithmetic (fit, look at,
/// zoom around a point, kept in the world), the wheel, a drag and a double click, a resize, and the close look sampled
/// again from the generator when zoomed in.
/// </summary>
public class SeedMapTests
{
    /// <summary>
    /// A small preview of a seed, with its generator (for the close look).
    /// </summary>
    private static SeedPreview.Preview Preview(string seed = "abc", int size = 64) => SeedPreview.Make(seed, size, cancel: TestContext.Current.CancellationToken);

    /// <summary>
    /// A map alone in a shown window of that size, laid out and drawn once (so the pointer can hit it).
    /// </summary>
    private static OpenMap Open(int width = 400, int height = 300)
    {
        var map = new SeedMap();
        var w = new Window { Width = width, Height = height, Content = map };
        w.Show();
        Settle();
        return new OpenMap(w, map);
    }

    /// <summary>
    /// A map in its window; disposing it closes the window and stops the map's work (its timer, the close look being
    /// sampled), so nothing of a test runs on into the next.
    /// </summary>
    private sealed class OpenMap(Window window, SeedMap map) : IDisposable
    {
        /// <summary>
        /// The window and the map in it.
        /// </summary>
        public void Deconstruct(out Window w, out SeedMap m) => (w, m) = (window, map);

        /// <summary>
        /// Closes the window, then stops the map.
        /// </summary>
        public void Dispose()
        {
            window.Close();
            map.Dispose();
        }
    }

    /// <summary>
    /// The posted work run, and the window laid out and drawn (a resize is seen then).
    /// </summary>
    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void EachCellIsShadedByBiomeHeightAndDepth()
    {
        float water = TerrainService.WaterLevel;
        // Outside the world: the background.
        Assert.Equal(((byte)18, (byte)22, (byte)28), SeedMap.Shade(SeedPreview.Ocean, float.NegativeInfinity));
        // The sea: darker the deeper, down to 60 m.
        var shallow = SeedMap.Shade(SeedPreview.Ocean, water);
        var deep = SeedMap.Shade(SeedPreview.Ocean, water - 60);
        Assert.Equal(((byte)48, (byte)98, (byte)150), shallow);
        Assert.Equal(((byte)20, (byte)48, (byte)100), deep);
        Assert.Equal(deep, SeedMap.Shade(SeedPreview.Ocean, water - 500));
        // Land at the water's level: the game map's colour; higher, lighter (up to 120 m above).
        byte meadows = (byte)Array.IndexOf(SeedPreview.Biomes, Heightmap.Biome.Meadows);
        var (r, g, b) = MapData.PixelColor(Heightmap.Biome.Meadows);
        Assert.Equal((r, g, b), SeedMap.Shade(meadows, water));
        var (hr, hg, hb) = SeedMap.Shade(meadows, water + 60);
        Assert.True(hr > r && hg > g && hb > b);
        Assert.Equal(SeedMap.Shade(meadows, water + 120), SeedMap.Shade(meadows, water + 1000));
        // Water on land (a pool, a river): the biome under a blue tint.
        Assert.Equal(((byte)((r + 96) / 3), (byte)((g + 196) / 3), (byte)((b + 300) / 3)), SeedMap.Shade(meadows, water - 1));
    }

    [AvaloniaFact]
    public void APictureHasACellAPixel()
    {
        byte[] biome = new byte[6];
        float[] height = [.. Enumerable.Repeat(TerrainService.WaterLevel, 6)];
        using var bmp = SeedMap.Picture(biome, height, 3, 2);
        Assert.Equal(new PixelSize(3, 2), bmp.PixelSize);
    }

    [AvaloniaFact]
    public void TheWholeWorldFitsTheViewAndPointsGoBothWays()
    {
        using var open = Open(400, 300);
        var (_, map) = open;
        int changed = 0;
        map.ViewChanged += () => changed++;
        map.Show(Preview());
        Assert.True(changed > 0);
        // The world's diameter across the view's short side, its middle in the middle.
        Assert.Equal(2 * SeedPreview.Radius / 300, map.MetersPerPixel, 3);
        Assert.Equal((0f, 0f), (map.CenterX, map.CenterZ));
        Assert.Equal(new Point(200, 150), map.ScreenOf(0, 0));
        // North up: a point north of the middle is higher on the screen.
        Assert.True(map.ScreenOf(0, 1000).Y < 150);
        var (x, z) = map.WorldAt(map.ScreenOf(1234, -567));
        Assert.Equal(1234, x, 1);
        Assert.Equal(-567, z, 1);
    }

    [AvaloniaFact]
    public void LookingAtAPointIsHeldToTheWorldAndTheZoomsLimits()
    {
        using var open = Open(400, 300);
        var (_, map) = open;
        map.Show(Preview());
        map.LookAt(1000, -2000);
        Assert.Equal((1000f, -2000f, 4f), (map.CenterX, map.CenterZ, map.MetersPerPixel));
        // Not closer than half a metre a pixel, nor farther than the world a little smaller than the view.
        map.LookAt(0, 0, 0.01f);
        Assert.Equal(0.5f, map.MetersPerPixel);
        map.LookAt(0, 0, 1e6f);
        Assert.Equal(2.5f * SeedPreview.Radius / 300, map.MetersPerPixel, 3);
        // The middle stays in the world.
        map.LookAt(1e6f, -1e6f);
        Assert.Equal((SeedPreview.Radius, -SeedPreview.Radius), (map.CenterX, map.CenterZ));
    }

    [AvaloniaFact]
    public void ZoomingKeepsThePointUnderItWhereItWas()
    {
        using var open = Open(400, 300);
        var (_, map) = open;
        map.Show(Preview());
        map.LookAt(500, 500, 20);
        var at = new Point(100, 80);
        var (bx, bz) = map.WorldAt(at);
        map.Zoom(0.5, at);
        Assert.Equal(10, map.MetersPerPixel, 3);
        var (ax, az) = map.WorldAt(at);
        Assert.Equal(bx, ax, 1);
        Assert.Equal(bz, az, 1);
        // Around the middle when no point is given.
        var middle = (map.CenterX, map.CenterZ);
        map.Zoom(2);
        Assert.Equal(20, map.MetersPerPixel, 3);
        Assert.Equal(middle, (map.CenterX, map.CenterZ));
    }

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

    [AvaloniaFact]
    public async Task ZoomedInThePartInViewIsSampledAgainAndDroppedZoomedOut()
    {
        using var open = Open(160, 120);
        var (_, map) = open;
        // Cells fine enough that all the world in view is coarser than them.
        var p = Preview(size: 128);
        map.Show(p);
        // A metre a pixel, far finer than the preview's cells: sampled again after the view settles.
        map.LookAt(p.Stats.Start.X, p.Stats.Start.Z, 1);
        await TestWindows.Until(() => map.HasCloseLook, 60000);
        map.Dots = [(p.Stats.Start.X, p.Stats.Start.Z, Brushes.Red), (SeedPreview.Radius, SeedPreview.Radius, Brushes.Blue)];
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        // Zoomed out past the cells: dropped.
        map.Fit();
        await TestWindows.Until(() => !map.HasCloseLook);
        // Without a generator (a search's results), nothing is sampled.
        map.Show(p with { Generator = null });
        map.LookAt(0, 0, 1);
        await Task.Delay(400, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.False(map.HasCloseLook);
    }

    [AvaloniaFact]
    public async Task ClearedOrDisposedNothingIsLeftRunning()
    {
        using var open = Open(160, 120);
        var (_, map) = open;
        var p = Preview();
        map.Show(p);
        map.Clear();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        // A close look started, then the map disposed: it never comes.
        map.Show(p);
        map.LookAt(0, 0, 1);
        await Task.Delay(250, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        map.Dispose();
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.False(map.HasCloseLook);
    }

    [AvaloniaFact]
    public async Task ADisposedMapMovedOrResizedSamplesNothing()
    {
        using var open = Open(160, 120);
        var (w, map) = open;
        map.Show(Preview());
        map.Dispose();
        // Zoomed in and resized after: no close look is sampled any more.
        map.LookAt(0, 0, 1);
        w.Width = 200;
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Settle();
        }
        Assert.False(map.HasCloseLook);
    }
}
