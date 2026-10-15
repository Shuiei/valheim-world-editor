using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The History panel's remaining branches: steps from an earlier session (with their day), a history carried to
/// another area with a removal in it, and taking Claude's changes back when refused, with nobody listening and with
/// none left.
/// </summary>
public class HistoryPanelBranchesTests
{
    /// <summary>The panel's words.</summary>
    private static List<string> Texts(Control c) => [.. c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "")];

    /// <summary>
    /// An area whose history came from another: Claude's hill from an earlier session three days ago, a raise, and the
    /// raise's removal.
    /// </summary>
    private static EditSession Carried()
    {
        var s = TestScenes.Flat(1);
        s.Shape(32, 32, _ => 1, 3, 0, "Claude: hill");
        s.Shape(20, 20, _ => 1, 3, 0, "Raise");
        s.RemoveChange(s.UndoList[1]);
        var k = s.Export();
        var old = k.Undo[0];
        var earlier = old with { Earlier = true, Time = DateTime.Now.AddDays(-3) };
        var ids = new Dictionary<EditSession.Change, int[]>(k.Ids);
        ids.Remove(old);
        ids[earlier] = k.Ids[old];
        var t = TestScenes.Flat(1);
        Assert.Equal(0, t.Import(k with { Undo = [earlier, k.Undo[1], k.Undo[2]], Ids = ids }, _ => []));
        return t;
    }

    [Fact]
    public void ARemovalCarriedToAnotherAreaStillPointsAtWhatItRemoved()
    {
        var t = Carried();
        Assert.Equal(3, t.UndoList.Count);
        Assert.True(t.UndoList[0].Earlier);
        Assert.Same(t.UndoList[1], t.UndoList[2].RevertOf);
    }

    [AvaloniaFact]
    public void AStepFromAnEarlierSessionSaysSoAndItsDay()
    {
        var t = Carried();
        var p = new HistoryPanel(() => t);
        p.Card.IsVisible = true;
        p.Refresh();
        var words = Texts(p.Card);
        Assert.Contains("earlier session", words);
        Assert.Contains(words, w => w.StartsWith(t.UndoList[0].Time.ToString("g", System.Globalization.CultureInfo.CurrentCulture), StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task ClaudesChangesGoBackOnlyWhenAllowed()
    {
        await new HistoryPanel(() => null).TakeBackClaudes();
        var t = Carried();
        var p = new HistoryPanel(() => t) { Allow = _ => Task.FromResult(false) };
        await p.TakeBackClaudes();
        Assert.False(t.UndoList[0].Removed);
        // Allowed, with nobody listening for the message.
        p.Allow = _ => Task.FromResult(true);
        await p.TakeBackClaudes();
        Assert.True(t.UndoList[0].Removed);
        int steps = t.UndoList.Count;
        // None left: nothing more.
        await p.TakeBackClaudes();
        Assert.Equal(steps, t.UndoList.Count);
    }

    // During a brush stroke nothing can be taken out of the history: the message says so instead of counting the
    // steps as built on.
    [AvaloniaFact]
    public async Task NothingIsTakenBackDuringAStroke()
    {
        var s = TestScenes.Flat(1);
        s.Shape(32, 32, _ => 1, 3, 0, "Claude: raised");
        s.BeginStroke(BrushTool.Raise, 40, 40);
        string? said = null;
        var p = new HistoryPanel(() => s) { Allow = _ => Task.FromResult(true) };
        p.Message += m => said = m;
        await p.TakeBackClaudes();
        Assert.False(s.UndoList[0].Removed);
        Assert.Equal("Nothing taken back: finish the brush stroke first.", said);
    }

    // A change of Claude's that a later step of the user's built on (its copy moved again) stays, and the message
    // says so.
    [AvaloniaFact]
    public async Task AChangeOfClaudesTheUserMovedOnIsNotTakenBack()
    {
        var s = TestScenes.Flat(1, new WorldScene.Thing(3, StableHash.Of("Rock_4"), new Vector3(0, 30, 0), Vector3.Zero, 1, false));
        int copy = s.Move([(0, new Vector3(4, 30, 0), Vector3.Zero)], "Claude: moved")[0];
        s.Move([(copy, new Vector3(8, 30, 0), Vector3.Zero)]);
        s.Shape(32, 32, _ => 1, 3, 0, "Claude: raised");
        string? said = null;
        var p = new HistoryPanel(() => s) { Allow = _ => Task.FromResult(true) };
        p.Message += m => said = m;
        await p.TakeBackClaudes();
        Assert.False(s.UndoList[0].Removed);
        Assert.True(s.UndoList[2].Removed);
        Assert.Equal(1, s.Scene.Things.Count(t => !t.Gone));
        Assert.Equal("Took back 1 change(s) Claude made; yours are kept. 1 could not be taken back: a later step moved or deleted what they added (undo instead).", said);
    }
}
