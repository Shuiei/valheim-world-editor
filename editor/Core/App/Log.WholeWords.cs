using System.Text;

namespace TerrainEditor.App;

/// <summary>
/// Replacing a whole word or path in a text (not inside a longer name), and the home folder in a text.
/// </summary>
public static partial class Log
{
    /// <summary>
    /// Text with each whole <paramref name="value"/> replaced: not followed by a letter, digit, '_', '-' or a '.' that
    /// goes on with one; with <paramref name="wordStart"/>, not after one either. Case is ignored on Windows and for
    /// words.
    /// </summary>
    private static string ReplaceWhole(string text, string value, string with, bool wordStart)
    {
        StringComparison comparison = wordStart || OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var b = new StringBuilder();
        int from = 0;
        for (int i = text.IndexOf(value, comparison); i >= 0; i = text.IndexOf(value, i + value.Length, comparison))
        {
            int end = i + value.Length;
            bool goesOn = end < text.Length && (NameChar(text[end]) || (text[end] == '.' && end + 1 < text.Length && char.IsLetterOrDigit(text[end + 1])));
            if (goesOn || (wordStart && i > 0 && NameChar(text[i - 1])))
            {
                continue;
            }
            b.Append(text, from, i - from).Append(with);
            from = end;
        }
        return b.Append(text, from, text.Length - from).ToString();
    }

    /// <summary>Whether a character can be part of a name: a letter, a digit, '_' or '-'.</summary>
    private static bool NameChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-';

    /// <summary>
    /// Text with the home folder written ~ (in the other separator too on Windows, and whatever its case there), so no
    /// one's user name is in it.
    /// </summary>
    internal static string WithoutHome(string text)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('/', '\\');
        if (home.Length < 2)
        {
            return text;
        }
        text = ReplaceWhole(text, home, "~", wordStart: false);
        return OperatingSystem.IsWindows() ? ReplaceWhole(text, home.Replace('\\', '/'), "~", wordStart: false) : text;
    }
}
