using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The 3D editor and the world behind it: the history kept from an earlier session said when the area opens (and asked
/// about before undoing it when the game saved the world since), Discard that must read the world again from files
/// that are gone, and an area that does not open again after the world was read again.
/// </summary>
[Collection("World files")]
public class MainWindowEditorWorldTests
{
    [AvaloniaFact]
    public async Task TheHistoryOfAnEarlierSessionIsSaidAndAskedAboutOnceTheWorldWasSavedAgain()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        r.Raise();
        await r.W.Save();
        Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
        await r.W.LeaveWorld();
        Assert.Null(r.W.World);
        // The game saved the world since (its latest save is not the editor's any more).
        string latest = Directory.GetFiles(r.Dir, "_main.*.chunks").OrderBy(f => f, StringComparer.Ordinal).Last();
        File.SetLastWriteTimeUtc(latest, DateTime.UtcNow.AddMinutes(5));
        await r.Open();
        Assert.StartsWith("History from an earlier session (", r.Said);
        Assert.Contains("1 step(s) you can undo, in History.", r.Said);
        Assert.Contains("The world was saved again since: you are asked before undoing them.", r.Said);
        var s = r.W.Session!;
        Assert.Single(s.UndoList);
        // No: nothing changes.
        r.Answers.Enqueue(false);
        await r.W.Undo();
        Assert.Contains("earlier session", Assert.Single(r.Asked));
        Assert.Equal("Nothing changed.", r.Said);
        Assert.Single(s.UndoList);
        // Yes: undone, and not asked again for this world.
        r.Answers.Enqueue(true);
        await r.W.Undo();
        Assert.Empty(s.UndoList);
        Assert.True(r.W.World!.EarlierAllowed);
        await r.W.Redo();
        Assert.Equal(2, r.Asked.Count);
        Assert.Single(s.UndoList);
    }

    [AvaloniaFact]
    public async Task WhileSavingTheKeysAndFollowWait()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        r.Raise();
        int asked = 0;
        r.W.ConfirmSave = _ =>
        {
            asked++;
            return Task.FromResult(true);
        };
        // The result stays on screen until this is set: the veil stays up with it, whenever the write ends.
        var read = new TaskCompletionSource();
        r.W.Tell = text =>
        {
            r.Told.Add(text);
            return read.Task;
        };
        var saving = r.W.Save();
        // The save is being written (or its result read): the veil is up, Ctrl+S does not ask again, and the area does
        // not move.
        Assert.True(r.W.IsBusy);
        r.W.KeyPress(Key.S, RawInputModifiers.Control, PhysicalKey.S, "s");
        Assert.Equal(1, asked);
        Assert.Equal("the world is being saved or read", r.W.MoveBlocker());
        read.SetResult();
        await saving;
        Assert.Single(r.Told);
        Assert.False(r.W.IsBusy);
        Assert.Null(r.W.MoveBlocker());
        Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
    }

    [AvaloniaFact]
    public async Task WhatStopsTheAreaMovingAndTheQuestionBeforeApplyingOverManyZones()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        Assert.Null(w.MoveBlocker());
        s.Brush.Radius = 4;
        s.BeginStroke(BrushTool.Raise, 32, 32);
        Assert.Equal("a stroke is on", w.MoveBlocker());
        s.EndStroke();
        w.View.Path.Points.Add(new System.Numerics.Vector2(10, 10));
        Assert.Equal("a path is drawn (apply or clear it)", w.MoveBlocker());
        // No live game here: no players to ask for.
        await w.PollPlayers();
        Assert.Empty(w.LivePlayers);
        // Over more than six zones the question lists six.
        string? asked = null;
        w.Ask = (_, text, _, _) => { asked = text; return Task.FromResult(true); };
        Assert.True(await w.ConfirmOverGame([.. Enumerable.Range(0, 7).Select(i => (i, 0))]));
        Assert.StartsWith("The ground of 7 zone(s) (0, 0; 1, 0; 2, 0; 3, 0; 4, 0; 5, 0…) was changed in the game", asked);
    }

    [AvaloniaFact]
    public async Task ADiscardThatCannotReadTheWorldAgainSaysWhy()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        r.Raise();
        // Another area: the change of the first one has no step here, so only reading the world again drops it.
        await r.W.EditArea(4, 0, 1);
        foreach (string f in Directory.GetFiles(r.Dir, "_main.*"))
        {
            File.Delete(f);
        }
        r.Answers.Enqueue(true);
        r.Answers.Enqueue(true);
        await r.W.DiscardHere();
        Assert.Equal(2, r.Asked.Count);
        Assert.StartsWith("Could not read the world again: ", r.Said);
        Assert.False(r.W.IsBusy);
        Assert.NotNull(r.W.Session);
    }

    // The world read again, but its area does not open again: the map, and the failure in a dialog (not the editor
    // left with no area, or a message that it worked).
    [AvaloniaFact]
    public async Task AnAreaThatDoesNotOpenAgainAfterAReadGoesToTheMapAndSaysWhy()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        var bad = new WorldScene
        {
            World = r.W.World!.World,
            Name = "test",
            ZonesX = -1,
            ZonesZ = -1,
            Heights = [],
            Biomes = [],
            Things = [],
            BiomeColor = [],
            Mask = [],
            OceanDepth = [],
            Limit = [],
        };
        Assert.False(await r.W.ReopenArea(bad));
        Assert.True(r.W.MapShown);
        Assert.Null(r.W.Session);
        Assert.False(r.W.IsBusy);
        Assert.StartsWith("The world was read again, but the area could not be opened again. Could not open that area: ", Assert.Single(r.Told));
    }

    // While the area opens again, another is asked for (an arrow move) and fails too: the last one asked for sends the
    // editor to the map, said once.
    [AvaloniaFact]
    public async Task AnotherAreaFailingDuringAReopenGoesToTheMapToo()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        var bad = new WorldScene
        {
            World = r.W.World!.World,
            Name = "test",
            ZonesX = -1,
            ZonesZ = -1,
            Heights = [],
            Biomes = [],
            Things = [],
            BiomeColor = [],
            Mask = [],
            OceanDepth = [],
            Limit = [],
        };
        var reopen = r.W.ReopenArea(bad);
        var other = r.W.EditBlock(0, 0, -1, -1);
        Assert.False(await reopen);
        Assert.False(await other);
        Assert.True(r.W.MapShown);
        Assert.Null(r.W.Session);
        Assert.False(r.W.IsBusy);
        Assert.StartsWith("Could not open that area: ", Assert.Single(r.Told));
    }

    // The old area still the world's (a live Reload keeps it): it is no longer, so its history is not kept over the
    // world read again.
    [AvaloniaFact]
    public async Task AnAreaThatDoesNotOpenAgainIsNoLongerTheWorldsArea()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        var world = r.W.World!;
        var bad = new WorldScene
        {
            World = world.World,
            Owner = world,
            Session = r.W.Session,
            Name = "test",
            ZonesX = -1,
            ZonesZ = -1,
            Heights = [],
            Biomes = [],
            Things = [],
            BiomeColor = [],
            Mask = [],
            OceanDepth = [],
            Limit = [],
        };
        Assert.Same(bad.Session, world.Area);
        Assert.False(await r.W.ReopenArea(bad));
        Assert.True(r.W.MapShown);
        Assert.Null(r.W.Session);
        Assert.Null(world.Area);
    }
}
