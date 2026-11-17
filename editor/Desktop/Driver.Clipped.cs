using System.Text.Json;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TerrainEditor.Desktop;

/// <summary>
/// The clipped command: the texts shown that do not fit their place (cut off, or ended with "…"), so a translation whose
/// words are longer than the English can be found and shortened.
/// </summary>
public static partial class Driver
{
    /// <summary>
    /// The 'clipped' command: every visible one-line text of the window and its open dialogs that is wider than the room
    /// it has, as [{"text", "english", "width", "room"}].
    /// </summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdClipped(Call call)
    {
        MainWindow w = call.W;
        var found = new List<Dictionary<string, object>>();
        foreach (var window in new Avalonia.Controls.Window[] { w }.Concat(w.OwnedWindows))
        {
            foreach (var tb in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Avalonia.Controls.TextBlock>())
            {
                if (!tb.IsEffectivelyVisible || tb.TextWrapping != TextWrapping.NoWrap || string.IsNullOrWhiteSpace(tb.Text) || tb.Bounds.Width <= 0)
                {
                    continue;
                }
                // The whole text on one line, untrimmed, against the room the text block was given.
                using var layout = new TextLayout(tb.Text, new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight), tb.FontSize, null);
                double room = tb.Bounds.Width - tb.Padding.Left - tb.Padding.Right;
                if (layout.WidthIncludingTrailingWhitespace > room + 1)
                {
                    found.Add(new Dictionary<string, object>
                    {
                        ["text"] = tb.Text,
                        ["english"] = English(tb.Text.Trim()),
                        ["width"] = Math.Round(layout.WidthIncludingTrailingWhitespace),
                        ["room"] = Math.Round(room),
                    });
                }
            }
        }
        return Task.FromResult(JsonSerializer.Serialize(new Dictionary<string, object> { ["clipped"] = found }));
    }
}
