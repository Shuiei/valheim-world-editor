using System.Globalization;
using System.Text;

namespace TerrainEditor.App;

/// <summary>
/// The JSON syntax of <c>CatalogueJson</c>: lists and dictionaries with their separators, and strings with their escapes.
/// </summary>
internal static partial class CatalogueJson
{
    /// <summary>
    /// A list or dictionary: with an indent, each item on a line of its own (Python's indent=0: no spaces).
    /// </summary>
    private static void Container(StringBuilder sb, char open, char close, int count, Style style, int level, Action<int, int> item)
    {
        if (count == 0)
        {
            sb.Append(open).Append(close);
            return;
        }
        sb.Append(open);
        string? newline = style.Indent is int n ? "\n" + new string(' ', n * (level + 1)) : null;
        sb.Append(newline);
        for (int k = 0; k < count; k++)
        {
            if (k > 0)
            {
                sb.Append(style.ItemSep).Append(newline);
            }
            item(k, level + 1);
        }
        if (style.Indent is int m)
        {
            sb.Append('\n').Append(' ', m * level);
        }
        sb.Append(close);
    }

    /// <summary>
    /// A JSON string as Python writes it: the short escapes, \u for other control characters and, with ascii, for
    /// anything past '~'.
    /// </summary>
    private static void Quote(StringBuilder sb, string s, bool ascii)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < ' ' || ascii && c > '~')
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        sb.Append('"');
    }
}
