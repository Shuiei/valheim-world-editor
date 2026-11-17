using System.Text.Json;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The app's texts by language (Lang): the English catalogue carried inside, a text with values put in, a language
/// taken from the user's lang folder over English (its broken texts left out), Claude's texts kept in English, a shown
/// text's English found, and the computer's language picked. It changes the language in use, so it runs alone.
/// </summary>
[Collection("DataDir")]
public sealed class LangTests : LangTestBase
{
    [Fact]
    public void EnglishIsCarriedAndAnUnknownKeyIsAMistake()
    {
        Assert.Equal("English", Lang.English[Lang.NameKey]);
        Assert.Equal("Cancel", Lang.T("common.cancel"));
        Assert.Equal("World folder not found: /x", Lang.T("settings.worldNotFound", "/x"));
        Assert.Throws<KeyNotFoundException>(() => Lang.T("no.such.text"));
    }

    [Fact]
    public void AUsersLanguageIsTakenWithEnglishWhereItHasNoText()
    {
        Write("fr-CA", new()
        {
            [Lang.NameKey] = "Français",
            ["common.cancel"] = "Annuler",
            ["settings.worldNotFound"] = "Dossier de mondes introuvable : {0}",
            // Other places for values than the English: left out, English shown.
            ["settings.bepInExNotFound"] = "Dossier BepInEx introuvable",
            // No English text has that key: left out.
            ["no.such.text"] = "Rien",
        });
        Assert.Equal("fr-CA", Lang.Use("fr-CA"));
        Assert.Equal("fr-CA", Lang.Current);
        Assert.Equal("Annuler", Lang.T("common.cancel"));
        Assert.Equal("Dossier de mondes introuvable : /x", Lang.T("settings.worldNotFound", "/x"));
        Assert.Equal("BepInEx folder not found: /x", Lang.T("settings.bepInExNotFound", "/x"));
        Assert.Equal("Copy", Lang.T("common.copy"));
        Assert.Throws<KeyNotFoundException>(() => Lang.T("no.such.text"));
        Assert.Contains(new Lang.Language("fr-CA", "Français"), Lang.Available());
        Assert.Equal(Lang.Source, Lang.Available()[0].Code);
    }

    [Fact]
    public void WhatClaudeIsToldStaysInEnglish()
    {
        Write("fr-CA", new() { [Lang.NameKey] = "Français", ["cost.free"] = "Gratuit", ["cost.needs"] = "{0} (il faut {1})" });
        Lang.Use("fr-CA");
        Assert.Equal("Free", Lang.En("cost.free"));
        Assert.Equal("Gratuit", TerrainEditor.Terrain.PieceCost.Of(Array.Empty<string>()).Describe());
        Assert.Equal("Free", TerrainEditor.Terrain.PieceCost.Of(Array.Empty<string>()).Describe(english: true));
        var cost = new TerrainEditor.Terrain.PieceCost.Total([("Wood", "Wood", 6)], ["Workbench"], []);
        Assert.Equal("Wood 6 (il faut Workbench)", cost.Describe());
        Assert.Equal("Wood 6 (needs Workbench)", cost.Describe(english: true));
    }

    // EnglishOf: a text shown in the language in use back to its English; one no catalogue has stays as it is, and
    // a language taken again is looked up afresh.
    [Fact]
    public void AShownTextsEnglishIsFound()
    {
        Write("fr-CA", new() { [Lang.NameKey] = "Français", ["common.cancel"] = "Annuler" });
        Lang.Use("fr-CA");
        Assert.Equal("Cancel", Lang.EnglishOf("Annuler"));
        Assert.Equal("Made in code", Lang.EnglishOf("Made in code"));
        Lang.Use(Lang.Source);
        Assert.Equal("Annuler", Lang.EnglishOf("Annuler"));
        Assert.Equal("Cancel", Lang.EnglishOf("Cancel"));
    }

    [Fact]
    public void AnUnknownOrUnreadableLanguageIsEnglish()
    {
        Assert.Equal(Lang.Source, Lang.Use("xx"));
        Assert.Equal("Cancel", Lang.T("common.cancel"));
        File.WriteAllText(Path.Combine(Folder, "nl.json"), "{ not json");
        Assert.Equal(Lang.Source, Lang.Use("nl"));
        Assert.Contains(new Lang.Language("nl", "nl"), Lang.Available());
    }

    // A hand-made file whose root is not an object, or that gives a key twice, must not throw out of Use (which
    // Program calls before the window exists), not even for a language the user never chose, since Available()
    // reads every file in the folder.
    [Fact]
    public void AFileThatIsNotOneObjectOrRepeatsAKeyDoesNotCrashTheStart()
    {
        File.WriteAllText(Path.Combine(Folder, "it.json"), "[]");
        File.WriteAllText(Path.Combine(Folder, "fr.json"), """{ "lang.name": "Français", "common.cancel": "Annuler", "common.cancel": "Abandonner" }""");
        Assert.Equal(Lang.Source, Lang.Use("it"));
        Assert.Contains(new Lang.Language("it", "it"), Lang.Available());
        Assert.Equal("fr", Lang.Use("fr"));
        Assert.Equal("Abandonner", Lang.T("common.cancel"));
        Assert.Throws<JsonException>(() => Lang.Parse(new MemoryStream("\"text\""u8.ToArray())));
    }

