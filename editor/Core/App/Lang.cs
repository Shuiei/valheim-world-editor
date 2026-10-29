using System.Globalization;
using System.Text.RegularExpressions;

namespace TerrainEditor.App;

/// <summary>
/// The words the app shows, in the user's language: a catalogue of texts by key per language (<c>editor/Core/Lang/en.json</c>:
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

    /// <summary>The data folder's catalogues (null: <c>AppSettings.DataDir</c>'s lang folder; tests set another).</summary>
    internal static string? FolderOverride { get; set; }

    /// <summary>The folder of the user's own catalogues: the data folder's lang folder.</summary>
    public static string Folder => FolderOverride ?? Path.Combine(AppSettings.DataDir, "lang");

    /// <summary>A text's places for values: {0}, {1:0.0}…</summary>
    [GeneratedRegex(@"(?<!\{)\{(\d+)[^{}]*\}")]
    internal static partial Regex Places();

    /// <summary>The text of <paramref name="key"/> in the language in use.</summary>
    /// <exception cref="KeyNotFoundException">No English text has that key (a mistake in the code).</exception>
    public static string T(string key) => _texts.TryGetValue(key, out string? t) || English.TryGetValue(key, out t)
        ? t
        : throw new KeyNotFoundException("No text " + key);

    /// <summary>
    /// The text of <paramref name="key"/> in the language in use, with the values put in its places ({0}, {1}…). With no
    /// values the text is shown as it is, like <see cref="T(string)"/>: a translation of a text without places may hold
    /// braces (<c>Fits</c> lets them through), and formatting it would throw.
    /// </summary>
    public static string T(string key, params object?[] values) => Filled(T(key), values);

    /// <summary>
    /// The English text of <paramref name="key"/>, with the values put in its places, whatever the language in use: for
    /// what is not shown to the user (Claude's answers). With no values the text is shown as it is.
    /// </summary>
    public static string En(string key, params object?[] values) => Filled(English[key], values);

    /// <summary><paramref name="text"/> with the values put in its places; the text as it is when there are none.</summary>
    private static string Filled(string text, object?[] values) => values.Length == 0 ? text : string.Format(CultureInfo.InvariantCulture, text, values);

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
}
