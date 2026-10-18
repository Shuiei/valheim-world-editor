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
/// forgetting it, a builder's id) too; a tool's message is shown in that language while Claude reads its English; and
/// so are the window's Built by choices and messages, Make player built, a runestone's Inspect and Replace's, and the
/// name the Replace pick button gives the eyedropper.
/// </summary>
[Collection("DataDir")]
public sealed class SelectPanelLangTests : SavedSelectionsFolder
{
    private static readonly int Beech = StableHash.Of("Beech1");

    /// <summary>A shown window, made in the pseudo language, editing a flat area with two trees, in the Select tool.</summary>
    private static MainWindow Window()
    {
        var w = TestWindows.Editing(TestScenes.Flat(
            2,
            TestScenes.Beech(1, new Vector3(40, 30, 40)),
            TestScenes.Beech(2, new Vector3(44, 30, 40))));
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

        w.SelectPanel.DeleteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.StartsWith(PseudoLang.Open.ToString(), w.MessageText.Text, StringComparison.Ordinal);
        Assert.Equal("Deleted 1 object(s). Ctrl+Z brings them back; they leave the world when it is saved.", w.LastMessageEn);
    }

    [AvaloniaFact]
    public void ThePickButtonNamesReplaceInTheLanguage()
    {
        var w = Window();
        using var pseudo = new PseudoLang();
        string? asked = null;
        w.SelectPanel.PickKindAsked += (label, _) => asked = label;

        w.SelectPanel.ReplacePickButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(PseudoLang.Wrap("Replace"), asked);
    }

    [AvaloniaFact]
    public async Task TheWindowsSelectMessagesAreSaidInTheLanguageAndClaudeReadsEnglish()
    {
        long builder = WorldSave.Builder;
        using var pseudo = new PseudoLang();
        try
        {
            var s = TestScenes.Flat(
                2,
                TestScenes.Beech(1, new Vector3(40, 30, 40)),
                new WorldScene.Thing(2, StableHash.Of("Runestone_Meadows"), new Vector3(30, 30, 30), Vector3.Zero, 1, false));
            var w = TestWindows.Editing(s);
            w.Tools.ChooseSelect();
            void Check(string english)
            {
                Assert.Empty(PseudoLang.Untranslated(w.MessageText));
                Assert.Equal(english, w.LastMessageEn);
            }
            string[] characters = [.. Characters.Local().Select(c => c.Name)];
            Assert.Empty(PseudoLang.Untranslated(w.BuilderBox, characters));

            // Built by: a bad id, another player's id (now a translated choice), nobody.
            w.AskPlayerId = () => Task.FromResult<string?>("abc");
            w.BuilderBox.SelectedIndex = w.BuilderIds.IndexOf(-1);
            await TestWindows.Until(() => w.LastMessageEn.StartsWith("A player id", StringComparison.Ordinal));
            Check("A player id is a whole number other than 0.");
            w.AskPlayerId = () => Task.FromResult<string?>("12345");
            w.BuilderBox.SelectedIndex = w.BuilderIds.IndexOf(-1);
            await TestWindows.Until(() => WorldSave.Builder == 12345);
            Check("New pieces are built by Player 12345.");
            Assert.Empty(PseudoLang.Untranslated(w.BuilderBox, characters));
            Assert.Contains(PseudoLang.Wrap("Player {0}").Replace("{0}", "12345", StringComparison.Ordinal), w.BuilderBox.Items.OfType<string>());

            // Make player built: a tree is nothing players build.
            w.View.Select([0]);
            w.Claim();
            Check("Nothing to change: the selected pieces already have a builder, or are not things players build.");
            w.BuilderBox.SelectedIndex = w.BuilderIds.IndexOf(0);
            await TestWindows.Until(() => WorldSave.Builder == 0);
            Check("New pieces get no builder: the game takes them for parts of a ruin.");
            w.Claim();
            Check("Built by is set to Nobody: choose a player first (View panel, Building).");

            // A runestone has nothing to inspect; Replace with nothing selected.
            w.View.Select([1]);
            w.Inspect();
            Check("A runestone holds no data of its own: the game builds it from its location. It can only be deleted.");
            w.View.Select([]);
            var p = w.SelectPanel;
            p.ReplaceKinds = [Beech];
            p.ReplaceBox.ItemsSource = new[] { "Beech1" };
            p.ReplaceBox.SelectedIndex = 0;
            p.ReplaceButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("Select objects to replace first.");
            Assert.Equal(1, s.Scene.Things.Count(t => t.Prefab == Beech && !t.Gone));
        }
        finally
        {
            WorldSave.Builder = builder;
        }
    }
}
