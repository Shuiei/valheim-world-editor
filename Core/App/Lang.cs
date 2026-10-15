using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TerrainEditor.App;

/// <summary>
/// The words the app shows, in the user's language: a catalogue of texts by key per language (<c>Core/Lang/en.json</c>:
/// English, the source every other is translated from). Catalogues are carried inside the app, and more can be dropped
/// in the data folder's <c>lang</c> folder (<c>fr.json</c>…), where one also adds to or corrects the carried catalogue
/// of its language. A text a catalogue does not have yet, or whose <c>{0}</c> places differ from the English, is shown
/// in English.
/// </summary>
public static partial class Lang
{
    /// <summary>The source language, which has every text.</summary>
    public const string Source = "en";

    /// <summary>The key of a catalogue's own name for its language ("English", "Français"), as Settings lists it.</summary>
    public const string NameKey = "lang.name";

    /// <summary>The English texts, by key.</summary>
    public static IReadOnlyDictionary<string, string> English { get; } = Carried(Source) ?? throw new InvalidOperationException("The English texts are not in the app.");

    /// <summary>The language in use (<c>Use</c>): a catalogue's code, such as "en" or "pt-BR".</summary>
    public static string Current { get; private set; } = Source;

    /// <summary>The texts of the language in use, by key; English where they have none.</summary>
    private static IReadOnlyDictionary<string, string> _texts = English;

    /// <summary>The texts of the language in use back to their English (EnglishOf), made when first asked; null until then.</summary>
    private static Dictionary<string, string>? _back;

    /// <summary>The data folder's catalogues (null: <c>AppSettings.DataDir</c>'s lang folder; tests set another).</summary>
    internal static string? FolderOverride { get; set; }

    /// <summary>The folder of the user's own catalogues: the data folder's lang folder.</summary>
    public static string Folder => FolderOverride ?? Path.Combine(AppSettings.DataDir, "lang");

    /// <summary>A text's places for values: {0}, {1:0.0}…</summary>
    [GeneratedRegex(@"(?<!\{)\{(\d+)[^{}]*\}")]
    private static partial Regex Places();

    /// <summary>The text of <paramref name="key"/> in the language in use.</summary>
    /// <exception cref="KeyNotFoundException">No English text has that key (a mistake in the code).</exception>
    public static string T(string key) => _texts.TryGetValue(key, out string? t) || English.TryGetValue(key, out t)
        ? t
        : throw new KeyNotFoundException("No text " + key);

    /// <summary>The text of <paramref name="key"/> in the language in use, with the values put in its places ({0}, {1}…).</summary>
    public static string T(string key, params object?[] values) => string.Format(CultureInfo.InvariantCulture, T(key), values);

    /// <summary>
    /// The English text of <paramref name="key"/>, with the values put in its places, whatever the language in use: for
    /// what is not shown to the user (Claude's answers).
    /// </summary>
    public static string En(string key, params object?[] values) => string.Format(CultureInfo.InvariantCulture, English[key], values);

    /// <summary>
    /// The English of a text shown in the language in use: what does not depend on the language (Claude's ids for the
    /// window's controls, which of them it may not use) is worked out from it. A text no catalogue text is, such as one
    /// made in code, is returned as it is.
    /// </summary>
    /// <param name="shown">A text as the window shows it.</param>
    /// <returns>Its English text, else <paramref name="shown"/>.</returns>
    public static string EnglishOf(string shown)
    {
        var back = _back;
        if (back == null)
        {
            back = [];
            foreach (var (key, text) in _texts)
            {
                if (English.TryGetValue(key, out string? en))
                {
                    back.TryAdd(text, en);
                }
            }
            _back = back;
        }
        return back.TryGetValue(shown, out string? english) ? english : shown;
    }

    /// <summary>
    /// Takes the language <paramref name="code"/> from now on (texts already shown stay as they are: the app takes it
    /// when it starts); the computer's language when null or empty; English when there is no such catalogue.
    /// </summary>
    /// <returns>The language taken.</returns>
    public static string Use(string? code)
    {
        string chosen = (string.IsNullOrEmpty(code) ? Pick(SystemLanguages(), Available().Select(l => l.Code)) : code) ?? Source;
        IReadOnlyDictionary<string, string>? texts = chosen == Source ? null : Load(chosen);
        if (texts == null)
        {
            // The source, with the user's corrections to it if any (checked like a translation).
            chosen = Source;
            texts = Load(Source) ?? English;
        }
        _texts = texts;
        _back = null;
        Current = chosen;
        return chosen;
    }

    /// <summary>A language there is a catalogue of.</summary>
    /// <param name="Code">Its code: the catalogue's file name ("en", "pt-BR").</param>
    /// <param name="Name">Its own name for itself (<c>NameKey</c>), else the code.</param>
    public sealed record Language(string Code, string Name);

