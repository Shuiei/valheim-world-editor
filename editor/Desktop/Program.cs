using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace TerrainEditor.Desktop;

/// <summary>
/// The native editor: an Avalonia window, the 3D view and the map drawn with OpenGL. It opens on the start page; the
/// options (see Options) open an area directly or let a program drive it.
/// </summary>
public static class Program
{
    /// <summary>
    /// Starts the editor, or with <c>--script-host</c>, <c>--mcp-stdio</c>, <c>--export-test-data</c> or
    /// <c>--export-locations</c> does only that and ends.
    /// </summary>
    /// <param name="args">The command line (see <see cref="Options"/>).</param>
    [STAThread]
    public static void Main(string[] args)
    {
        // --script-host: one script run for the editor that started this process (the script and the area on
        // stdin, the changes on stdout: nothing else on it, no log, no window). Ended outright once it has answered:
        // a thread the script started and left running would keep it alive.
        if (args.Contains("--script-host"))
        {
            Environment.Exit(ScriptHost.Serve());
        }
        // --mcp-stdio: only the relay to the running editor for Claude (nothing else on stdout: no log,
        // no window).
        if (args.Contains("--mcp-stdio"))
        {
            ClaudeRelay.Run();
            return;
        }
        // --export-test-data <folder>: the tests' copies of the game's data made again, each keeping the
        // entries it has (for developers, after a game update: editor/tests/fixtures).
        int exportData = Array.IndexOf(args, "--export-test-data");
        if (exportData >= 0)
        {
            Environment.ExitCode = TerrainEditor.App.GameData.Export(args.ElementAtOrDefault(exportData + 1));
            return;
        }
        // --export-locations <file>: the tests' copy of the game's location rules made again (for
        // developers, after a game update: editor/tests/fixtures/locations.json).
        int export = Array.IndexOf(args, "--export-locations");
        if (export >= 0)
        {
            Environment.ExitCode = TerrainEditor.App.GameLocations.Export(args.ElementAtOrDefault(export + 1));
            return;
        }
        // A command line that cannot be read (an option without its value, a number that is none): said, and the
        // editor does not start.
        try
        {
            Options.Parse(args);
        }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException or IndexOutOfRangeException)
        {
            // Before the log starts, and the answer to whoever typed the command: the error output only, as typed.
            Console.Error.WriteLine($"ValheimWorldEditor: {e.Message}");
            Environment.ExitCode = 2;
            return;
        }
        // Everything said also goes to ValheimWorldEditor.log in the data folder (a window app has no console on Windows).
        TerrainEditor.App.Log.Start(BuildInfo.Version);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Options.Say($"crash: {TerrainEditor.App.Log.Scrub(e.ExceptionObject.ToString() ?? "")}");
        TaskScheduler.UnobservedTaskException += (_, e) => Options.Say($"error in a background task: {TerrainEditor.App.Log.Scrub(e.Exception.ToString())}");
        // Logging out, shutting down or `kill` (SIGTERM, SIGHUP): the tunnel closes,
        // then the app ends (changes not saved are lost, as when the computer turns off).
        using var term = System.Runtime.InteropServices.PosixSignalRegistration.Create(System.Runtime.InteropServices.PosixSignal.SIGTERM, Quit);
        using var hup = System.Runtime.InteropServices.PosixSignalRegistration.Create(System.Runtime.InteropServices.PosixSignal.SIGHUP, Quit);
        // VWE_TRACE=1: Avalonia's warnings on the console.
        if (Environment.GetEnvironmentVariable("VWE_TRACE") == "1")
        {
            System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
        }
        var settings = TerrainEditor.App.AppSettings.Load();
        // The language the windows are built in (Settings; the computer's by default).
        Options.Say($"language: {TerrainEditor.App.Lang.Use(settings.Language)}");
        // How many steps the history keeps (Settings).
        EditSession.StepLimit = settings.HistorySteps;
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace(Avalonia.Logging.LogEventLevel.Warning)
            .With(new Win32PlatformOptions { RenderingMode = WindowsRendering(settings.DriverOpenGl) })
            // Linux: Avalonia turns OpenGL off for software renderers (Mesa's llvmpipe, VMware's
            // SVGA3D), where its own drawing is faster without it; the 3D view and the map need
            // OpenGL, so they were blank in virtual machines and without GPU drivers.
            .With(new X11PlatformOptions { GlxRendererBlacklist = [] })
            // macOS: OpenGL (4.1, core), which the 3D view and the map draw with; Metal would leave them blank.
            .With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software] })
            .StartWithClassicDesktopLifetime(args);
        // History files Apply live queued are written before the process ends (its background threads stop with it).
        HistoryFile.Flush();
    }

    /// <summary>
    /// SIGTERM or SIGHUP: closes the tunnel, then shuts the app down on the UI thread (instead of the process ending at
    /// once).
    /// </summary>
    internal static void Quit(System.Runtime.InteropServices.PosixSignalContext c)
    {
        c.Cancel = true;
        Options.Say($"quit: {c.Signal}");
        TerrainEditor.App.Tunnel.Close();
        Avalonia.Threading.Dispatcher.UIThread.Post(() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown());
    }

    /// <summary>
    /// How Windows draws, in the order tried: ANGLE (OpenGL ES on Direct3D 11) first, then the graphics driver's own
    /// OpenGL (WGL), drawing in software last. Old or laptop drivers' OpenGL showed old frames (objects coming and going)
    /// and black lines across the window (issue #115); Direct3D drivers are the ones games use. Settings' "Draw with the
    /// graphics driver's OpenGL" puts WGL first again, for a driver whose Direct3D draws worse.
    /// </summary>
    /// <param name="driverOpenGl">Whether the driver's OpenGL is tried first.</param>
    /// <returns>The modes, the first that works taken.</returns>
    internal static Win32RenderingMode[] WindowsRendering(bool driverOpenGl) => driverOpenGl
        ? [Win32RenderingMode.Wgl, Win32RenderingMode.AngleEgl, Win32RenderingMode.Software]
        : [Win32RenderingMode.AngleEgl, Win32RenderingMode.Wgl, Win32RenderingMode.Software];
}

