using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace TerrainEditor.Desktop;

/// <summary>
/// The native editor: an Avalonia window, the 3D view and the map drawn with OpenGL. It opens on the start page; the
/// options (see Options) open an area directly or let a program drive it.
/// </summary>
public static class Program
{
    /// <summary>
    /// Starts the editor, or with <c>--mcp-stdio</c>, <c>--export-test-data</c> or <c>--export-locations</c> does only
    /// that and ends.
    /// </summary>
    /// <param name="args">The command line (see <see cref="Options"/>).</param>
    [STAThread]
    public static void Main(string[] args)
    {
        // --mcp-stdio: only the relay to the running editor for Claude (nothing else on stdout: no log,
        // no window).
        if (args.Contains("--mcp-stdio"))
        {
            ClaudeRelay.Run();
            return;
        }
        // --export-test-data <folder>: the tests' copies of the game's data made again, each keeping the
        // entries it has (for developers, after a game update: tests/fixtures).
        int exportData = Array.IndexOf(args, "--export-test-data");
        if (exportData >= 0)
        {
            Environment.ExitCode = TerrainEditor.App.GameData.Export(args.ElementAtOrDefault(exportData + 1));
            return;
        }
        // --export-locations <file>: the tests' copy of the game's location rules made again (for
        // developers, after a game update: tests/fixtures/locations.json).
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
            Console.Error.WriteLine($"ValheimWorldEditor: {e.Message}");
            Environment.ExitCode = 2;
            return;
        }
        // Everything said also goes to ValheimWorldEditor.log in the data folder (a window app has no console on Windows).
        TerrainEditor.App.Log.Start(BuildInfo.Version);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Options.Say($"crash: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) => Options.Say($"error in a background task: {e.Exception}");
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
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace(Avalonia.Logging.LogEventLevel.Warning)
            .With(new Win32PlatformOptions { RenderingMode = WindowsRendering(settings.DriverOpenGl) })
            // Linux: Avalonia turns OpenGL off for software renderers (Mesa's llvmpipe, VMware's
            // SVGA3D), where its own drawing is faster without it; the 3D view and the map need
            // OpenGL, so they were blank in virtual machines and without GPU drivers.
            .With(new X11PlatformOptions { GlxRendererBlacklist = [] })
            .StartWithClassicDesktopLifetime(args);
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

/// <summary>The Avalonia application: the Fluent theme in dark with the editor's styles, and the main window.</summary>
public sealed class App : Application
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Ui.Apply(this);
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    /// <summary>
    /// Opens the main window, and catches errors in the UI thread's handlers: logged and shown instead of closing the
    /// app and losing what is not saved.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            // An error in a button's or a key's handler would close the app and lose what is not saved:
            // it is logged and shown instead.
            Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                Options.Say($"error: {e.Exception}");
                e.Handled = true;
                window.ShowError(e.Exception);
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>
/// Command line options.
/// <code>
///   &lt;world folder&gt;                                     that world's map
///   --live &lt;bridge url&gt; --token &lt;token&gt;                a live game's map (the token also from
///                                                      WORLD_BRIDGE_TOKEN)
///   --world &lt;folder or name&gt; [--zone x,z] [--size n]   straight into the 3D editor with that area
///   --driver                                           driven by another program (see Driver)
///   --window &lt;width&gt;x&lt;height&gt;                          the window's size (the documentation's pictures)
///   --data &lt;folder&gt;                                    settings and memory kept there (tests)
///   --gamedata &lt;folder&gt;                                the game's data from the tests' copies (tests/fixtures):
///                                                      where there is no Valheim (CI), so that worlds open
///   --mcp-stdio                                        no window: relays Claude's MCP messages on stdin/stdout to
///                                                      the running editor (Claude Desktop; see ClaudeRelay)
/// </code>
/// </summary>
public static class Options
{
    /// <summary>--world: the world (folder or name) to open straight in the 3D editor.</summary>
    public static string? World { get; private set; }
    /// <summary>--world or --zone given: straight into the 3D editor, not the start page.</summary>
    public static bool Direct { get; private set; }
    /// <summary>--zone: the x of the zone to open the area at.</summary>
    public static int ZoneX { get; private set; }
    /// <summary>--zone: the z of the zone to open the area at.</summary>
    public static int ZoneZ { get; private set; }
    /// <summary>--size: the area, in zones each way (1 to 9; 5 by default).</summary>
    public static int Size { get; private set; } = 5;
    /// <summary>Driven by another program, one command per line (see Driver).</summary>
    public static bool Driver { get; private set; }
    /// <summary>
    /// --data &lt;folder&gt;: settings, saved servers, Place tool memory and stamps go there instead of the user's (the
    /// visual tests). The game's look is still read from the usual place.
    /// </summary>
    public static string? Data { get; private set; }

    /// <summary>&lt;world folder&gt;: its map, as if picked on the start page.</summary>
    public static string? Folder { get; private set; }
    /// <summary>--live: the bridge's URL of a game's world, as if connected on the start page.</summary>
    public static string? LiveUrl { get; private set; }
    /// <summary>--token: the bridge's token (else WORLD_BRIDGE_TOKEN).</summary>
    public static string? LiveToken { get; private set; }

    /// <summary>--window: the window's width (0: the usual).</summary>
    public static int WindowWidth { get; private set; }
    /// <summary>--window: the window's height (0: the usual).</summary>
    public static int WindowHeight { get; private set; }

    /// <summary>Writes a line on the console (the log takes it too once started).</summary>
    public static void Say(string line) => Console.WriteLine(line);

    /// <summary>
    /// The value after the option at <paramref name="i"/> (moved past it); an option given last, without its value, is a
    /// command-line error.
    /// </summary>
    private static string Value(string[] args, ref int i) =>
        ++i < args.Length ? args[i] : throw new ArgumentException($"{args[i - 1]} needs a value after it.");

    /// <summary>
    /// Reads the command line, starting from the defaults each time (the tests parse several in one process). --data
    /// also points the settings, servers, Place memory and stamps there.
    /// </summary>
    public static void Parse(string[] args)
    {
        // Each start from nothing (the tests parse several lines in one process).
        Folder = LiveUrl = World = Data = null;
        Direct = Driver = false;
        ZoneX = ZoneZ = 0;
        Size = 5;
        WindowWidth = WindowHeight = 0;
        LiveToken = Environment.GetEnvironmentVariable("WORLD_BRIDGE_TOKEN");
        // Every argument, the last too (a switch such as --driver or --map-back can come last).
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--world":
                    World = Value(args, ref i);
                    Direct = true;
                    break;
                case "--driver":
                    Driver = true;
                    // Driven by the tests or the documentation: never the computer's own game outside the home.
                    TerrainEditor.App.GameLook.SearchOutsideHome = false;
                    break;
                case "--data":
                    Data = Path.GetFullPath(Value(args, ref i));
                    Directory.CreateDirectory(Data);
                    TerrainEditor.App.AppSettings.PathOverride = Path.Combine(Data, "settings.json");
                    TerrainEditor.App.ServerConfig.PathOverride = Path.Combine(Data, "servers.cfg");
                    PlaceMemory.PathOverride = Path.Combine(Data, "place.json");
                    Stamps.PathOverride = Path.Combine(Data, "stamps.json");
                    TerrainEditor.App.AppSettings.DataDirOverride = Data;
                    // The tests' editor: "My game" never finds the player's own profiles.
                    TerrainEditor.App.LocalGame.SearchDefaultPlaces = false;
                    break;
                case "--gamedata":
                {
                    // As the driver's gamedata, but before a world given on the command line opens.
                    string folder = Path.GetFullPath(Value(args, ref i));
                    try
                    {
                        TerrainEditor.App.GameData.UseCopies(folder);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
                    {
                        // A folder missing, or one of its files: a command-line error, not a crash.
                        throw new ArgumentException($"--gamedata {folder} cannot be read: {e.Message}", e);
                    }
                    break;
                }
                case "--zone":
                    Direct = true;
                    string[] p = Value(args, ref i).Split(',');
                    ZoneX = int.Parse(p[0]);
                    ZoneZ = int.Parse(p[1]);
                    break;
                case "--size":
                    Size = Math.Clamp(int.Parse(Value(args, ref i)), 1, 9);
                    break;
                case "--window":
                {
                    string[] wh = Value(args, ref i).Split('x');
                    WindowWidth = Math.Clamp(int.Parse(wh[0]), 640, 7680);
                    WindowHeight = Math.Clamp(int.Parse(wh[1]), 480, 4320);
                    break;
                }
                case "--live":
                    LiveUrl = Value(args, ref i);
                    break;
                case "--token":
                    LiveToken = Value(args, ref i);
                    break;
                default:
                    if (!args[i].StartsWith("--"))
                    {
                        Folder ??= args[i];
                    }
                    break;
            }
        }
    }
}