    /// <summary>The languages there are catalogues of, carried and the user's, English first, then by name.</summary>
    public static IReadOnlyList<Language> Available()
    {
        var codes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { Source };
        const string Prefix = "TerrainEditor.Lang.";
        foreach (string r in typeof(Lang).Assembly.GetManifestResourceNames().Where(r => r.StartsWith(Prefix, StringComparison.Ordinal) && r.EndsWith(".json", StringComparison.Ordinal)))
        {
            codes.Add(r[Prefix.Length..^".json".Length]);
        }
        if (Directory.Exists(Folder))
        {
            foreach (string f in Directory.GetFiles(Folder, "*.json"))
            {
                codes.Add(Path.GetFileNameWithoutExtension(f));
            }
        }
        static string NameOf(string code) => code == Source ? English[NameKey] : Load(code) is { } t && t.TryGetValue(NameKey, out string? n) ? n : code;
        return [.. codes.Select(c => new Language(c, NameOf(c))).OrderBy(l => l.Code != Source).ThenBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>
    /// The catalogue of <paramref name="code"/>: the carried one with the user's file over it, each one's texts checked
    /// against the English first (those of no English key, or that do not take the English one's values, are left out,
    /// so a broken correction leaves the carried text); null when there is none.
    /// </summary>
    internal static Dictionary<string, string>? Load(string code)
    {
        var carried = Carried(code);
        var user = UserFile(code);
        if (carried == null && user == null)
        {
            return null;
        }
        static Dictionary<string, string> Checked(Dictionary<string, string>? texts) =>
            (texts ?? []).Where(kv => English.TryGetValue(kv.Key, out string? en) && Fits(en, kv.Value)).ToDictionary();
        return Merge(Checked(carried), Checked(user));
    }

    /// <summary>
    /// Whether <paramref name="text"/> can stand for the English <paramref name="en"/>: the same places for values, and,
    /// when the English takes values, a text they can be put in (no stray brace or broken place such as "{path}").
    /// </summary>
    internal static bool Fits(string en, string text)
    {
        if (!SamePlaces(en, text))
        {
            return false;
        }
        var places = Places().Matches(en);
        if (places.Count == 0)
        {
            // Shown as it is: never formatted.
            return true;
        }
        int values = places.Max(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)) + 1;
        try
        {
            _ = string.Format(CultureInfo.InvariantCulture, text, new object?[values]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Whether two texts have the same places for values (each {n} as often, whatever its format).</summary>
    internal static bool SamePlaces(string a, string b)
    {
        static string Of(string s) => string.Join(",", Places().Matches(s).Select(m => m.Groups[1].Value).Order(StringComparer.Ordinal));
        return Of(a) == Of(b);
    }

    /// <summary>A catalogue carried inside the app, null when there is none.</summary>
    private static Dictionary<string, string>? Carried(string code)
    {
        using var s = typeof(Lang).Assembly.GetManifestResourceStream($"TerrainEditor.Lang.{code}.json");
        return s == null ? null : Parse(s);
    }

    /// <summary>The user's catalogue of <paramref name="code"/> in <c>Folder</c>; null when there is none or it cannot be read (logged).</summary>
    private static Dictionary<string, string>? UserFile(string code)
    {
        string path = Path.Combine(Folder, code + ".json");
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            using var s = File.OpenRead(path);
            return Parse(s);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Console.WriteLine($"Language: {path} could not be read ({e.Message}); English is shown for it.");
            return null;
        }
    }

    /// <summary>A catalogue's file: one JSON object of texts by key (a value that is not a string is skipped).</summary>
    internal static Dictionary<string, string> Parse(Stream json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        return doc.RootElement.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String).ToDictionary(p => p.Name, p => p.Value.GetString()!);
    }

    /// <summary><paramref name="over"/>'s texts over a copy of <paramref name="under"/>'s.</summary>
    private static Dictionary<string, string> Merge(IReadOnlyDictionary<string, string> under, Dictionary<string, string>? over)
    {
        var texts = new Dictionary<string, string>(under);
        foreach (var (k, v) in over ?? [])
        {
            texts[k] = v;
        }
        return texts;
    }

    /// <summary>
    /// The first of the computer's languages there is a catalogue of: the exact one ("pt-BR"), else its main language
    /// ("pt"), else a catalogue of the same main language ("pt-PT"); null when none fits.
    /// </summary>
    internal static string? Pick(IEnumerable<string> wanted, IEnumerable<string> available)
    {
        var have = available.ToList();
        foreach (string w in wanted)
        {
            string main = w.Split('-')[0];
            string? found = have.Find(c => c.Equals(w, StringComparison.OrdinalIgnoreCase))
                ?? have.Find(c => c.Equals(main, StringComparison.OrdinalIgnoreCase))
                ?? have.Find(c => c.Split('-')[0].Equals(main, StringComparison.OrdinalIgnoreCase));
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>
    /// The computer's languages, most wanted first, as codes ("fr-FR", "de"): on Windows the user's, elsewhere from
    /// LANGUAGE, LC_ALL, LC_MESSAGES and LANG ("fr_FR.UTF-8"). The app runs without .NET's culture data, so its
    /// CultureInfo cannot say.
    /// </summary>
    internal static List<string> SystemLanguages()
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsLanguage() is string w ? [w] : [];
        }
        var codes = new List<string>();
        foreach (string var in new[] { "LANGUAGE", "LC_ALL", "LC_MESSAGES", "LANG" })
        {
            codes.AddRange(FromLocales(Environment.GetEnvironmentVariable(var)));
        }
        return [.. codes.Distinct()];
    }

    /// <summary>Codes from a locale variable: "fr_FR.UTF-8" is fr-FR, "de:en" is de then en; C and POSIX are none.</summary>
    internal static IEnumerable<string> FromLocales(string? value) =>
        (value ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Split('.', '@')[0].Replace('_', '-'))
            .Where(v => v.Length > 0 && v is not ("C" or "POSIX"));

    /// <summary>The Windows user's language ("fr-FR"), null when it cannot be read.</summary>
    private static unsafe string? WindowsLanguage()
    {
        // LOCALE_NAME_MAX_LENGTH.
        char* name = stackalloc char[85];
        int n = GetUserDefaultLocaleName(name, 85);
        return n > 1 ? new string(name, 0, n - 1) : null;
    }

    /// <summary>Windows: the user's locale name.</summary>
    [LibraryImport("kernel32.dll")]
    private static unsafe partial int GetUserDefaultLocaleName(char* name, int length);
}
