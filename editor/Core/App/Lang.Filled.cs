using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TerrainEditor.App;

/// <summary>
/// The English of a text shown that a catalogue text made with values ("150 graines" for "{0} seeds"): for what names a
/// control by its English words in any language (the driver's commands).
/// </summary>
public static partial class Lang
{
    /// <summary>
    /// The texts of the language in use that take values, each as a pattern catching its values, with its key and the
    /// length of its own words (the longest wins when several match); made when first asked, null until then.
    /// </summary>
    private static List<(Regex Pattern, string Key, int Words)>? _filled;

    /// <summary>
    /// The English of <paramref name="shown"/> when it is a catalogue text of the language in use with its values put
    /// in: the English text with the same values (each as shown, its format left as it is); null when no such text
    /// makes it. Texts made only of places and signs ("{0} {1}") are left out, as they would match anything.
    /// </summary>
    /// <param name="shown">A text as the window shows it.</param>
    internal static string? EnglishOfFilled(string shown)
    {
        var filled = _filled ??= [.. _texts
            .Where(kv => English.ContainsKey(kv.Key) && Places().IsMatch(English[kv.Key]))
            .Select(kv => (Pattern(kv.Value), kv.Key, Words(kv.Value)))
            .Where(p => p.Item3 > 0)];
        (Match Found, string Key, int Words)? best = null;
        foreach (var (pattern, key, words) in filled)
        {
            var m = pattern.Match(shown);
            if (m.Success && (best == null || words > best.Value.Words))
            {
                best = (m, key, words);
            }
        }
        if (best is not var (found, bestKey, _))
        {
            return null;
        }
        int count = Places().Matches(English[bestKey]).Max(p => int.Parse(p.Groups[1].Value, CultureInfo.InvariantCulture)) + 1;
        object?[] values = [.. Enumerable.Range(0, count).Select(i => found.Groups["p" + i.ToString(CultureInfo.InvariantCulture)] is { Success: true } g ? g.Value : "")];
        return En(bestKey, values);
    }

    /// <summary>
    /// A text with places as a pattern for the whole of a text it makes: its words as they are, each place catching
    /// what was put there (a place given twice must catch the same both times).
    /// </summary>
    private static Regex Pattern(string text)
    {
        var sb = new StringBuilder("^");
        var seen = new HashSet<string>();
        int at = 0;
        foreach (Match m in Places().Matches(text))
        {
            sb.Append(Regex.Escape(Literal(text[at..m.Index])));
            string n = m.Groups[1].Value;
            sb.Append(seen.Add(n) ? $"(?<p{n}>.+?)" : $"\\k<p{n}>");
            at = m.Index + m.Length;
        }
        sb.Append(Regex.Escape(Literal(text[at..]))).Append('$');
        return new Regex(sb.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }

    /// <summary>How many letters a text with places has outside them: none for "{0} {1}" or "{0} ({1})".</summary>
    private static int Words(string text) => Places().Replace(text, "").Count(char.IsLetter);

    /// <summary>A piece of a text with places as formatting shows it: a doubled brace is one.</summary>
    private static string Literal(string piece) => piece.Replace("{{", "{", StringComparison.Ordinal).Replace("}}", "}", StringComparison.Ordinal);
}
