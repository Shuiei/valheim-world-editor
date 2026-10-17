using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Blueprints panel in another language (the made-up one: PseudoLang): its list empty, filled with a Homestead
/// blueprint and an older one, searched for nothing, and with Homestead not installed shows only catalogue texts; a
/// message is shown in the language while Claude reads it in English; the questions it asks are in the language; and a
/// building that cannot be saved is told to Claude (save_blueprint) in English.
/// </summary>
[Collection("DataDir")]
public sealed class BlueprintsPanelLangTests
{
    /// <summary>
    /// The names the tests' blueprints and Homestead's place carry: data, the same in every language.
    /// </summary>
    private static readonly string[] Data = ["Gate", "Old", "OtherWorld", "test profile", "test", ".vbuild"];

    [AvaloniaFact]
    public void BlueprintsPanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        using var r = new BlueprintsPanelRun();
        r.B.Toggle(true);
        Assert.Empty(PseudoLang.Untranslated(r.B.Card, Data));

        r.Keep("Gate", "woodwall", "wood_floor");
        r.B.Store.Save("Old", "OtherWorld", null, CopyFormat.ToJson(BlueprintsPanelRun.Clip("Old", "woodwall")));
        r.B.Refresh();
        Assert.Equal(2, r.B.List.GetLogicalDescendants().OfType<Button>().Count(b => Tips.KeyOf(b) == "blueprints.paste"));
        Assert.Empty(PseudoLang.Untranslated(r.B.Card, Data));

        r.B.Search.Text = "castle";
        Assert.Empty(PseudoLang.Untranslated(r.B.Card, Data));
        r.B.Search.Text = "";

        r.Installed = false;
        r.B.Refresh();
        Assert.True(r.B.GetHomesteadButton.IsVisible);
        Assert.Empty(PseudoLang.Untranslated(r.B.Card, Data));
    }

    [AvaloniaFact]
    public void AMessageIsShownInTheLanguageAndClaudeReadsItInEnglish()
    {
        using var pseudo = new PseudoLang();
        using var r = new BlueprintsPanelRun();
        r.Keep("Gate", "woodwall");
        r.B.Toggle(true);
        var vbuild = r.B.List.GetLogicalDescendants().OfType<Button>().Single(b => Tips.KeyOf(b) == "blueprints.vbuild");
        vbuild.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith(PseudoLang.Open + "Written to ", r.W.MessageText.Text);
        Assert.StartsWith("Written to ", r.W.LastMessageEn);
        Assert.EndsWith(".vbuild. Only the objects are written, not the ground.", r.W.LastMessageEn);
        Assert.DoesNotContain("⟦", r.W.LastMessageEn);
    }

    [AvaloniaFact]
    public async Task TheQuestionsAskedAreInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        var asked = new List<string>();
        using (var r = new BlueprintsPanelRun())
        {
            r.B.Confirm = text => { asked.Add(text); return Task.FromResult(false); };
            r.Keep("Gate", "woodwall");
            // Saving over a blueprint of that name asks first.
            r.W.View.Paste.Clip = BlueprintsPanelRun.Clip("Gate", "woodwall");
            r.B.AskDetails = (d, _) => Task.FromResult<Homestead.Details?>(d with { Name = "Gate" });
            Assert.Null(await r.B.Save());
            // So does deleting one.
            await r.B.Delete("hs:" + Homestead.FileName("Gate"), "Gate");
            Assert.Single(r.Listed);
        }
        Assert.Equal(2, asked.Count);
        Assert.All(asked, text => Assert.True(PseudoLang.Translated(text, Data), text));

        // The window asks them in its dialog: its title and buttons are in the language too.
        var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
        w.Show();
        var answer = w.Blueprints.Confirm(asked[1]);
        var d = TestWindows.Shown(w);
        Assert.Empty(PseudoLang.Untranslated(d, Data));
        DialogsTests.Press(d, Lang.T("common.cancel"));
        Assert.False(await answer);
    }

    [AvaloniaFact]
    public void ABuildingThatCannotBeSavedIsToldToClaudeInEnglish()
    {
        using var pseudo = new PseudoLang();
        using var r = new BlueprintsPanelRun();
        Directory.CreateDirectory(r.Dir);
        File.WriteAllText(r.Homestead, "not a folder");
        var clip = CopyFormat.ToJson(BlueprintsPanelRun.Clip("Tower", "woodwall"));
        string? problem = r.B.SaveBuildingAs(clip, new Homestead.Details("Tower", "", []));
        Assert.StartsWith("Could not save the blueprint: ", problem);
        Assert.DoesNotContain("⟦", problem!);
        // The user is told in the language.
        Assert.StartsWith(PseudoLang.Open + "Could not save the blueprint: ", r.Said);
    }
}
