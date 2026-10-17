using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's edits as the user sees them, in another language (the made-up one: PseudoLang): the status bar shows the
/// language while its English is kept for Claude; the history step keeps its "Claude: " mark before the step's words in
/// the language, so it is still found as Claude's; a path's message, which is also Claude's answer, answers Claude in
/// English.
/// </summary>
[Collection("DataDir")]
public sealed class ClaudeToolsLangTests
{
    [AvaloniaFact]
    public async Task ClaudesEditsShowInTheLanguageAndStayClaudes()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var s = r.W.Session!;
        using var pseudo = new PseudoLang();

        string answer = await r.T.ShapeGround("smooth", -20, -20, 20, 20);
        Assert.DoesNotContain("⟦", answer, StringComparison.Ordinal);
        Assert.StartsWith("⟦Claude's " + PseudoLang.Wrap("smooth") + ": ", r.W.MessageText.Text, StringComparison.Ordinal);
        Assert.Matches(@"^Claude's smooth: \d+ ground point\(s\); Ctrl\+Z takes it back\.$", r.W.LastMessageEn);
        Assert.Equal("Claude: " + PseudoLang.Wrap("smooth"), s.UndoLabel);
        Assert.True(HistoryPanel.ByClaude(s.UndoList[^1]));

        answer = await r.T.DigPath("river", [[-20f, 0f], [20f, 0f]], width: 6, depth: 3);
        Assert.DoesNotContain("⟦", answer, StringComparison.Ordinal);
        Assert.StartsWith("⟦", r.W.MessageText.Text, StringComparison.Ordinal);
        Assert.StartsWith(r.W.LastMessageEn + " Now pending: ", answer, StringComparison.Ordinal);
        Assert.Equal("Claude: " + PseudoLang.Wrap("river along a line"), s.UndoLabel);
        Assert.True(HistoryPanel.ByClaude(s.UndoList[^1]));
    }
}
