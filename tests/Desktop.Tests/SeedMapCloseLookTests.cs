using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;
using static TerrainEditor.Desktop.Tests.SeedMapRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The seed map's close look: zoomed in the part in view is sampled again from the generator, zoomed out it is dropped,
/// and nothing is left running once the map is cleared or disposed.
/// </summary>
public class SeedMapCloseLookTests
{
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
