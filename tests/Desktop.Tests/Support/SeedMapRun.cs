using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The seed map tests' setup: a seed's preview, a map alone in a shown window, and the posted work run and drawn.
/// </summary>
internal static class SeedMapRun
{
    /// <summary>
    /// A small preview of a seed, with its generator (for the close look).
    /// </summary>
    internal static SeedPreview.Preview Preview(string seed = "abc", int size = 64) => SeedPreview.Make(seed, size, cancel: TestContext.Current.CancellationToken);

    /// <summary>
    /// A map alone in a shown window of that size, laid out and drawn once (so the pointer can hit it).
    /// </summary>
    internal static SeedMapWindow Open(int width = 400, int height = 300)
    {
        var map = new SeedMap();
        var w = new Window { Width = width, Height = height, Content = map };
        w.Show();
        Settle();
        return new SeedMapWindow(w, map);
    }

    /// <summary>
    /// The posted work run, and the window laid out and drawn (a resize is seen then).
    /// </summary>
    internal static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
