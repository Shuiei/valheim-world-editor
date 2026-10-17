
namespace TerrainEditor.Desktop;

/// <summary>
/// --driver: the editor driven by another program (the visual tests), one command per line on its
/// input, one answer per line on its output ("@@ ok &lt;json&gt;" or "@@ error &lt;message&gt;"), so a single
/// editor serves many tests. Commands:
/// </summary>
/// <remarks>
/// <code>
///    gamedata &lt;folder&gt;              the game's data from the tests' copies (tests/fixtures): where
///                                   there is no Valheim (CI), so that worlds open
///    world &lt;folder&gt;                 open a world (its map)
///    area &lt;zx&gt; &lt;zz&gt; &lt;size&gt;          open an area in the 3D editor
///    map                            back to the map
///    stroke &lt;tool&gt; &lt;x&gt; &lt;z&gt; &lt;steps&gt;  a brush stroke at world x, z (tool: raise, lower, smooth...)
///    picture &lt;file.png&gt;             a picture of the view shown (3D or map), once it is drawn
///    shot &lt;file.png&gt;                a picture of the whole window, panels and view (documentation)
///    look &lt;game|seethrough&gt; &lt;on|off&gt;, look res &lt;sharp|balanced|fast&gt;
///                                   the View panel's Look switches
///    camera &lt;x&gt; &lt;z&gt; &lt;yaw°&gt; &lt;pitch°&gt; &lt;distance&gt; [y]  the 3D view's camera on world x, z (at height y:
///                                               inside a dungeon)
///    click &lt;text&gt;                   the visible button, switch or box labelled so, or whose words
///                                   begin so (windows and dialogs)
///    choose &lt;text&gt;                  the entry so named in whichever visible list has it
///    type &lt;hint&gt;|&lt;text&gt;             the visible text box whose placeholder contains hint gets text
///    set &lt;label&gt;|&lt;value&gt;            the slider, text box(es: a,b) or list beside the label so worded
///    panel &lt;view|history|help|none&gt; the right-hand panel shown
///    message [text]                 the status bar's message (none: cleared)
///    wait &lt;ms&gt;                      a pause (a brush stroke works frame by frame while held)
///    bench &lt;seconds&gt;                the 3D camera turns on its own; the frame rates come back
///    mouse &lt;down|move|up&gt; &lt;x&gt; &lt;z&gt; [left|right|middle] [shift|ctrl|alt ...]
///                                   the mouse at world x, z over the 3D view (real pointer events)
///    wheel &lt;x&gt; &lt;z&gt; &lt;steps&gt;          the wheel there (positive: zoom in)
///    key &lt;name&gt; [shift|ctrl|alt ...] a key pressed in the window (Avalonia key names: E, Escape, Enter, D1...)
///    mapview &lt;x&gt; &lt;z&gt; &lt;m per pixel&gt;  the map looking there
///    mappick &lt;zone x&gt; &lt;zone z&gt;      the map's zone picked (as a click on it)
///    search &lt;text&gt;                  the map's search (objects by kind)
///    hit &lt;n&gt;                        the search's result n (from 0) picked, as a click in its list
///    zones                          the map's zone filter: show the matching zones
///    workshop [file.blueprint]      the Workshop opened (on that blueprint); how long it took
///    state                          what is shown, the objects, what is pending, frames drawn
///    quit
/// </code>
/// Also spirv (SPIRV-Cross loads), mouse3 (a point in the air), inspect &lt;prefab&gt; and check &lt;label&gt;|on|off.
/// </remarks>
public static partial class Driver
{
    /// <summary>
    /// How long a picture may wait for the view to be ready: the world map's first build on a slow computer (CI's
    /// two cores) takes more than a minute. VWE_PICTURE_WAIT (seconds) gives longer: the tests under coverage, which
    /// measures this app too.
    /// </summary>
    private static readonly TimeSpan PictureWait = TimeSpan.FromSeconds(
        int.TryParse(Environment.GetEnvironmentVariable("VWE_PICTURE_WAIT"), out int s) && s > 0 ? s : 180);

    /// <summary>The View panel's 3D resolution choices, in their list's order.</summary>
    private static readonly string[] Resolutions = ["sharp", "balanced", "fast"];

    /// <summary>The handler of one command: the window and the command's words and arguments.</summary>
    private delegate Task<string> Handler(Call call);

    /// <summary>
    /// One command as given: the window, the command's words (the first is the command) and its arguments.
    /// </summary>
    internal sealed record Call(MainWindow W, string[] A, string[] Args)
    {
        /// <summary>The argument at this position as a number (invariant culture).</summary>
        public float F(int i) => float.Parse(Args[i], System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Each command's handler, by its name (the table's order follows the class summary).</summary>
    private static readonly Dictionary<string, Handler> Commands = new()
    {
        ["spirv"] = CmdSpirv,
        ["gamedata"] = CmdGamedata,
        ["world"] = CmdWorld,
        ["area"] = CmdArea,
        ["map"] = CmdMap,
        ["stroke"] = CmdStroke,
        ["mouse"] = CmdMouse,
        ["mouse3"] = CmdMouse3,
        ["wheel"] = CmdWheel,
        ["key"] = CmdKey,
        ["mapview"] = CmdMapview,
        ["mappick"] = CmdMappick,
        ["search"] = CmdSearch,
        ["hit"] = CmdHit,
        ["zones"] = CmdZones,
        ["picture"] = CmdPicture,
        ["shot"] = CmdShot,
        ["look"] = CmdLook,
        ["type"] = CmdType,
        ["set"] = CmdSet,
        ["panel"] = CmdPanel,
        ["message"] = CmdMessage,
        ["wait"] = CmdWait,
        ["inspect"] = CmdInspect,
        ["camera"] = CmdCamera,
        ["click"] = CmdClick,
        ["check"] = CmdCheck,
        ["choose"] = CmdChoose,
        ["bench"] = CmdBench,
        ["workshop"] = CmdWorkshop,
        ["bounds"] = CmdBounds,
        ["state"] = CmdState,
        ["quit"] = CmdState,
    };

    /// <summary>Runs one command; its answer's JSON (usually <see cref="State"/>). Throws for an unknown or failed command.</summary>
    internal static async Task<string> Run(MainWindow w, string cmd)
    {
        string[] a = cmd.Split(' ', 2);
        string[] args = a.Length > 1 ? a[1].Split(' ') : [];
        if (!Commands.TryGetValue(a[0], out var handler))
        {
            throw new InvalidOperationException("unknown command " + a[0]);
        }
        return await handler(new Call(w, a, args));
    }

    /// <summary>A command's argument split at its first '|' (type, set, check).</summary>
    /// <param name="a">The command and its argument.</param>
    /// <param name="usage">How the command is written, for the message.</param>
    /// <exception cref="InvalidOperationException">There is no argument or it has no '|'.</exception>
    internal static (string Before, string After) Bar(string[] a, string usage)
    {
        int bar = a.Length > 1 ? a[1].IndexOf('|') : -1;
        return bar >= 0 ? (a[1][..bar], a[1][(bar + 1)..]) : throw new InvalidOperationException("usage: " + usage);
    }
}
