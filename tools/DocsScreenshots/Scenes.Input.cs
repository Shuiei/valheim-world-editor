using System.Text.Json;

namespace TerrainEditor.DocsScreenshots;

/// <summary>The pointer and key input the scenes send: drags, paths, clicks, holds and the Area tool's box.</summary>
public sealed partial class Scenes
{
    /// <summary>A mouse drag on the ground from a to b (release false: the button stays down).</summary>
    /// <returns>The state after the release; null when the button stays down.</returns>
    private static void Drag(Editor e, (double X, double Z) a, (double X, double Z) b, int steps = 4, string button = "left", string mods = "", int hold = 30, bool release = true) =>
        Path(e, [a, b], steps, hold, button, mods, release);

    /// <summary>A drag through several points (a brush stroke along them).</summary>
    /// <returns>The state after the release; null when the button stays down.</returns>
    private static void Path(Editor e, (double X, double Z)[] points, int steps = 3, int hold = 60, string button = "left", string mods = "", bool release = true)
    {
        Move(e, points[0], mods);
        e.Send(Cmd($"mouse down {points[0].X} {points[0].Z} {button} {mods}"));
        for (int k = 1; k < points.Length; k++)
        {
            var (a, b) = (points[k - 1], points[k]);
            for (int i = 1; i <= steps; i++)
            {
                double x = a.X + (b.X - a.X) * i / steps;
                double z = a.Z + (b.Z - a.Z) * i / steps;
                e.Send(Cmd($"mouse move {x} {z} {button} {mods}"));
                e.Send($"wait {hold}");
            }
        }
        var (endX, endZ) = points[^1];
        if (release)
        {
            e.Send(Cmd($"mouse up {endX} {endZ} {button} {mods}"));
        }
    }

    /// <summary>A left click at a ground point.</summary>
    private static void Click(Editor e, (double X, double Z) p, string mods = "")
    {
        Move(e, p, mods);
        e.Send(Cmd($"mouse down {p.X} {p.Z} left {mods}"));
        e.Send(Cmd($"mouse up {p.X} {p.Z} left {mods}"));
    }

    /// <summary>The button held still at a point for a while (a brush: raise a mound, dig a hole).</summary>
    private static JsonElement Hold(Editor e, (double X, double Z) at, int ms, int steps = 1)
    {
        Move(e, at);
        e.Send($"mouse down {at.X} {at.Z}");
        for (int i = 0; i < Math.Max(1, steps); i++)
        {
            e.Send($"wait {ms / Math.Max(1, steps)}");
        }
        return e.Send($"mouse up {at.X} {at.Z}");
    }

    /// <summary>A brush held still at a point for a while (raise a mound, dig a hole).</summary>
    /// <param name="e">The editor.</param>
    /// <param name="key">The key choosing the brush (D1 is Raise).</param>
    /// <param name="at">Where the brush is held.</param>
    /// <param name="size">The brush size.</param>
    /// <param name="hold">How long the button is held, in milliseconds.</param>
    /// <param name="steps">How many waits the hold is split into (0 counts as 1).</param>
    private static JsonElement Stroke(Editor e, string key, (double X, double Z) at, double size, int hold = 1200, int steps = 0)
    {
        e.Send($"key {key}");
        e.Send($"set Size|{size}");
        return Hold(e, at, hold, steps);
    }

    /// <summary>The Area tool: a box from a to b, the action, Enter.</summary>
    /// <param name="e">The editor.</param>
    /// <param name="a">One corner.</param>
    /// <param name="b">The opposite corner.</param>
    /// <param name="action">The action to choose, by its name in the panel.</param>
    /// <param name="apply">Press Enter; false leaves the action chosen but not done.</param>
    private static void AreaAction(Editor e, (double X, double Z) a, (double X, double Z) b, string action, bool apply = true)
    {
        e.Send("key B");
        e.Send("click Box");
        Drag(e, a, b);
        e.Send($"choose {action}");
        e.Send(apply ? "key Return" : "state");
    }

    /// <summary>
    /// A left click on a point of the editor's window (pixels from its top left) with xdotool, for what the driver
    /// cannot click by its name (a row of a list that is not a drop-down); the pointer is then moved off the window.
    /// </summary>
    /// <exception cref="InvalidOperationException">xdotool is missing or fails, or the window is not found.</exception>
    private static void ClickWindow(Editor e, int x, int y)
    {
        string window = RunXdotool("search", "--onlyvisible", "--name", "Valheim World Editor").Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()
            ?? throw new InvalidOperationException("the editor's window was not found");
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        RunXdotool("mousemove", "--window", window, x.ToString(inv), y.ToString(inv), "click", "1");
        RunXdotool("mousemove", "1915", "1075");
        e.Send("wait 300");
    }

    /// <summary>
    /// The programs a scene may run on the display: xdotool (pointer and keys the driver cannot give), ImageMagick's
    /// import (the screen as it is, an open list included) and timeout (a wait for a window, with a limit).
    /// </summary>
    private static readonly string[] DisplayPrograms = ["xdotool", "import", "timeout"];

    /// <summary>
    /// Runs one of <see cref="DisplayPrograms"/> with these arguments, each passed as it is (no shell), and waits for it.
    /// </summary>
    /// <returns>What it wrote.</returns>
    /// <exception cref="InvalidOperationException">It is not one of them, does not start or fails.</exception>
    private static string RunOnDisplay(string program, params string[] args)
    {
        if (!DisplayPrograms.Contains(program))
        {
            throw new InvalidOperationException($"{program} is not a display program");
        }
        var start = new System.Diagnostics.ProcessStartInfo(program) { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (string a in args)
        {
            start.ArgumentList.Add(a);
        }
        using var p = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException($"{program} did not start");
        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode == 0 ? output : throw new InvalidOperationException($"{program} {string.Join(' ', args)}: exit code {p.ExitCode}");
    }

    /// <summary>Runs xdotool with these arguments (RunOnDisplay); its output, trimmed.</summary>
    private static string RunXdotool(params string[] args) => RunOnDisplay("xdotool", args).Trim();
}
