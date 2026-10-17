using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Auto apply in the 3D editor: every change and undo is sent to the game, the sending waits for the end of a
/// stroke, nothing is sent offline, and changes asked while one run is going are merged.
/// </summary>
[Collection("World files")]
public class AutoApplyTests
{
    [AvaloniaFact]
    public async Task AutoSendsEveryChangeAndUndoToTheGame()
    {
        using var r = new WorldWindowRun();
        await r.OpenLive();
        Assert.True(r.W.AutoApplyBox.IsVisible);
        r.Raise();
        Assert.Equal(1, r.W.World!.Pending.Zones);
        // Switched on with something waiting: sent at once, and remembered.
        r.W.AutoApplyBox.IsChecked = true;
        await TestWindows.Until(() => r.W.World.Pending == (0, 0, 0, 0));
        Assert.True(r.W.Settings.AutoApply);
        Assert.Equal(1, r.Saves);
        Assert.Single(r.Game!.Terrain);
        // A new change, then its undo: each goes to the game by itself.
        r.Raise(40, 40, "second");
        await TestWindows.Until(() => r.Game.Terrain.Count == 2 && r.W.World.Pending == (0, 0, 0, 0));
        await r.W.Undo();
        await TestWindows.Until(() => r.Game.Terrain.Count == 3 && r.W.World.Pending == (0, 0, 0, 0));
        // Off: changes wait again.
        r.W.AutoApplyBox.IsChecked = false;
        Assert.False(r.W.Settings.AutoApply);
        r.Raise(10, 10, "third");
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, r.W.World.Pending.Zones);
        Assert.Equal(3, r.Game.Terrain.Count);
    }

    [AvaloniaFact]
    public async Task AutoWaitsForTheEndOfAStroke()
    {
        using var r = new WorldWindowRun();
        await r.OpenLive();
        r.W.AutoApplyBox.IsChecked = true;
        var s = r.W.Session!;
        s.Brush.Radius = 4;
        s.BeginStroke(BrushTool.Raise, 32, 32);
        s.StrokeStep(32, 32, 0.1f);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Empty(r.Game!.Terrain);
        s.EndStroke();
        await TestWindows.Until(() => r.Game.Terrain.Count == 1 && r.W.World!.Pending == (0, 0, 0, 0));
    }

    [AvaloniaFact]
    public async Task AutoDoesNothingOffline()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        r.W.Settings.AutoApply = true;
        r.Raise();
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, r.W.World!.Pending.Zones);
        // Apply now on an offline world does nothing either.
        await r.W.ApplyNow();
        Assert.Equal(1, r.W.World.Pending.Zones);
    }

    [AvaloniaFact]
    public async Task AppliesAskedWhileOneRunsAreMerged()
    {
        using var r = new WorldWindowRun();
        await r.OpenLive();
        r.Raise();
        var first = r.W.ApplyNow();
        r.Raise(40, 40, "during");
        var second = r.W.ApplyNow();
        Assert.True(second.IsCompleted);
        await first;
        Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
        // The second change went with the first apply or with the one merged after it.
        Assert.InRange(r.Game!.Terrain.Count, 1, 2);
        // The Save button (Apply live) goes the same way.
        r.Raise(10, 10, "button");
        await r.W.Save();
        Assert.Equal((0, 0, 0, 0), r.W.World.Pending);
    }
}
