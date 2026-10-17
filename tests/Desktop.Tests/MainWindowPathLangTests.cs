using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Save;
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
    /// A shown window, in the pseudo language already, editing a flat 2 × 2 zone area (ground at 30 m) with these
    /// objects, and a line across its middle.
    /// </summary>
    private static (MainWindow W, EditSession S) Open(params WorldScene.Thing[] things)
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2, things);
        w.View.Show(s.Scene, null);
        w.Edit(s);
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

    [AvaloniaFact]
    public void ACaveIsSaidAndNamedInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        var (w, s) = Open(new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(32, 30, 32), Vector3.Zero, 1, false));
        var path = w.View.Path;
        path.Act = PathTool.Action.Cave;
        path.Width = 8;
        path.Soft = 2.5f;
        path.CaveDepth = 7.5f;
        path.Headroom = 4.5f;
        w.ApplyPath();
        Assert.StartsWith(PseudoLang.Open + "Dug a cave ", w.MessageText.Text);
        Assert.Matches(@"^Dug a cave \d+ m long, roofed with \d+ boulder\(s\); 1 tree\(s\) and rock\(s\) in the way taken away\.", w.LastMessageEn);
        Assert.Matches(@"^⟦Cave along \d+ m⟧$", s.UndoLabel);
    }

    // Each thing a dug cave can say, shown in the language and English for Claude: too shallow for a roof, roofed with
    // floor open to the sky, dug (with trees taken away or not), walls that could not rise, the game's limit reached.
    [AvaloniaFact]
    public void EachCaveOutcomeIsSaidInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        static void Is(Said said, params string[] english)
        {
            Assert.Equal(string.Join(" ", english.Select(PseudoLang.Wrap)), said.Shown);
            Assert.Equal(string.Join(" ", english), said.English);
        }
        Is(MainWindow.CaveOutcome(0, 3, 1, 40, 2, true),
            "Dug the cave's trench, but it is nowhere deep enough for a roof: make Depth larger than Headroom by 1.5 m, or the line longer (its ends slope up to the ground).");
        Is(MainWindow.CaveOutcome(5, 3, 0, 40, 0, false),
            "Dug a cave roofed with 5 boulder(s), but 3 point(s) of its floor stay open to the sky (at the area's edge?).");
        Is(MainWindow.CaveOutcome(5, 0, 0, 40, 0, false), "Dug a cave 40 m long, roofed with 5 boulder(s).", "Ctrl+Z takes it back.");
        Is(MainWindow.CaveOutcome(5, 0, 2, 40, 4, true),
            "Dug a cave 40 m long, roofed with 5 boulder(s); 2 tree(s) and rock(s) in the way taken away.",
            "At 4 point(s) the walls could not rise to the rock (the game's ±8 m: No limit in the brush options lets them).",
            "Part of it reached the game's ±8 m limit (switch on No limit in the brush options to dig deeper).");
    }
}
