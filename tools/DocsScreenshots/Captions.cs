using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// The texts this tool draws on the pictures (the labels on shots and pairs, the drawn diagrams' words), in the
/// language the pictures are taken in: the scenes write them in English and every helper that draws text asks
/// <see cref="Tr(string)"/> for them.
/// </summary>
/// <remarks>
/// The language is the app's: the first of LANGUAGE, LC_ALL, LC_MESSAGES and LANG ("fr", "pt_BR.UTF-8", "fr:en") there
/// is a catalogue of in editor/Core/Lang, chosen as the app chooses (editor/Core/App/Lang.Platform.cs and
/// Lang.Languages.cs: the code itself, else its main language, else a catalogue of the same main language); English
/// when none. A caption is looked up (a) in this tool's caption file, Captions/&lt;code&gt;.json (English to the
/// translation), then (b) in the app's catalogue when the caption is exactly one of its English texts (the same key in
/// &lt;code&gt;.json), so an interface word on a picture reads as in the window. A caption neither has fails loudly:
/// no English slips into a translated picture. <c>--captions-check</c> (Scenes.Captions.cs) looks up every caption
/// in every language without the app.
/// </remarks>
public static partial class Captions
{
    /// <summary>The language the scenes write their captions in: the app's source language.</summary>
    public const string English = "en";

    /// <summary>The caption files: Captions/&lt;code&gt;.json beside this tool's sources.</summary>
    public static string Folder => Path.Combine(Editor.Repo, "tools", "DocsScreenshots", "Captions");

    /// <summary>The app's catalogues: editor/Core/Lang/&lt;code&gt;.json.</summary>
    public static string Catalogues => Path.Combine(Editor.Repo, "editor", "Core", "Lang");

    /// <summary>The captions' language in use: the app's (read the first time), until <see cref="Use"/> changes it.</summary>
    public static Translator Current
    {
        get => field ??= new Translator(AppLanguage(), collect: false);
        private set;
    }

    /// <summary>Takes the language <paramref name="code"/> for the captions from now on (the check goes through each).</summary>
    /// <param name="code">A catalogue's code ("fr", "pt-BR"), or <see cref="English"/>.</param>
    /// <param name="collect">Note the captions with no translation (<see cref="Translator.Missing"/>) and draw their
    /// English instead of failing: for the check, which lists them all.</param>
    /// <returns>The translator now in use.</returns>
    public static Translator Use(string code, bool collect = false) => Current = new Translator(code, collect);

    /// <summary>A caption in the pictures' language.</summary>
    /// <param name="english">The caption as the scene writes it.</param>
    /// <returns>Its translation (the English itself in English).</returns>
    /// <exception cref="InvalidOperationException">The language has no translation of it.</exception>
    public static string Tr(string english) => Current.Lookup(english);

    /// <summary>A caption with values in its places ("{0} m"), in the pictures' language.</summary>
    /// <param name="english">The caption as the scene writes it, with its places.</param>
    /// <param name="values">The values put in its places (numbers written as in English: 8, 12.5).</param>
    /// <returns>Its translation, filled.</returns>
    /// <exception cref="InvalidOperationException">The language has no translation of it, or one whose places differ.</exception>
    public static string Tr(string english, params object?[] values)
    {
        string text = Current.Lookup(english);
        if (Places(text) != Places(english))
        {
            throw new InvalidOperationException($"{Current.Code} caption \"{english}\": its translation \"{text}\" has other places than {{0}}, {{1}}… of the English");
        }
        return string.Format(CultureInfo.InvariantCulture, text, values);
    }

    /// <summary>The places in a text ("{0}", "{1:0.0}"), by number, in order: what a translation must keep.</summary>
    private static string Places(string text) =>
        string.Join(",", PlaceRegex().Matches(text).Select(m => m.Groups[1].Value).Distinct().Order(StringComparer.Ordinal));

    /// <summary>A place in a text: "{0}", "{1:0.0}" (its number in group 1).</summary>
    [GeneratedRegex(@"\{(\d+)(?::[^}]*)?\}")]
    private static partial Regex PlaceRegex();

    /// <summary>The locale variables the app reads its language from on Linux, most wanted first.</summary>
    private static readonly string[] LocaleVariables = ["LANGUAGE", "LC_ALL", "LC_MESSAGES", "LANG"];

    /// <summary>
    /// The language the app shows when started by this tool (no language in the stand-in settings): the first of the
    /// locale variables' codes there is a catalogue of, English when none.
    /// </summary>
    public static string AppLanguage()
    {
        var wanted = LocaleVariables
            .SelectMany(v => FromLocales(Environment.GetEnvironmentVariable(v)))
            .Distinct();
        return Pick(wanted, Languages()) ?? English;
    }