    [Fact]
    public void TheUsersEnglishFileCorrectsTheCarriedOne()
    {
        Write(Lang.Source, new() { ["common.cancel"] = "Never mind" });
        Lang.Use(Lang.Source);
        Assert.Equal("Never mind", Lang.T("common.cancel"));
        Assert.Equal("Copy", Lang.T("common.copy"));
    }

    [Fact]
    public void ATextTheValuesCannotGoInIsLeftOutEnglishCorrectionsToo()
    {
        Write("fr-CA", new() { [Lang.NameKey] = "Français", ["settings.worldNotFound"] = "Introuvable {0} dans {chemin}" });
        Lang.Use("fr-CA");
        Assert.Equal("World folder not found: /x", Lang.T("settings.worldNotFound", "/x"));
        Write(Lang.Source, new() { ["settings.worldNotFound"] = "Missing {0} {", ["common.cancel"] = "Stop {" });
        Lang.Use(Lang.Source);
        Assert.Equal("World folder not found: /x", Lang.T("settings.worldNotFound", "/x"));
        // A text never formatted may hold braces.
        Assert.Equal("Stop {", Lang.T("common.cancel"));
        Assert.True(Lang.Fits("{0} of {1:0.0} m", "{1:0.##} m sur {0}"));
        Assert.False(Lang.Fits("{0} m", "{0} m {"));
    }

    // A text without places is never formatted, so a translation of it may hold braces (Fits lets them through)
    // even when the code asks for it with an empty list of values, as PieceCost.Describe does.
    [Fact]
    public void ATextWithoutPlacesIsNotFormattedEvenWhenAskedWithNoValues()
    {
        Write("fr-CA", new() { [Lang.NameKey] = "Français", ["cost.free"] = "Gratuit {sans}" });
        Lang.Use("fr-CA");
        Assert.Equal("Gratuit {sans}", Lang.T("cost.free", []));
        Assert.Equal("Free", Lang.En("cost.free", []));
        Assert.Equal("World folder not found: /x", Lang.T("settings.worldNotFound", "/x"));
    }

    [Fact]
    public void TheComputersLanguageIsPickedFromItsLocales()
    {
        Assert.Equal(["fr-FR", "de", "en-GB"], Lang.FromLocales("fr_FR.UTF-8:de:en_GB@euro"));
        Assert.Empty(Lang.FromLocales("C"));
        Assert.Empty(Lang.FromLocales(null));
        Assert.Equal(["en-GB", "fr-FR"], Lang.FromMultiString("en-GB\0fr-FR\0\0"));
        string[] have = ["en", "fr", "pt-PT"];
        Assert.Equal("fr", Lang.Pick(["fr-CA"], have));
        Assert.Equal("pt-PT", Lang.Pick(["pt-BR"], have));
        Assert.Equal("fr", Lang.Pick(["ja", "fr"], have));
        Assert.Null(Lang.Pick(["ja"], have));
        // Tagalog's code finds the Filipino catalogue.
        Assert.Equal("fil", Lang.Pick(["tl-PH"], ["en", "fil"]));
        Assert.Equal("fil", Lang.Pick(["fil-PH"], ["en", "fil"]));
    }

    // A carried language with the user's corrections over it: their texts where they have one, the carried
    // translation elsewhere (not English).
    [Fact]
    public void TheUsersFileCorrectsACarriedLanguage()
    {
        Write("fr", new() { ["common.cancel"] = "Laisser tomber" });
        Assert.Equal("fr", Lang.Use("fr"));
        Assert.Equal("Laisser tomber", Lang.T("common.cancel"));
        Assert.NotEqual("Copy", Lang.T("common.copy"));
        Assert.Equal("Français", Lang.Available().Single(l => l.Code == "fr").Name);
    }

    // EnglishOfFilled: a text shown that a translation made with values, back to the English with the same values; a
    // text no translation with values makes, and one only a text of places and signs would match, have none.
    [Fact]
    public void AShownTextMadeWithValuesHasItsEnglishFound()
    {
        Write("fr-CA", new()
        {
            [Lang.NameKey] = "Français",
            ["newWorld.count.seeds"] = "{0} graines",
            ["settings.worldNotFound"] = "Dossier de mondes introuvable : {0}",
            ["newWorld.nameDistance"] = "{0} {1}",
        });
        Lang.Use("fr-CA");
        Assert.Equal("150 seeds", Lang.EnglishOfFilled("150 graines"));
        Assert.Equal("World folder not found: /x y", Lang.EnglishOfFilled("Dossier de mondes introuvable : /x y"));
        Assert.Null(Lang.EnglishOfFilled("Annuler"));
        Assert.Null(Lang.EnglishOfFilled("Haldor 300"));
    }

    [Fact]
    public void PlacesForValuesAreComparedWhateverTheirFormatOrOrder()
    {
        Assert.True(Lang.SamePlaces("{0} of {1:0.0} m", "{1:0.##} m sur {0}"));
        Assert.False(Lang.SamePlaces("{0} of {1}", "{0}"));
        Assert.True(Lang.SamePlaces("a {{literal}}", "un {{littéral}}"));
    }
}
