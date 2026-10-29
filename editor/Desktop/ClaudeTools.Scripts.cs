using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's scripts (run_script) and the checks before a change: the user's leave, and the open area's state.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>
    /// Why Claude cannot change the open area now, or null: nothing open, or live with Auto on (each change
    /// would go to the game at once, and Claude's must wait for the user).
    /// </summary>
    private string? CannotEdit()
    {
        if (_w.Session == null)
        {
            return "No area is open: open_area (a world) or open_workshop first.";
        }
        if (LiveWithAuto)
        {
            return "The open world is live with Auto on: every change would go to the game at once. Ask the user to turn Auto off; Claude's changes then wait for Apply live.";
        }
        return null;
    }

    /// <summary>Whether the open world is live with Auto on: every change would go to the game at once.</summary>
    private bool LiveWithAuto => _w.World is { IsLive: true } && _w.Settings.AutoApply;

    /// <summary>Why Claude may not run a script now: the user has not allowed it (Settings); null when it may.</summary>
    internal static string ScriptsRefused => "The user has not allowed Claude to run scripts (Settings → Claude → Claude may run scripts).";

    /// <summary>What Claude is told when the user, shown its script, refused it.</summary>
    internal static string ScriptRefusedByUser => "The user was shown this script and refused it: nothing ran. Do not run it again unless the user asks.";

    /// <summary>run_script: runs a C# script as the Script tool does, as one pending step (or only says what it would change).</summary>
    [McpServerTool(Name = "run_script", Title = "Run a script on the area")]
    [Description("Runs a C# script on the open area (or the Workshop's plot), as the editor's Script tool does: the script's statements are the body of a program using the API script_reference gives (Area, Ground, Objects, Noise, Rnd, Print). What it changes (ground heights, paint, objects placed or taken away) goes in as one step of the history, pending until the user saves; nothing changes when it has mistakes or fails (the errors say which line). Returns what it printed and what it changed. Ground goes past the game's ±8 m from the original, as with the Script tool (Ground.NoLimit is true; saving turns that into ground discs, up to ±400 m): set Ground.NoLimit = false to keep within it. Needs the user's leave, given once in Settings → Claude → Claude may run scripts (off by default: a script has the user's rights on their computer); refused otherwise, dry runs too. The user is then shown each script and runs or refuses it (the same code is asked once: a dry run, then the run): keep scripts short and readable.")]
    public Task<string> RunScript(
        [Description("The script: C# statements, e.g. \"Ground.Mountain(Area.CenterX, Area.CenterZ, height: 30, radius: 50);\"")] string code,
        [Description("A few words for the history (\"Claude: river valley\").")] string label = "Claude: script",
        [Description("true: only say what it would change (ground points, paint, objects placed and taken away), changing nothing. Use it before a large change.")] bool dryRun = false) => OnUi(async () =>
    {
        // A dry run compiles and runs the code too: the same leave. Said first: the user's own setting, which no
        // other change (opening an area, turning Auto off) would get round.
        if (!_w.Settings.ClaudeScripts)
        {
            return ScriptsRefused;
        }
        if (CannotEdit() is string why)
        {
            return why;
        }
        // Shown to the user first, each script (the same code once): the user's leave alone would let a script talked
        // into by text in the world run unseen.
        if (!await _w.ClaudeMayRun(code))
        {
            return ScriptRefusedByUser;
        }
        var r = await _w.RunScriptCode(code, label.StartsWith("Claude", StringComparison.Ordinal) ? label : ClaudesStep(label), apply: !dryRun);
        _w.Msg(r.Message);
        return r.Output.English;
    });

    /// <summary>
    /// A history label of Claude's: the "Claude: " mark, which stays the same in every language since Take back
    /// Claude's changes finds Claude's steps by it (HistoryPanel.ByClaude), then what the step did.
    /// </summary>
    /// <param name="text">What the step did: in the user's language, or the words Claude gave.</param>
    internal static string ClaudesStep(string text) => "Claude: " + text;

    /// <summary>Runs a script written by a tool as one pending step labelled "Claude: label".</summary>
    /// <param name="code">The script.</param>
    /// <param name="label">What the step did, in the user's language (ClaudesStep puts the mark before it).</param>
    private Task<string> RunGenerated(string code, string label) => OnUi(async () =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        // The game's ±8 m holds, as these tools say (a script of Claude's own goes past it unless it says otherwise).
        var r = await _w.RunScriptCode("Ground.NoLimit = false;\n" + code, ClaudesStep(label));
        _w.Msg(r.Message);
        return r.Output.English;
    });
}
