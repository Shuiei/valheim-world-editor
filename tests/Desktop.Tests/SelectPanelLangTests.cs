using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool in another language (the made-up one: PseudoLang): its panel shows only catalogue texts with nothing
/// selected, two objects selected, Move by on and a selection kept; the questions it asks (a selection's name,
/// forgetting it, a builder's id) too; and a tool's message is shown in that language while Claude reads its English.
/// </summary>
[Collection("DataDir")]
public sealed class SelectPanelLangTests : IDisposable
{
    private readonly string? _old = SavedSelections.PathOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-select-lang-" + Guid.NewGuid().ToString("N")[..8]);

    public SelectPanelLangTests()
    {
        Directory.CreateDirectory(_dir);
        SavedSelections.PathOverride = Path.Combine(_dir, "selections.json");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        SavedSelections.PathOverride = _old;
        Directory.Delete(_dir, true);
    }

    private static readonly int Beech = StableHash.Of("Beech1");

    /// <summary>A shown window, made in the pseudo language, editing a flat area with two trees, in the Select tool.</summary>
    private static MainWindow Window()
    {
        var w = new MainWindow(load: false) { Width = 1400, Height = 900 };
        w.Show();
        var s = TestScenes.Flat(2,
            new WorldScene.Thing(1, Beech, new Vector3(40, 30, 40), Vector3.Zero, 1, false),
            new WorldScene.Thing(2, Beech, new Vector3(44, 30, 40), Vector3.Zero, 1, false));
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.Tools.ChooseSelect();
        w.SelectPanel.WorldName = () => "World one";
        return w;
    }

    /// <summary>The kinds Replace with lists: game data, shown as the game names them.</summary>
    private static string[] Kinds(SelectPanel p) => [.. p.ReplaceBox.Items.OfType<string>()];

    [AvaloniaFact]
    public async Task SelectPanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        var w = Window();
        var p = w.SelectPanel;
        p.FillSaved();
        Assert.Empty(PseudoLang.Untranslated(p.Card, Kinds(p)));

        w.View.Select([0, 1]);
        p.Refresh();
        Assert.Empty(PseudoLang.Untranslated(p.Card, Kinds(p)));
        p.ByButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Empty(PseudoLang.Untranslated(p.Card, Kinds(p)));

        p.AskName = Task.FromResult<string?>;
        await p.Keep();
        Assert.Equal(1, p.SavedBox.ItemCount);
        Assert.Empty(PseudoLang.Untranslated(p.Card, Kinds(p)));
    }

    [AvaloniaFact]
    public async Task SelectQuestionsAreAskedInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        var w = Window();
        var p = w.SelectPanel;
        w.View.Select([0]);

        // Keeping a selection asks for its name (kept under the name offered).
        var keeping = p.Keep();
        var d = TestWindows.Shown(w);
        Assert.Empty(PseudoLang.Untranslated(d));
        DialogsTests.Press(d, Lang.T("common.ok"));
        await keeping;
        Assert.Equal(1, p.SavedBox.ItemCount);

        // Forgetting it asks first.
        var forgetting = p.Forget();
        d = TestWindows.Shown(w);
        Assert.Empty(PseudoLang.Untranslated(d));
        DialogsTests.Press(d, Lang.T("common.cancel"));
        await forgetting;
        Assert.Equal(1, p.SavedBox.ItemCount);

        // Built by's other player asks for the id.
        var asking = w.AskPlayerId();
        d = TestWindows.Shown(w);
        Assert.Empty(PseudoLang.Untranslated(d));
        DialogsTests.Press(d, Lang.T("common.cancel"));
        Assert.Null(await asking);
    }

    [AvaloniaFact]
    public void ASelectMessageIsShownInTheLanguageAndClaudeReadsEnglish()
    {
        var w = Window();
        using var pseudo = new PseudoLang();
        w.View.Select([0]);

        w.SelectPanel.InvertButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.StartsWith(PseudoLang.Open.ToString(), w.MessageText.Text, StringComparison.Ordinal);
        Assert.Equal("Selected the 1 shown object(s) that were not selected.", w.LastMessageEn);
    }
}
