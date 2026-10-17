
namespace TerrainEditor.Desktop;

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
///   --script-host                                      no window: runs one script for the editor that started
///                                                      this process (see ScriptHost.Serve)
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
                    try
                    {
                        Directory.CreateDirectory(Data);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // Said like any other option that cannot be used, before the editor starts.
                        throw new ArgumentException($"--data {Data} cannot be used: {ex.Message}", ex);
                    }
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
