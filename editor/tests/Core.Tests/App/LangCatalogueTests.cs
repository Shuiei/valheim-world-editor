using System.Text.Json;
using System.Text.RegularExpressions;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The catalogues checked against the code: every key the code asks for is in English, every English text is asked
/// for, formats with its values and has one key, and every translation matches the English. Runs alone, as LangTests.
/// </summary>
[Collection("DataDir")]
public sealed partial class LangCatalogueTests : LangTestBase
{
    private static readonly string[] Expected1 = ["Core", "Desktop"];
    /// <summary>
    /// A key asked for in the code: Lang.T("key"…), Lang.En("key"…), and the status bar's messages in two languages,
    /// Said.Of("key"…) and Msg("key"…).
    /// </summary>
    [GeneratedRegex("""(?:Lang\.(?:T|En)|Said\.Of|\bMsg)\("([^"]+)"[,)]""")]
    private static partial Regex Asked();

    // Every English text can be shown as the code asks for it: one with places takes as many values as its highest
    // place + 1 (no stray brace, no broken place: the values here are numbers, which every format takes); one without
    // is shown as it is, so it must not hold a doubled brace, which only formatting turns into one.
    [Fact]
    public void EveryEnglishTextFormatsWithItsValues()
    {
        Assert.All(Lang.English, kv =>
        {
            var places = Lang.Places().Matches(kv.Value);
            if (places.Count == 0)
            {
                Assert.False(kv.Value.Contains("{{", StringComparison.Ordinal) || kv.Value.Contains("}}", StringComparison.Ordinal), kv.Key);
                Assert.Equal(kv.Value, Lang.En(kv.Key));
                return;
            }
            int values = places.Max(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)) + 1;
            object?[] dummies = [.. Enumerable.Range(0, values).Select(i => (object?)(i + 0.5))];
            // A stray brace or a broken place throws here.
            Assert.NotEmpty(Lang.En(kv.Key, dummies));
        });
    }

    // Stacked changes each add their own block of texts: two adding the same key would leave one text unused (the
    // last one counts), so a key is given once.
    [Fact]
    public void NoKeyIsGivenTwice()
    {
        string editor = Path.Combine(Fixtures.Root, "..", "..");
        ReadOnlySpan<byte> json = File.ReadAllBytes(Path.Combine(editor, "Core", "Lang", "en.json"));
        if (json.StartsWith("\uFEFF"u8))
        {
            json = json[3..];
        }
        var reader = new Utf8JsonReader(json, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var seen = new HashSet<string>();
        var twice = new List<string>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1 && !seen.Add(reader.GetString()!))
            {
                twice.Add(reader.GetString()!);
            }
        }
        Assert.True(twice.Count == 0, "Keys given twice in en.json: " + string.Join(", ", twice));
    }

    [Fact]
    public void TheCataloguesMatchTheCodeAndTheEnglish()
    {
        string editor = Path.Combine(Fixtures.Root, "..", "..");
        string code = string.Join("\n", Expected1
            .SelectMany(d => Directory.GetFiles(Path.Combine(editor, d), "*.cs", SearchOption.AllDirectories))
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
        foreach (string file in Directory.GetFiles(Path.Combine(editor, "Core", "Lang"), "*.json"))
        {
            using var s = File.OpenRead(file);
            var texts = Lang.Parse(s);
            Assert.True(texts.ContainsKey(Lang.NameKey), file);
            Assert.All(texts, kv => Assert.True(Lang.English.TryGetValue(kv.Key, out string? en) && Lang.SamePlaces(en, kv.Value), $"{Path.GetFileName(file)}: {kv.Key}"));
        }
    }
}
