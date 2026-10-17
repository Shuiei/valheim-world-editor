using System.Globalization;

namespace TerrainEditor.App;

/// <summary>
/// Translated texts: checking each against its English (the same places for values, one that can be formatted), and
/// finding the English of a text shown in the language in use.
/// </summary>
public static partial class Lang
{
    /// <summary>The texts of the language in use back to their English (EnglishOf), made when first asked; null until then.</summary>
    private static Dictionary<string, string>? _back;

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
        catch (FormatException e)
        {
            Log.CaughtBrief(e, "checking a translated text's places for values (not used)");
            return false;
        }
    }

    /// <summary>Whether two texts have the same places for values (each {n} as often, whatever its format).</summary>
    internal static bool SamePlaces(string a, string b)
    {
        static string Of(string s) => string.Join(",", Places().Matches(s).Select(m => m.Groups[1].Value).Order(StringComparer.Ordinal));
        return Of(a) == Of(b);
    }
}
