using System.Numerics;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Path tool's Apply in another language (the made-up one: PseudoLang): a path and a cave are said and named in
/// History in the language, their English kept for Claude; and each thing a dug cave can say.
/// </summary>
[Collection("DataDir")]
public sealed class MainWindowPathLangTests
{
    /// <summary>
    /// A window editing a flat 2 × 2 zone area with these objects and a line across its middle.
    /// </summary>
    private static (MainWindow W, EditSession S) Open(params WorldScene.Thing[] things)
    {
        var s = TestScenes.Flat(2, things);
        var w = TestWindows.Editing(s);
        w.View.Path.Points.AddRange([new Vector2(20, 64), new Vector2(108, 64)]);
        return (w, s);
    }

    [AvaloniaFact]
    public void APathIsSaidAndNamedInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        var (w, s) = Open();
        w.View.Path.Act = PathTool.Action.Raise;
        w.ApplyPath();
        string raise = PathTool.Label(PathTool.Action.Raise).English;
        Assert.StartsWith(PseudoLang.Wrap("Applied “" + PseudoLang.Wrap(raise) + "” along ")[..^1], w.MessageText.Text);
        Assert.Matches("^Applied “" + raise + "” along \\d+ m\\. The line is kept", w.LastMessageEn);
        Assert.Equal(PseudoLang.Wrap("Path: " + PseudoLang.Wrap(raise)), s.UndoLabel);
    }
}
