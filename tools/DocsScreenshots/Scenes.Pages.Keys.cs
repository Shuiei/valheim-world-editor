namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// Typing into a text box the driver cannot fill (the Script tool's code has no placeholder to find it by): clicked and
/// typed on the display with xdotool.
/// </summary>
public sealed partial class Scenes
{
    /// <summary>
    /// Clicks a point of the editor's window (pixels from its top left), selects all there and types the text (new lines
    /// as Return). Throws when xdotool is missing or fails: the picture would show the wrong text. The text should hold
    /// no F: typed in the Script tool's code, it also switches the 3D view to walking.
    /// </summary>
    /// <param name="e">The editor.</param>
    /// <param name="x">Pixels from the window's left.</param>
    /// <param name="y">Pixels from the window's top.</param>
    /// <param name="text">The text to type.</param>
    private static void TypeAt(Editor e, int x, int y, string text)
    {
        string window = RunXdotool("search", "--onlyvisible", "--name", "Valheim World Editor").Split('\n', StringSplitOptions.RemoveEmptyEntries)[0];
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        RunXdotool("mousemove", "--window", window, x.ToString(inv), y.ToString(inv), "click", "1");
        // Real key presses (not sent to the window: the app ignores those), to the window under the pointer; each new
        // line as Return (xdotool types it as Linefeed, which a text box does not take).
        RunXdotool("key", "ctrl+a");
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                RunXdotool("key", "Return");
            }
            if (lines[i].Length > 0)
            {
                RunXdotool("type", "--delay", "8", lines[i]);
            }
        }
        RunXdotool("mousemove", "1915", "1075");
        e.Send("wait 500");
    }

    /// <summary>
    /// Turns the mouse wheel over points of the editor's window (pixels from its top left), one notch at each, in turn:
    /// up (positive) or down. Throws when xdotool is missing or fails.
    /// </summary>
    /// <param name="e">The editor.</param>
    /// <param name="notches">Each point and its notch.</param>
    private static void WheelAt(Editor e, params (int X, int Y, int Notch)[] notches)
    {
        string window = RunXdotool("search", "--onlyvisible", "--name", "Valheim World Editor").Split('\n', StringSplitOptions.RemoveEmptyEntries)[0];
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var args = new List<string>();
        foreach (var (x, y, notch) in notches)
        {
            args.AddRange(["mousemove", "--window", window, x.ToString(inv), y.ToString(inv), "sleep", "0.3", "click", notch > 0 ? "4" : "5", "sleep", "0.5"]);
        }
        args.AddRange(["mousemove", "1915", "1075"]);
        RunXdotool([.. args]);
        e.Send("wait 500");
    }

    /// <summary>Clicks a point of the editor's window (pixels from its top left), then moves the pointer off it.</summary>
    /// <param name="e">The editor.</param>
    /// <param name="at">The point.</param>
    private static void ClickAt(Editor e, (int X, int Y) at)
    {
        string window = RunXdotool("search", "--onlyvisible", "--name", "Valheim World Editor").Split('\n', StringSplitOptions.RemoveEmptyEntries)[0];
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        RunXdotool("mousemove", "--window", window, at.X.ToString(inv), at.Y.ToString(inv), "sleep", "0.3", "click", "1", "sleep", "0.2", "mousemove", "1915", "1075");
        e.Send("wait 500");
    }
}
