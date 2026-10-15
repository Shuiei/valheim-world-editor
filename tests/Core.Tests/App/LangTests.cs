using System.Text.Json;
using System.Text.RegularExpressions;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The app's texts by language (Lang): the English catalogue carried inside, a text with values put in, a language
/// taken from the user's lang folder over English (its broken texts left out), Claude's texts kept in English, a shown
/// text's English found, the computer's language picked, and the catalogues checked against the code: every key the
/// code asks for is in English, every English text is asked for, every translation matches the English. It changes the
/// language in use, so it runs alone.
/// </summary>
[Collection("DataDir")]
public sealed partial class LangTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vwe-lang-" + Guid.NewGuid().ToString("N")[..8]);

    public LangTests()
    {
        Directory.CreateDirectory(_folder);
        Lang.FolderOverride = _folder;
    }

    public void Dispose()
    {
        Lang.FolderOverride = null;
        Lang.Use(Lang.Source);
        Directory.Delete(_folder, true);
    }

    /// <summary>Writes a catalogue of <paramref name="code"/> in the user's lang folder.</summary>
    private void Write(string code, Dictionary<string, string> texts) =>
        File.WriteAllText(Path.Combine(_folder, code + ".json"), JsonSerializer.Serialize(texts));

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
        Write("fr", new()
        {
            [Lang.NameKey] = "Français",
            ["common.cancel"] = "Annuler",
            ["settings.worldNotFound"] = "Dossier de mondes introuvable : {0}",
            // Other places for values than the English: left out, English shown.
            ["settings.bepInExNotFound"] = "Dossier BepInEx introuvable",
            // No English text has that key: left out.
            ["no.such.text"] = "Rien",
        });
        Assert.Equal("fr", Lang.Use("fr"));
        Assert.Equal("fr", Lang.Current);
        Assert.Equal("Annuler", Lang.T("common.cancel"));
        Assert.Equal("Dossier de mondes introuvable : /x", Lang.T("settings.worldNotFound", "/x"));
        Assert.Equal("BepInEx folder not found: /x", Lang.T("settings.bepInExNotFound", "/x"));
        Assert.Equal("Copy", Lang.T("common.copy"));
        Assert.Throws<KeyNotFoundException>(() => Lang.T("no.such.text"));
        Assert.Contains(new Lang.Language("fr", "Français"), Lang.Available());
        Assert.Equal(Lang.Source, Lang.Available()[0].Code);
    }

    [Fact]
    public void WhatClaudeIsToldStaysInEnglish()
    {
        Write("fr", new() { [Lang.NameKey] = "Français", ["cost.free"] = "Gratuit", ["cost.needs"] = "{0} (il faut {1})" });
        Lang.Use("fr");
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
        Write("fr", new() { [Lang.NameKey] = "Français", ["common.cancel"] = "Annuler" });
        Lang.Use("fr");
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
        File.WriteAllText(Path.Combine(_folder, "de.json"), "{ not json");
        Assert.Equal(Lang.Source, Lang.Use("de"));
        Assert.Contains(new Lang.Language("de", "de"), Lang.Available());
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
        Write("fr", new() { [Lang.NameKey] = "Français", ["settings.worldNotFound"] = "Introuvable {0} dans {chemin}" });
        Lang.Use("fr");
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
        Write("fr", new() { [Lang.NameKey] = "Français", ["cost.free"] = "Gratuit {sans}" });
        Lang.Use("fr");
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
        string[] have = ["en", "fr", "pt-PT"];
        Assert.Equal("fr", Lang.Pick(["fr-CA"], have));
        Assert.Equal("pt-PT", Lang.Pick(["pt-BR"], have));
        Assert.Equal("fr", Lang.Pick(["ja", "fr"], have));
        Assert.Null(Lang.Pick(["ja"], have));
    }

    [Fact]
    public void PlacesForValuesAreComparedWhateverTheirFormatOrOrder()
    {
        Assert.True(Lang.SamePlaces("{0} of {1:0.0} m", "{1:0.##} m sur {0}"));
        Assert.False(Lang.SamePlaces("{0} of {1}", "{0}"));
        Assert.True(Lang.SamePlaces("a {{literal}}", "un {{littéral}}"));
    }

    /// <summary>A key asked for in the code: Lang.T("key"…).</summary>
    [GeneratedRegex("""Lang\.T\("([^"]+)"[,)]""")]
    private static partial Regex Asked();

    [Fact]
    public void TheCataloguesMatchTheCodeAndTheEnglish()
    {
        string repo = Path.Combine(Fixtures.Root, "..", "..");
        string code = string.Join("\n", new[] { "Core", "Desktop" }
            .SelectMany(d => Directory.GetFiles(Path.Combine(repo, d), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText));
        var missing = Asked().Matches(code).Select(m => m.Groups[1].Value).Distinct().Where(k => !Lang.English.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, "Texts the code asks for that are not in en.json: " + string.Join(", ", missing));
        // The tips are asked for by their key alone (Tips.cs, checked by TipsTests), the rest by their full key (some
        // through a variable: the key is still written in the code).
        var unused = Lang.English.Keys.Where(k => !k.StartsWith("tip.", StringComparison.Ordinal) && k != Lang.NameKey && !code.Contains($"\"{k}\"", StringComparison.Ordinal)).ToList();
        Assert.True(unused.Count == 0, "Texts in en.json no code asks for: " + string.Join(", ", unused));
        Assert.All(Lang.English, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), kv.Key));
        // Each carried translation: only English keys, the same places for values.
        foreach (string file in Directory.GetFiles(Path.Combine(repo, "Core", "Lang"), "*.json"))
        {
            using var s = File.OpenRead(file);
            var texts = Lang.Parse(s);
            Assert.True(texts.ContainsKey(Lang.NameKey), file);
            Assert.All(texts, kv => Assert.True(Lang.English.TryGetValue(kv.Key, out string? en) && Lang.SamePlaces(en, kv.Value), $"{Path.GetFileName(file)}: {kv.Key}"));
        }
    }
}
