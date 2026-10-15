using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The window's world pages and the map's actions: what does nothing without a world or an area, the file pickers with
/// nobody to choose, an area that cannot be loaded, the map closing the area, Discard, Leave, Reload and Save from the
/// map (offline and live, and a game that stops answering), quitting over changes not applied, and errors said once in a
/// dialog away from the 3D editor.
/// </summary>
[Collection("World files")]
public class MainWindowWorldPagesTests
{
    [AvaloniaFact]
    public async Task WithoutAWorldOrAnAreaTheActionsDoNothingAndThePickersGiveNothing()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        int told = 0;
        w.Tell = _ => { told++; return Task.CompletedTask; };
        w.Ask = (_, _, _, _) => throw new InvalidOperationException("nothing to ask");
        // The map without a world: the start page.
        w.ShowMap();
        Assert.NotNull(w.StartPage);
        Assert.False(w.MapShown);
        await w.SaveWorld();
        await w.DiscardWorld();
        await w.ReloadWorld();
        await w.LeaveWorld();
        Assert.Null(w.World);
        // The editor's actions without an area.
        w.MessageText.Text = "";
        await w.Save();
        await w.DiscardHere();
        await w.ResizeArea(3);
        w.StampAt(10, 10);
        w.ApplyPath();
        w.Claim();
        w.Inspect();
        Assert.Equal("", w.MessageText.Text);
        Assert.Equal(0, told);
        // Nobody chooses a file or a folder (the headless window has no picker).
        Assert.Null(await w.StartPage!.PickFolder("A folder"));
        Assert.Null(await w.StartPage.PickFile("A file"));
        Assert.Null(await w.Blueprints.PickFile());
        Assert.Null(await w.AreaPanel.PickPicture());
        Assert.Null(await w.AreaPanel.PickFolder());
        int stamps = w.Tools.StampList.Count;
        w.Tools.LoadStampButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(stamps, w.Tools.StampList.Count);
    }

    [AvaloniaFact]
    public async Task AnAreaThatCannotBeLoadedIsSaidAndAResizeToItsSizeDoesNothing()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        var scene = r.W.View.Scene;
        await r.W.ResizeArea(1);
        Assert.Same(scene, r.W.View.Scene);
        var s = r.W.Session;
        await r.W.EditBlock(0, 0, -1, -1);
        Assert.StartsWith("Could not open that area: ", r.Said);
        Assert.False(r.W.IsBusy);
        Assert.Same(scene, r.W.View.Scene);
        // The area shown stays open: its edits and history go on in it.
        Assert.Same(s, r.W.Session);
        Assert.Same(s, r.W.World!.Area);
    }

    // The map shown while another area loads: that load failing does not open the area left again behind the map.
    [AvaloniaFact]
    public async Task AnAreaThatFailsToLoadAfterTheMapWasShownLeavesTheAreaClosed()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        var load = r.W.EditBlock(0, 0, -1, -1);
        r.W.ShowMap();
        await load;
        Assert.StartsWith("Could not open that area: ", r.Said);
        Assert.True(r.W.MapShown);
        Assert.Null(r.W.Session);
        Assert.Null(r.W.World!.Area);
        Assert.NotNull(r.W.World.History);
    }

    // The map closes the area: a script still running in it changes nothing, and its history stays with the world.
    [AvaloniaFact]
    public async Task TheMapClosesTheAreaSoAScriptStillRunningChangesNothing()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        var s = r.W.Session!;
        var run = r.W.RunScriptCode("Ground.Raise(32, 32, 1);", "Script: late");
        r.W.ShowMap();
        Assert.Null(r.W.Session);
        Assert.Null(r.W.World!.Area);
        Assert.NotNull(r.W.World.History);
        var outcome = await run;
        Assert.False(outcome.Changed);
        // It compiled and ran, and only then found its area closed.
        Assert.Equal("The area was closed while the script ran: nothing changed.", outcome.Message);
        Assert.Empty(s.UndoList);
        Assert.Equal((0, 0, 0, 0), r.W.World.Pending);
    }

    [AvaloniaFact]
    public async Task OfflineTheMapAsksBeforeDiscardingOrLeavingAndHasNothingToReload()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        r.Raise();
        r.W.ShowMap();
        Assert.True(r.W.MapShown);
        await r.W.DiscardWorld();
        Assert.Equal("Discard the unsaved changes (1 zone(s) of ground)? The world is read again from disk.", Assert.Single(r.Asked));
        await r.W.LeaveWorld();
        Assert.Equal("Unsaved: 1 zone(s) of ground. Leave the world without saving them?", r.Asked[^1]);
        Assert.NotNull(r.W.World);
        Assert.Equal(1, r.W.World!.Pending.Zones);
        // Reload is for a live world.
        await r.W.ReloadWorld();
        Assert.Equal(2, r.Asked.Count);
        Assert.Equal(1, r.W.World.Pending.Zones);
        // Quitting asks too; no keeps the window open.
        r.W.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Unsaved: 1 zone(s) of ground. Quit without saving them?", r.Asked[^1]);
        Assert.True(r.W.IsVisible);
    }

    [AvaloniaFact]
    public async Task AnErrorIsSaidInTheStatusBarAndOnceInADialogAwayFromTheEditor()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        // In the 3D editor: the status bar only.
        r.W.ShowError(new InvalidOperationException("boom"));
        Assert.Equal("Something went wrong: boom Your changes are still here. Details in the log (Open log, on the start page).", r.Said);
        Assert.Empty(r.Told);
        // On the map: a dialog, one at a time.
        r.W.ShowMap();
        r.W.ShowError(new InvalidOperationException("one"));
        r.W.ShowError(new InvalidOperationException("two"));
        Assert.StartsWith("Something went wrong: one", Assert.Single(r.Told));
        Assert.StartsWith("Something went wrong: two", r.Said);
        // Once that dialog is closed, the next error has its own.
        Dispatcher.UIThread.RunJobs();
        r.W.ShowError(new InvalidOperationException("three"));
        Assert.Equal(2, r.Told.Count);
    }

    [AvaloniaFact]
    public async Task LiveTheMapAppliesAsksInItsOwnWordsAndSaysWhenTheGameStopsAnswering()
    {
        using var r = new WorldWindowRun();
        await r.OpenLive();
        r.Raise();
        r.W.ShowMap();
        await r.W.LeaveWorld();
        Assert.Equal("Not applied: 1 zone(s) of ground. Leave the world without applying them?", Assert.Single(r.Asked));
        await r.W.DiscardWorld();
        Assert.Equal("Discard the unsaved changes (1 zone(s) of ground)? The world is read again from the game.", r.Asked[^1]);
        // Quitting with changes not applied.
        r.W.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Not applied: 1 zone(s) of ground. Quit without applying them?", r.Asked[^1]);
        Assert.True(r.W.IsVisible);
        // Save on the map: applied to the game, then said.
        await r.W.SaveWorld();
        Assert.Single(r.Told);
        Assert.Single(r.Game!.Terrain);
        Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
        Assert.False(r.W.IsBusy);
        // Reload reads the game again; a game that is gone is said.
        int snapshots = r.Game.Snapshots;
        await r.W.ReloadWorld();
        Assert.True(r.Game.Snapshots > snapshots);
        r.StopGame();
        await r.W.ReloadWorld();
        Assert.StartsWith("Could not reach the game: ", r.Said);
        Assert.False(r.W.IsBusy);
    }
}
