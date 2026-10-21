using Avalonia.Controls;
using Avalonia.Threading;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The window under test: opening one on an area, and waiting on its posted work, background tasks and the dialogs it
/// opens.
/// </summary>
internal static class TestWindows
{
    /// <summary>
    /// A shown 1600 × 1000 main window editing the area given.
    /// </summary>
    internal static MainWindow Editing(EditSession s)
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        w.View.Show(s.Scene, null);
        w.Edit(s);
        return w;
    }

    /// <summary>
    /// A shown 1600 × 1000 main window editing a flat 2 × 2 zone area (ground at 30 m) with these objects, and the area's
    /// edit session.
    /// </summary>
    internal static (MainWindow W, EditSession S) EditingFlat(params WorldScene.Thing[] things)
    {
        var s = TestScenes.Flat(2, things);
        return (Editing(s), s);
    }

    /// <summary>
    /// A shown 1600 × 1000 main window with the world at <paramref name="dir"/> (a copy of the test world) open, and no
    /// area edited.
    /// </summary>
    /// <param name="dir">The world's folder.</param>
    internal static async Task<MainWindow> OpenedWorld(string dir)
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        await w.OpenWorld(() => Task.Run(() => WorldSession.Open(dir)), "Opening…");
        return w;
    }

    /// <summary>
    /// A shown 1600 × 1000 main window with the world at <paramref name="dir"/> (a copy of the test world) open and its
    /// area of <paramref name="size"/> zones across, from zone (<paramref name="x"/>, <paramref name="z"/>), edited.
    /// </summary>
    /// <param name="dir">The world's folder.</param>
    /// <param name="x">The zone the area starts at, across.</param>
    /// <param name="z">The zone the area starts at, down.</param>
    /// <param name="size">The area's size in zones.</param>
    internal static async Task<MainWindow> EditingWorld(string dir, int x = 0, int z = 0, int size = 1)
    {
        var w = await OpenedWorld(dir);
        await w.EditArea(x, z, size);
        return w;
    }

    /// <summary>
    /// A shown window on zone 0, 0 of a copy of the test world, in the Area tool; with the copy's folder, and the zone's
    /// edit session.
    /// </summary>
    internal static (MainWindow W, EditSession S, string Dir) AreaToolOnWorldCopy()
    {
        string dir = Fixtures.CopyFixture();
        var scene = WorldScene.Load(dir, 0, 0, 1);
        var w = Editing(scene.Session!);
        w.Tools.ChooseMode(ToolMode.Area);
        return (w, scene.Session!, dir);
    }

    /// <summary>
    /// A shown window with the world at <paramref name="dir"/> (a copy of the test world) open, its first zone's area
    /// edited and its ground raised there: one zone not saved. What it tells is let go.
    /// </summary>
    /// <param name="dir">The world's folder.</param>
    internal static async Task<MainWindow> ChangedWorld(string dir)
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        w.Tell = _ => Task.CompletedTask;
        await w.OpenWorld(() => Task.Run(() => WorldSession.Open(dir)), "Opening…");
        await w.EditArea(0, 0, 1);
        w.Session!.Shape(32, 32, Formula.Compile("2", []), 3, 0, "raise");
        return w;
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
