using System.Numerics;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// An edit session in another language (the made-up one: PseudoLang): the save bar's pending line, a step's short
/// description, the Deleted, Moved and Removed history labels, what taking a step out says (done or refused) and the
/// brush stroke's limit message are catalogue texts; the messages keep their English for Claude.
/// </summary>
[Collection("DataDir")]
public sealed class EditSessionLangTests
{
    private static readonly Func<Formula.Env, double> Two = Formula.Compile("2", []);

    [Fact]
    public void ItsTextsAreTranslatedAndItsMessagesKeepTheirEnglish()
    {
        using var pseudo = new PseudoLang();
        var s = TestScenes.Flat(2, TestScenes.Beech(1, new Vector3(0, 30, 0)), TestScenes.Beech(2, new Vector3(8, 30, 8)));
        Assert.Equal(PseudoLang.Wrap("All saved"), s.PendingText);

        s.Shape(30, 30, Two, 3, 0, "raise");
        s.Delete([0]);
        s.Move([(1, new Vector3(12, 30, 12), Vector3.Zero)]);
        Assert.True(PseudoLang.Translated(s.PendingText, []), s.PendingText);
        Assert.StartsWith(PseudoLang.Open + "Unsaved: ", s.PendingText);
        Assert.Equal([PseudoLang.Wrap("Deleted 1"), PseudoLang.Wrap("Moved 1")], s.UndoList.Skip(1).Select(c => c.Label));
        Assert.Equal(PseudoLang.Wrap("1 zone"), s.UndoList[0].Describe());
        Assert.Equal(PseudoLang.Wrap("1 removed"), s.UndoList[1].Describe());

        var removed = s.RemoveChange(s.UndoList[0]);
        Assert.Equal(PseudoLang.Wrap("Removed “raise”; everything else is kept. Ctrl+Z brings it back."), removed.Shown);
        Assert.Equal("Removed “raise”; everything else is kept. Ctrl+Z brings it back.", removed.English);
        Assert.Equal(PseudoLang.Wrap("Removed: raise"), s.UndoLabel);
        var refused = s.RemoveChange(s.UndoList[0]);
        Assert.Equal(PseudoLang.Wrap("That change cannot be removed."), refused.Shown);
        Assert.Equal("That change cannot be removed.", refused.English);

        // A stroke pushed past the game's ±8 m.
        s.Brush.Strength = 1;
        s.BeginStroke(BrushTool.Raise, 64, 64);
        for (int i = 0; i < 300; i++)
        {
            s.StrokeStep(64, 64, 0.1f);
        }
        var limit = s.EndStroke();
        Assert.StartsWith(PseudoLang.Open.ToString(), limit.Shown);
        Assert.StartsWith("Reached the game limit", limit.English);
    }

    // What the first test does not reach: a step over two zones, a move's new copy, a zone reset (the step and the save
    // bar), a step a later one took from, a step no longer in the history, a stroke the Mask stops, and a live area's
    // save bar. Each a catalogue text, the messages with their English kept.
    [Fact]
    public async Task TheOtherTextsAndRefusalsAreTranslatedToo()
    {
        using var pseudo = new PseudoLang();
        var s = TestScenes.Flat(2, TestScenes.Beech(1, new Vector3(0, 30, 0)));
        s.Shape(64, 30, Two, 3, 0, "raise");
        Assert.Equal(PseudoLang.Wrap("2 zones"), s.UndoList[^1].Describe());
        int copy = Assert.Single(s.Move([(0, new Vector3(4, 30, 4), Vector3.Zero)]));
        Assert.Equal(PseudoLang.Wrap("1 new") + " · " + PseudoLang.Wrap("1 removed"), s.UndoList[^1].Describe());
        s.MarkResets([(1, 1)], true, false, "reset");
        Assert.Equal(PseudoLang.Wrap("1 zone reset(s)"), s.UndoList[^1].Describe());
        Assert.True(PseudoLang.Translated(s.PendingText, []), s.PendingText);
        Assert.Contains(PseudoLang.Wrap("2 zones"), s.PendingText);
        Assert.Contains(PseudoLang.Wrap("1 reset"), s.PendingText);

        // The move's copy moved again: the first move cannot be taken out.
        s.Move([(copy, new Vector3(8, 30, 8), Vector3.Zero)]);
        var taken = s.RemoveChange(s.UndoList[1]);
        Assert.Equal(PseudoLang.Wrap("That change cannot be removed: a later step moved or deleted what it added. Undo back to it instead."), taken.Shown);
        Assert.Equal("That change cannot be removed: a later step moved or deleted what it added. Undo back to it instead.", taken.English);
        var last = s.UndoList[^1];
        Assert.True(s.Undo());
        var gone = s.RemoveChange(last);
        Assert.Equal(PseudoLang.Wrap("That change is no longer in the history to undo: nothing was removed."), gone.Shown);
        Assert.Equal("That change is no longer in the history to undo: nothing was removed.", gone.English);

        s.Mask.On = true;
        s.Mask.HeightMin = 100;
        s.BeginStroke(BrushTool.Raise, 64, 64);
        s.StrokeStep(64, 64, 0.1f);
        var masked = s.EndStroke();
        Assert.StartsWith(PseudoLang.Open + "Nothing changed: the Mask", masked.Shown);
        Assert.StartsWith("Nothing changed: the Mask", masked.English);

        using var game = new FakeGame();
        var (_, live) = await TestScenes.LiveArea(game);
        Assert.Equal(PseudoLang.Wrap("All applied"), live.PendingText);
        live.Shape(10, 10, Two, 3, 0, "raise");
        Assert.StartsWith(PseudoLang.Open + "Not applied: ", live.PendingText);
        Assert.True(PseudoLang.Translated(live.PendingText, []), live.PendingText);
    }
}
