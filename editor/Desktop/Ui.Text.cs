using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

public static partial class Ui
{
    /// <summary>
    /// A path under the home folder as ~/… (Linux and macOS, as a terminal shows it): shorter, and no
    /// one's user name on screen. Unchanged on Windows and outside the home folder.
    /// </summary>
    public static string Tilde(string path)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('/');
        return !OperatingSystem.IsWindows() && home.Length > 1 && (path == home || path.StartsWith(home + "/")) ? "~" + path[home.Length..] : path;
    }

    /// <summary>"Valheim World Editor", "Valheim" in the accent colour (the start page's and the map's title).</summary>
    public static TextBlock BrandTitle(double size, VerticalAlignment valign = VerticalAlignment.Stretch)
    {
        var t = new TextBlock { FontSize = size, FontWeight = FontWeight.SemiBold, VerticalAlignment = valign };
        t.Inlines ??= [];
        t.Inlines.Add(new Avalonia.Controls.Documents.Run("Valheim") { Foreground = Accent });
        t.Inlines.Add(new Avalonia.Controls.Documents.Run(" World Editor"));
        return t;
    }

    /// <summary>A small grey wrapped explanation under a control.</summary>
    public static TextBlock Hint(string text) => new() { Text = text, FontSize = 11.5, Foreground = Muted, TextWrapping = TextWrapping.Wrap };
}
