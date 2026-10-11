using Avalonia.Headless.XUnit;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The window's New world page: shown in place of the start page, a world made and opened from it, and a folder that
/// already holds a world refused with the page saying why.
/// </summary>
[Collection("World files")]
public class MainWindowNewWorldTests
{
    [AvaloniaFact]
    public async Task ANewWorldIsMadeAndOpened()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-newworld-" + Guid.NewGuid().ToString("N")[..8]);
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        try
        {
            w.Show();
            w.ShowNewWorld();
            Assert.NotNull(w.NewWorld);
            Assert.EndsWith("· A new world", w.Title);
            string folder = Path.Combine(dir, "Fresh");
            await w.CreateWorld(folder, "Fresh", "abc");
            // The page is gone; the world is open on its map.
            Assert.Null(w.NewWorld);
            Assert.NotNull(w.World);
            Assert.True(w.MapShown);
            Assert.StartsWith("Made Fresh (seed abc).", w.MessageText.Text);
            Assert.True(File.Exists(Path.Combine(folder, "Fresh.fwl")) || Directory.EnumerateFiles(folder).Any());
        }
        finally
        {
            // The world left (its session released) and the window closed before its folder goes.
            await w.LeaveWorld();
            w.CloseWithoutAsking();
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task AFolderHoldingAWorldIsNeverWrittenOver()
    {
        string dir = Fixtures.CopyFixture();
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        try
        {
            w.Show();
            w.ShowNewWorld();
            var page = w.NewWorld!;
            await w.CreateWorld(dir, "CITest", "abc");
            Assert.Same(page, w.NewWorld);
            Assert.Null(w.World);
            Assert.StartsWith("Could not make the world:", page.Problem.Text);
            Assert.Contains("already holds a world", page.Problem.Text);
        }
        finally
        {
            w.CloseWithoutAsking();
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
