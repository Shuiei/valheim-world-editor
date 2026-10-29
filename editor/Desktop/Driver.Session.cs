using System.Text.Json;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The commands that open and close the worlds and areas, the Workshop, the panels and the messages.
/// </summary>
public static partial class Driver
{
    /// <summary>The 'spirv' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdSpirv(Call call)
    {
        // SPIRV-Cross (the terrain shader from Valheim for Windows) loads in this build.
        TerrainShader.SelfTest();
        return Task.FromResult("{\"spirvCross\":\"ok\"}");
    }

    /// <summary>The 'gamedata' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdGamedata(Call call)
    {
        string[] a = call.A;
        GameData.UseCopies(a[1]);
        return Task.FromResult("{}");
    }

    /// <summary>The 'world' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static async Task<string> CmdWorld(Call call)
    {
        MainWindow w = call.W;
        string[] a = call.A;
        await w.OpenWorld(() => Task.Run(() => WorldSession.Open(a[1])), "Opening the world…");
        return State(w);
    }

    /// <summary>The 'area' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static async Task<string> CmdArea(Call call)
    {
        MainWindow w = call.W;
        await w.EditArea((int)call.F(0), (int)call.F(1), (int)call.F(2));
        return State(w);
    }

    /// <summary>The 'map' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdMap(Call call)
    {
        MainWindow w = call.W;
        w.ShowMap();
        return Task.FromResult(State(w));
    }

    /// <summary>The 'workshop' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static async Task<string> CmdWorkshop(Call call)
    {
        MainWindow w = call.W;
        string[] a = call.A;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await w.OpenWorkshop(a.Length > 1 ? a[1] : null);
        return JsonSerializer.Serialize(new { opened = sw.ElapsedMilliseconds, objects = w.View.Scene?.Things.Count(t => !t.Gone) ?? 0 });
    }

    /// <summary>The 'panel' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdPanel(Call call)
    {
        MainWindow w = call.W;
        string[] args = call.Args;
        w.ShowRightPanel(args[0]);
        return Task.FromResult(State(w));
    }

    /// <summary>The 'note' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdNote(Call call)
    {
        MainWindow w = call.W;
        if (call.Args is not ["close"])
        {
            throw new InvalidOperationException("usage: note close");
        }
        w.LocationNote.IsVisible = false;
        return Task.FromResult(State(w));
    }

    /// <summary>The 'message' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdMessage(Call call)
    {
        MainWindow w = call.W;
        string[] a = call.A;
        w.StatusMessage = a.Length > 1 ? a[1] : "";
        return Task.FromResult(State(w));
    }

    /// <summary>The 'wait' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static async Task<string> CmdWait(Call call)
    {
        MainWindow w = call.W;
        await Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp(call.F(0), 0, 60000)));
        return State(w);
    }

    /// <summary>The 'state' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdState(Call call)
    {
        MainWindow w = call.W;
        return Task.FromResult(State(w));
    }
}
