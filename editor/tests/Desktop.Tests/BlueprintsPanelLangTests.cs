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
/// message is shown in the language while Claude reads it in English; so is each message of importing, pasting,
/// exporting and making a Homestead blueprint, and the import picker; the questions it asks are in the language; and a
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

    /// <summary>
    /// The panel's last message: shown in the language (every sentence, values inside), read by Claude in English.
    /// </summary>
    /// <param name="r">The run.</param>
    /// <param name="english">How the English starts.</param>
    private static void SaidInTheLanguage(BlueprintsPanelRun r, string english)
    {
        Assert.True(PseudoLang.Translated(r.Said!, []), r.Said);
        Assert.StartsWith(english, r.SaidEn);
        Assert.DoesNotContain(PseudoLang.Open, r.SaidEn!);
        r.Said = r.SaidEn = null;
    }

    [AvaloniaFact]
    public void ImportPasteExportAndHomesteadMessagesAreInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        using var r = new BlueprintsPanelRun { Installed = false };
        Directory.CreateDirectory(r.Dir);
        // Imported, with all it says it left out, and the reminder that Homestead is not installed.
        string file = Path.Combine(r.Dir, "Hall.blueprint");
        File.WriteAllText(file, "#Terrain\nsquare;0;0;0;2;0;0;dirt\n#Pieces\nwoodwall;Misc;0;0;0;0;0;0;1;\"\";1;1;1\nbroken\n"
            + "NoSuch_0;Misc;0;0;0;0;0;0;1;\"\";1;1;1\n");
        Assert.NotNull(r.B.Import(file));
        Assert.Contains("Its terrain marks are not kept", r.SaidEn);
        Assert.Contains("1 kind(s) the game does not know were left out (mods?): NoSuch_0.", r.SaidEn);
        Assert.Contains("1 unreadable line(s) skipped.", r.SaidEn);
        Assert.Contains(PseudoLang.Wrap(Lang.En("blueprints.msg.notInstalledYet")), r.Said);
        SaidInTheLanguage(r, "Imported “Hall” as a Homestead blueprint: ");
        Assert.Null(r.B.Import(Path.Combine(r.Dir, "missing.vbuild")));
        SaidInTheLanguage(r, "Could not import that file: ");
        string empty = Path.Combine(r.Dir, "empty.vbuild");
        File.WriteAllText(empty, "nothing useful here");
        Assert.Null(r.B.Import(empty));
        SaidInTheLanguage(r, "Could not import that file: no pieces found in it");
        string wide = Path.Combine(r.Dir, "wide.blueprint");
        File.WriteAllText(wide, "#Terrain\ncircle;0;0;0;1;0;0;dirt\ncircle;2000;0;0;1;0;0;dirt");
        Assert.Null(r.B.Import(wide));
        SaidInTheLanguage(r, "Could not import that file: its ground marks span ");

        // Pasted; exported, or not when it cannot be read; an older one made a Homestead blueprint.
        string odd = r.B.Store.Save("Odd", null, null, CopyFormat.ToJson(BlueprintsPanelRun.Clip("Odd", "woodwall", "NoSuch_a")));
        r.B.Paste(odd);
        SaidInTheLanguage(r, "“Odd” is on the clipboard");
        Assert.NotNull(r.B.Export(odd, "blueprint"));
        SaidInTheLanguage(r, "Written to ");
        Assert.Null(r.B.Export("no-such-blueprint", "vbuild"));
        SaidInTheLanguage(r, "Could not export that blueprint.");
        Assert.NotNull(r.B.ToHomestead(odd));
        SaidInTheLanguage(r, "“Odd” is now a Homestead blueprint");

        // A folder that cannot be written.
        Directory.Delete(r.Homestead, recursive: true);
        File.WriteAllText(r.Homestead, "not a folder");
        Assert.Null(r.B.ToHomestead(odd));
        SaidInTheLanguage(r, "Could not write in the blueprints folder: ");
    }

    [AvaloniaFact]
    public void TheImportPickerIsInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        var (title, type) = MainWindow.ImportPicker();
        Assert.True(PseudoLang.Translated(title, []), title);
        Assert.True(PseudoLang.Translated(type.Name, []), type.Name);
        Assert.Equal(["*.blueprint", "*.vbuild"], type.Patterns);
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
