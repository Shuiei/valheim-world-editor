using Avalonia.Controls;
using Avalonia.Threading;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Waiting on the window under test: its posted work, background tasks and the dialogs it opens.
/// </summary>
internal static class TestWindows
{
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
    /// The dialog the owner shows (after the posted work that opens it).
    /// </summary>
    internal static Window Shown(Window owner)
    {
        Dispatcher.UIThread.RunJobs();
        return Assert.Single(owner.OwnedWindows);
    }
}
