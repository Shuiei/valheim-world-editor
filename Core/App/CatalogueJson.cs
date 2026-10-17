using System.Globalization;
using System.Text;

namespace TerrainEditor.App;

/// <summary>
/// The JSON of the game data read at the editor's start (GameCatalogues), written the way the
/// catalogues once kept in Core/WorldGen were (Python's json module), so each kind reads exactly as its
/// catalogue did: floats in their shortest form ("1.0", "0.30000001192092896", "1e-05"), numbers
/// rounded half to even, keys in the order they were added (or sorted), the same separators and line
/// breaks. Values: null, bool, int or long (ints), double (floats), string, IList, <c>Dict</c>.
/// </summary>
internal static partial class CatalogueJson
{
    /// <summary>
    /// How a catalogue is written, as Python's json.dumps arguments.
    /// </summary>
    /// <param name="ItemSep">Between items (Python's separators[0]); with an indent the line break follows it.</param>
    /// <param name="KeySep">Between a key and its value (separators[1]).</param>
    /// <param name="Indent">Spaces per level, each item on its own line; null: all on one line.</param>
    /// <param name="SortKeys">Keys in ordinal order instead of the order they were added (sort_keys).</param>
    /// <param name="Ascii">Characters past '~' written as \u escapes (ensure_ascii).</param>
    public sealed record Style(string ItemSep = ", ", string KeySep = ": ", int? Indent = null, bool SortKeys = false, bool Ascii = true);

    /// <summary>
    /// A value (null, bool, int, long, double, string, Dict or IList, nested) as JSON text in the given style.
    /// </summary>
    public static string Write(object? value, Style style)
    {
        var sb = new StringBuilder();
        Append(sb, value, style, 0);
        return sb.ToString();
    }

    /// <summary>
    /// Writes one value at a nesting level; anything else than the kinds <c>Write</c> takes throws ArgumentException.
    /// </summary>
    private static void Append(StringBuilder sb, object? v, Style style, int level)
    {
        switch (v)
        {
            case null:
                sb.Append("null");
                break;
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case int i:
                sb.Append(i.ToString(CultureInfo.InvariantCulture));
                break;
            case long l:
                sb.Append(l.ToString(CultureInfo.InvariantCulture));
                break;
            case double d:
                sb.Append(Repr(d));
                break;
            case string s:
                Quote(sb, s, style.Ascii);
                break;
            case Dict dict:
                var items = style.SortKeys ? dict.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList() : [.. dict];
                Container(sb, '{', '}', items.Count, style, level, (k, lv) =>
                {
                    Quote(sb, items[k].Key, style.Ascii);
                    sb.Append(style.KeySep);
                    Append(sb, items[k].Value, style, lv);
                });
                break;
            case System.Collections.IList list:
                Container(sb, '[', ']', list.Count, style, level, (k, lv) => Append(sb, list[k], style, lv));
                break;
            default:
                throw new ArgumentException($"no JSON for a {v.GetType().Name}");
        }
    }
}
