namespace TerrainEditor.App;

/// <summary>
/// A message in two languages: as the user sees it, in the language in use, and in English, which is what Claude reads
/// back (the status bar's last message in its answers: everything Claude is told stays English). Made from a catalogue
/// key and its values (<see cref="Of"/>), or from a text that is the same in both (<see cref="Raw"/>: an exception's
/// message, a name). Parts are joined with a space (<c>+</c>).
/// </summary>
/// <param name="Shown">The message in the language in use.</param>
/// <param name="English">The same message in English.</param>
public sealed record Said(string Shown, string English)
{
    /// <summary>No message.</summary>
    public static Said Empty { get; } = new("", "");

    /// <summary>
    /// The text of <paramref name="key"/> with the values put in its places, in both languages. A value that is a
    /// <see cref="Said"/> (a name already in the user's language, such as a biome's) goes in as its own language's
    /// text on each side.
    /// </summary>
    /// <param name="key">The catalogue key, written in full.</param>
    /// <param name="values">The values for its places ({0}, {1}…).</param>
    public static Said Of(string key, params object?[] values) =>
        new(Lang.T(key, Side(values, english: false)), Lang.En(key, Side(values, english: true)));

    /// <summary>A text that is the same in every language (an exception's message, a file name).</summary>
    /// <param name="text">The text.</param>
    public static Said Raw(string text) => new(text, text);

    /// <summary>Messages joined with <paramref name="separator"/>, each language with its own texts (empty ones left out).</summary>
    /// <param name="separator">What goes between two messages, such as ", " for a list.</param>
    /// <param name="parts">The messages, in order.</param>
    public static Said Join(string separator, IEnumerable<Said> parts)
    {
        var list = parts.ToList();
        return new(Joined(separator, list.Select(p => p.Shown)), Joined(separator, list.Select(p => p.English)));
    }

    /// <summary>Two messages one after the other, joined with a space (an empty one adds nothing).</summary>
    /// <param name="first">The first message.</param>
    /// <param name="then">The message after it.</param>
    public static Said operator +(Said first, Said then) => Join(" ", [first, then]);

    /// <summary>The values with each <see cref="Said"/> replaced by one language's text.</summary>
    /// <param name="values">The values as given.</param>
    /// <param name="english">English rather than the shown text.</param>
    private static object?[] Side(object?[] values, bool english) =>
        values.Any(v => v is Said) ? [.. values.Select(v => v is Said s ? (english ? s.English : s.Shown) : v)] : values;

    /// <summary>The texts that are not empty, joined with <paramref name="separator"/>.</summary>
    /// <param name="separator">What goes between two texts.</param>
    /// <param name="texts">The texts.</param>
    private static string Joined(string separator, IEnumerable<string> texts) => string.Join(separator, texts.Where(t => t.Length > 0));
}
