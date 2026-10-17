using System.Numerics;
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
}
