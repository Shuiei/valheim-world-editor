using Avalonia.Controls;
using Avalonia.Threading;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The window under test: one shown and editing a made-up area, and waiting on its posted work, background tasks and
/// the dialogs it opens.
/// </summary>
internal static class TestWindows
{
    /// <summary>
    /// A shown window editing a flat area of <paramref name="size"/> × <paramref name="size"/> zones (ground at 30 m)
    /// with these objects.
    /// </summary>
    internal static (MainWindow W, EditSession S) EditingFlat(int size = 2, params WorldScene.Thing[] things)
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(size, things);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        return (w, s);
    }

    /// <summary>
    /// Waits for the window's posted work and background tasks.
    /// </summary>
    internal static async Task Until(Func<bool> done, int ms = 15000)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!done())
        {
            Assert.True(watch.ElapsedMilliseconds < ms, "timed out");
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
    }

    /// <summary>
    /// The dialog the owner shows (after the posted work that opens it, and the background work some do first, such as
    /// Settings finding the mods' extensions).
    /// </summary>
    internal static Window Shown(Window owner, int ms = 15000)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Dispatcher.UIThread.RunJobs();
        while (owner.OwnedWindows.Count == 0 && watch.ElapsedMilliseconds < ms)
        {
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }
        return Assert.Single(owner.OwnedWindows);
    }
}
