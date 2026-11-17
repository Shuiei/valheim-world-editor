namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// The texts each picture shows cut off: with $DOCS_CLIPPED naming a file, every picture adds to it the one-line texts of
/// the window that are wider than their room (the driver's clipped command), so a translation's too long words are found
/// in the pictures that show them.
/// </summary>
public sealed partial class Editor
{
    /// <summary>The file the clipped texts are added to: $DOCS_CLIPPED; null when unset (nothing is checked).</summary>
    private static readonly string? ClippedLog = Environment.GetEnvironmentVariable("DOCS_CLIPPED") is { Length: > 0 } f ? f : null;

    /// <summary>Adds the texts the window shows cut off, under the picture's name, to $DOCS_CLIPPED (when set).</summary>
    /// <param name="picture">The picture just taken.</param>
    private void LogClipped(string picture)
    {
        if (ClippedLog == null)
        {
            return;
        }
        var clipped = Send("clipped").GetProperty("clipped");
        if (clipped.GetArrayLength() == 0)
        {
            return;
        }
        var lines = clipped.EnumerateArray().Select(c =>
            $"{Path.GetFileName(picture)}\t{c.GetProperty("english").GetString()}\t{c.GetProperty("text").GetString()}\t{c.GetProperty("width").GetDouble()}/{c.GetProperty("room").GetDouble()}");
        File.AppendAllLines(ClippedLog, lines);
    }
}
