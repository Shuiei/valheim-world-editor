using System.Diagnostics;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// The game window and the pointer over it, driven with xdotool (the app's own driver cannot scroll or point at everything).
/// </summary>
public sealed partial class Editor
{
    /// <summary>The game window to drive: the last visible window of its process; null when there is none.</summary>
    private string? GameWindow() =>
        Xdotool("search", "--onlyvisible", "--pid", _p.Id.ToString(System.Globalization.CultureInfo.InvariantCulture))?
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

    /// <summary>Runs xdotool with these arguments; its output, or null when it is missing or fails.</summary>
    private static string? Xdotool(params string[] args)
    {
        try
        {
            var start = new ProcessStartInfo("xdotool") { RedirectStandardOutput = true, UseShellExecute = false };
            foreach (string a in args)
            {
                start.ArgumentList.Add(a);
            }
            using var p = Process.Start(start);
            if (p == null)
            {
                return null;
            }
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode == 0 ? output.Trim() : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Moves the pointer onto a point of the game window (pixels from its top left). Returns the window, or null when
    /// there is none (nothing is moved then).
    /// </summary>
    /// <param name="x">Pixels from the window's left.</param>
    /// <param name="y">Pixels from the window's top.</param>
    /// <returns>The window; null when there is none.</returns>
    private string? PointAt(int x, int y)
    {
        string? window = GameWindow();
        if (window != null)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Xdotool("mousemove", "--window", window, x.ToString(inv), y.ToString(inv));
        }
        return window;
    }

    /// <summary>
    /// Turns the mouse wheel over a point of the window (pixels from its top left), to scroll a panel the driver cannot
    /// scroll; the pointer is then moved off the window. Does nothing without xdotool.
    /// </summary>
    /// <param name="x">Pixels from the window's left.</param>
    /// <param name="y">Pixels from the window's top.</param>
    /// <param name="notches">Wheel notches: down when positive, up when negative.</param>
    public void Scroll(int x, int y, int notches)
    {
        if (PointAt(x, y) == null)
        {
            return;
        }
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        Xdotool("click", "--repeat", Math.Abs(notches).ToString(inv), "--delay", "40", notches > 0 ? "5" : "4");
        Xdotool("mousemove", "1915", "1075");
        Send("wait 300");
    }

    /// <summary>
    /// Moves the pointer onto a point of the window (pixels from its top left), so what it hovered (a room, a piece)
    /// is no longer marked. Does nothing without xdotool.
    /// </summary>
    /// <param name="x">Pixels from the window's left.</param>
    /// <param name="y">Pixels from the window's top.</param>
    public void Point(int x, int y)
    {
        if (PointAt(x, y) != null)
        {
            Send("wait 300");
        }
    }
}