    /// <summary>The languages there are catalogues of (English among them), by code.</summary>
    public static IReadOnlyList<string> Languages() =>
        [.. Directory.GetFiles(Catalogues, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().Append(English).Distinct().Order(StringComparer.Ordinal)];

    /// <summary>Codes from a locale variable, as the app reads them: "fr_FR.UTF-8" is fr-FR, "de:en" is de then en; C and POSIX are none.</summary>
    private static IEnumerable<string> FromLocales(string? value) =>
        (value ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Split('.', '@')[0].Replace('_', '-'))
            .Where(v => v.Length > 0 && v is not ("C" or "POSIX"));

    /// <summary>
    /// The first wanted language there is a catalogue of, as the app picks it: the exact one ("pt-BR"), else its main
    /// language ("pt"), else a catalogue of the same main language ("pt-PT" finds pt-BR); null when none fits.
    /// </summary>
    private static string? Pick(IEnumerable<string> wanted, IReadOnlyList<string> available)
    {
        foreach (string w in wanted)
        {
            string main = w.Split('-')[0];
            string? found = available.FirstOrDefault(c => c.Equals(w, StringComparison.OrdinalIgnoreCase))
                ?? available.FirstOrDefault(c => c.Equals(main, StringComparison.OrdinalIgnoreCase))
                ?? available.FirstOrDefault(c => c.Split('-')[0].Equals(main, StringComparison.OrdinalIgnoreCase));
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>A JSON file of texts by key (a caption file, a catalogue), read; throws when it is not one.</summary>
    /// <param name="path">The file.</param>
    internal static Dictionary<string, string> ReadTexts(string path) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"{path}: not a JSON object of texts");

    /// <summary>The captions in one language: its caption file and its catalogue, and what was asked of them.</summary>
    public sealed class Translator
    {
        /// <summary>This language's caption file, English to translation (empty in English or without a file).</summary>
        private readonly Dictionary<string, string> _captions = new(StringComparer.Ordinal);

        /// <summary>The app's texts in this language by their English: more than one when keys sharing an English differ.</summary>
        private readonly Dictionary<string, List<(string Key, string Text)>> _catalogue = new(StringComparer.Ordinal);

        /// <summary>Note missing captions instead of failing.</summary>
        private readonly bool _collect;

        /// <summary>This language's captions: its caption file, then the app's catalogue.</summary>
        /// <param name="code">The language's code ("fr", "pt-BR"), or <see cref="English"/>.</param>
        /// <param name="collect">Note missing captions (<see cref="Missing"/>) and give their English instead of failing.</param>
        public Translator(string code, bool collect)
        {
            Code = code;
            _collect = collect;
            if (code == English)
            {
                return;
            }
            if (File.Exists(CaptionFile))
            {
                foreach (var (english, text) in ReadTexts(CaptionFile))
                {
                    _captions[english] = text;
                }
            }
            string catalogue = Path.Combine(Catalogues, code + ".json");
            if (File.Exists(catalogue))
            {
                var texts = ReadTexts(catalogue);
                foreach (var (key, english) in ReadTexts(Path.Combine(Catalogues, English + ".json")))
                {
                    if (texts.TryGetValue(key, out string? text))
                    {
                        if (!_catalogue.TryGetValue(english, out var found))
                        {
                            _catalogue[english] = found = [];
                        }
                        found.Add((key, text));
                    }
                }
            }
        }

        /// <summary>The language's code.</summary>
        public string Code { get; }

        /// <summary>This language's caption file (it may not exist).</summary>
        public string CaptionFile => Path.Combine(Folder, Code + ".json");

        /// <summary>The captions asked for, in English.</summary>
        public HashSet<string> Asked { get; } = new(StringComparer.Ordinal);

        /// <summary>The captions asked for that have no translation (only noted when collecting).</summary>
        public HashSet<string> Missing { get; } = new(StringComparer.Ordinal);

        /// <summary>The caption file's entries, in English.</summary>
        public IEnumerable<string> FileEntries => _captions.Keys;

        /// <summary>A caption in this language: the caption file's, else the catalogue's when it is one of its English texts.</summary>
        /// <param name="english">The caption as the scene writes it.</param>
        /// <returns>Its translation (the English itself in English, or when collecting and there is none).</returns>
        /// <exception cref="InvalidOperationException">There is no translation of it (and the translator does not collect).</exception>
        public string Lookup(string english)
        {
            Asked.Add(english);
            if (Code == English)
            {
                return english;
            }
            if (_captions.TryGetValue(english, out string? text))
            {
                return text;
            }
            if (_catalogue.TryGetValue(english, out var found))
            {
                var distinct = found.Select(f => f.Text).Distinct(StringComparer.Ordinal).ToList();
                if (distinct.Count == 1)
                {
                    return distinct[0];
                }
                return Fail(english, $"the {Code} catalogue translates it {distinct.Count} ways ({string.Join(", ", found.Select(f => $"{f.Key}: \"{f.Text}\""))}): put the one meant in {CaptionFile}");
            }
            return Fail(english, $"it is neither in {CaptionFile} nor an English text of the app's catalogue: add its translation to {CaptionFile}");
        }

        /// <summary>A caption with no translation: noted when collecting (its English given back), else an error.</summary>
        private string Fail(string english, string why)
        {
            if (_collect)
            {
                Missing.Add(english);
                return english;
            }
            throw new InvalidOperationException($"caption \"{english}\" has no {Code} translation: {why}");
        }
    }
}
