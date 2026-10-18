using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Discard and Reload buttons of the 3D editor: undoing the steps not saved or applied,
/// reading the world again for older ones, and what a live game does on Reload.
/// </summary>
[Collection("World files")]
public class DiscardAndReloadTests
{
    private static readonly Func<Formula.Env, double> Two = Formula.Compile("2", []);

    /// <summary>
    /// Raises the button's click (no posted work run).
    /// </summary>
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public async Task DiscardWithNothingWaitingSaysSoWithoutAsking()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        Assert.False(r.W.DiscardButton.IsEnabled);
        await r.W.DiscardHere();
        Assert.Empty(r.Asked);
        Assert.Equal("There is nothing to discard.", r.W.MessageText.Text);
    }

    [AvaloniaFact]
    public async Task DiscardUndoesTheUnsavedStepsHereAndKeepsTheView()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        r.Raise(20, 20, "one");
        r.Raise(40, 40, "two");
        await TestWindows.Until(() => r.W.DiscardButton.IsEnabled);
        var cam = r.W.View.WorldCamera;
        // Asked no: nothing changes.
        r.Answers.Enqueue(false);
        await r.W.DiscardHere();
        Assert.Equal("Discard the unsaved changes (1 zone(s) of ground)? The last 2 step(s) in History are undone.", Assert.Single(r.Asked));
        Assert.Equal(2, r.W.Session!.UndoList.Count);
        // Yes: both undone, nothing to redo, the area and the view stay.
        r.W.Session.Undo();
        r.W.Session.Redo();
        r.Answers.Enqueue(true);
        await r.W.DiscardHere();
        Assert.Empty(r.W.Session.UndoList);
        Assert.False(r.W.Session.CanRedo);
        Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
        Assert.Equal("Discarded. Everything else is kept.", r.W.MessageText.Text);
        Assert.False(r.W.DiscardButton.IsEnabled);
        Assert.Equal(cam, r.W.View.WorldCamera);
        Assert.Equal(2, r.Asked.Count);
    }

    [AvaloniaFact]
    public async Task ChangesWithNoStepHereNeedTheWorldReadAgain()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        r.Raise();
        // Another area: the change of the first one has no step here.
        await r.W.EditArea(4, 0, 1);
        Assert.Equal(0, r.W.Session!.UnappliedSteps);
        Assert.Equal(1, r.W.World!.Pending.Zones);
        // The first question (no steps to undo), then the second answered no: kept.
        r.Answers.Enqueue(true);
        r.Answers.Enqueue(false);
        await r.W.DiscardHere();
        Assert.Equal("Discard the unsaved changes (1 zone(s) of ground)?", r.Asked[0]);
        Assert.Contains("can only be discarded by reading the world again", r.Asked[1]);
        Assert.Equal(1, r.W.World.Pending.Zones);
        // Yes: the world is read again, the same area opened, the view kept.
        var cam = r.W.View.WorldCamera;
        r.Answers.Enqueue(true);
        r.Answers.Enqueue(true);
        await r.W.DiscardHere();
        Assert.Equal((0, 0, 0, 0), r.W.World.Pending);
        Assert.Equal(4, r.W.View.Scene!.X0);
        Assert.Equal("Discarded: the world was read again.", r.W.MessageText.Text);
        Assert.Equal(cam!.Value.World, r.W.View.WorldCamera!.Value.World);
    }

    [AvaloniaFact]
    public async Task LiveDiscardKeepsTheStepsAlreadyApplied()
    {
        using var r = new WorldWindowRun();
        await r.OpenLive();
        r.Raise(20, 20, "applied");
        await r.W.ApplyNow();
        r.Raise(40, 40, "not yet");
        Assert.Equal(1, r.W.Session!.UnappliedSteps);
        r.Answers.Enqueue(true);
        await r.W.DiscardHere();
        Assert.StartsWith("Discard the changes not applied yet (1 zone(s) of ground)? The last 1 step(s)", r.Asked[0]);
        Assert.Equal("applied", Assert.Single(r.W.Session.UndoList).Label);
        Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
    }

    [AvaloniaFact]
    public async Task DiscardButtonFollowsWhatWaits()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        r.Raise();
        await TestWindows.Until(() => r.W.DiscardButton.IsEnabled);
        r.Answers.Enqueue(true);
        Click(r.W.DiscardButton);
        await TestWindows.Until(() => !r.W.DiscardButton.IsEnabled);
        Assert.Single(r.Asked);
    }

    [AvaloniaFact]
    public async Task ReloadIsForLiveWorldsOnly()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        Assert.False(r.W.ReloadButton.IsVisible);
        Assert.False(r.W.AutoApplyBox.IsVisible);
        r.Raise();
        await r.W.ReloadHere();
        Assert.Empty(r.Asked);
        Assert.Equal(1, r.W.World!.Pending.Zones);
    }

    [AvaloniaFact]
    public async Task ReloadAsksWhenSomethingWaitsThenReadsTheGameAgain()
    {
        using var r = new WorldWindowRun();
        await r.OpenLive(1, 0, 1);
        Assert.True(r.W.ReloadButton.IsVisible);
        int snapshots = r.Game!.Snapshots;
        // Nothing waits: no question.
        await r.W.ReloadHere();
        Assert.Empty(r.Asked);
        Assert.Equal(snapshots + 1, r.Game.Snapshots);
        Assert.Equal("The world was loaded again from the game.", r.W.MessageText.Text);
        Assert.Equal(1, r.W.View.Scene!.X0);
        // Something waits: asked; no keeps it.
        r.Raise();
        r.Answers.Enqueue(false);
        await r.W.ReloadHere();
        Assert.Single(r.Asked);
        Assert.Equal(1, r.W.World!.Pending.Zones);
        Assert.Equal(snapshots + 1, r.Game.Snapshots);
        r.Answers.Enqueue(true);
        Click(r.W.ReloadButton);
        await TestWindows.Until(() => r.W.World!.Pending == (0, 0, 0, 0) && r.W.Session != null && r.W.Session.UndoList.Count == 0);
        Assert.Equal(snapshots + 2, r.Game.Snapshots);
    }

    [AvaloniaFact]
    public async Task AGameThatDoesNotAnswerTheReloadIsReported()
    {
        using var r = new WorldWindowRun();
        await r.OpenLive();
        r.Game!.Fail["/snapshot"] = 500;
        await r.W.ReloadHere();
        Assert.StartsWith("Could not reach the game", r.W.MessageText.Text);
        Assert.NotNull(r.W.Session);
    }

    [Fact]
    public void OnlyTheStepsNotAppliedAreUndone()
    {
        var s = TestScenes.Flat(2);
        s.Shape(20, 20, Two, 3, 0, "a");
        s.Shape(40, 40, Two, 3, 0, "b");
        s.UndoList[0].Applied = true;
        Assert.Equal(1, s.UnappliedSteps);
        s.Shape(60, 60, Two, 3, 0, "c");
        s.Undo();
        Assert.True(s.CanRedo);
        Assert.Equal(1, s.UndoUnapplied());
        Assert.Equal("a", Assert.Single(s.UndoList).Label);
        Assert.False(s.CanRedo);
        Assert.Equal(0, s.UnappliedSteps);
        Assert.Equal(0, s.UndoUnapplied());
    }

    [Fact]
    public void NothingIsUndoneDuringAStroke()
    {
        var s = TestScenes.Flat(2);
        s.Shape(20, 20, Two, 3, 0, "a");
        s.Undo();
        s.Redo();
        s.Undo();
        s.BeginStroke(BrushTool.Raise, 40, 40);
        Assert.Equal(0, s.UndoUnapplied());
        // The redo list stays too.
        Assert.True(s.CanRedo);
        s.EndStroke();
    }

    [Fact]
    public void EditsAreToldApartFromSaves()
    {
        var s = TestScenes.Flat(2, new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(0, 30, 0), Vector3.Zero, 1, false));
        int edits = 0, changes = 0;
        s.EditMade += () => edits++;
        s.Changed += () => changes++;
        s.Shape(20, 20, Two, 3, 0, "a");
        s.Undo();
        s.Redo();
        s.Delete([0]);
        s.RemoveChange(s.UndoList[0]);
        s.BeginStroke(BrushTool.Raise, 40, 40);
        s.StrokeStep(40, 40, 0.1f);
        s.EndStroke();
        Assert.Equal(6, edits);
        int before = changes;
        s.UndoUnapplied();
        Assert.True(changes > before);
    }
}
