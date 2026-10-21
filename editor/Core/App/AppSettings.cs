using System.Text.Json.Serialization;

namespace TerrainEditor.App;

/// <summary>
/// What the app remembers between runs, per user: settings.json in the app's data folder
/// (~/.local/share/ValheimWorldEditor on Linux, %LOCALAPPDATA%\ValheimWorldEditor on Windows).
/// </summary>
public sealed partial class AppSettings
{
    /// <summary>
    /// The Valheim game folder chosen in Settings (null: found automatically in the Steam libraries).
    /// </summary>
    public string? ValheimPath { get; set; }

    /// <summary>
    /// Extra places to look for the game's BepInEx: a BepInEx folder, a mod manager profile, or a
    /// folder of profiles (a mod manager with a custom data folder).
    /// </summary>
    public List<string> BepInExFolders { get; set; } = [];

    /// <summary>The add-ons turned off in Settings, by their folder's name (AddOns): installed but not used.</summary>
    public List<string> AddOnsOff { get; set; } = [];

    /// <summary>
    /// Extra places with worlds, always listed: a world folder, or a folder of worlds (a server's
    /// -savedir/worlds_local, a backup folder...).
    /// </summary>
    public List<string> WorldFolders { get; set; } = [];

    /// <summary>
    /// The mods' editor extensions allowed to run (ModExtensions): each DLL's full path and the SHA-256 of the file as
    /// it was allowed. A changed file runs only once allowed again: it is other code.
    /// </summary>
    public Dictionary<string, string> Extensions { get; set; } = [];

    /// <summary>
    /// The worlds opened lately, newest first, at most 12 (see <c>AddRecent</c>).
    /// </summary>
    public List<RecentWorld> Recent { get; set; } = [];

    /// <summary>
    /// The bridge address last connected to by URL from the start page, with its scheme (http:// added when it was
    /// typed without one).
    /// </summary>
    public string? LiveUrl { get; set; }

    /// <summary>
    /// The language the app shows (Settings): a catalogue's code ("en", "fr"…, see <c>Lang</c>); null: the computer's
    /// language when there is a catalogue of it, else English. Taken when the app starts.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Which start-page choice was used last ("game", "server", "offline").
    /// </summary>
    public string? LastMode { get; set; }

    /// <summary>
    /// The 3D editor's live "Auto" switch (every change sent to the game at once), kept between runs like the web
    /// editor's.
    /// </summary>
    public bool AutoApply { get; set; }

    /// <summary>
    /// The 3D editor's Follow switch (the area moves by itself when the view nears its edge), kept between runs.
    /// </summary>
    public bool AreaFollow { get; set; }

    /// <summary>
    /// Claude's connection (Settings): the editor's MCP server, on 127.0.0.1 at <c>ClaudePort</c>, for callers that
    /// send <c>ClaudeToken</c>. Off unless chosen.
    /// </summary>
    public bool ClaudeConnect { get; set; }

    /// <summary>
    /// Whether Claude may run scripts (run_script, the Script tool's Run): a script has the editor's rights on this
    /// computer, like any program the user runs, so it takes the user's leave once (Settings). Off unless chosen.
    /// </summary>
    public bool ClaudeScripts { get; set; }

    /// <summary>
    /// Windows: draw with the graphics driver's own OpenGL first instead of Direct3D (ANGLE); taken when the editor
    /// starts.
    /// </summary>
    public bool DriverOpenGl { get; set; }

    /// <summary>
    /// Expand World (Settings): worlds are generated as the Expand World mods make them, from the settings the world
    /// file keeps and the mods' files in the BepInEx folder. Off unless chosen: every world is generated as the
    /// game itself does, whatever the world was made with.
    /// </summary>
    public bool ExpandWorld { get; set; }

    /// <summary>
    /// The BepInEx folder Expand World's settings and data files are read from (Settings), looked at before the
    /// folders the editor knows; null or empty for none (the first folder that has the mods is used).
    /// </summary>
    public string? ExpandWorldFolder { get; set; }

    /// <summary>
    /// How many steps the editor's history keeps (Settings); 0 or less: no limit (only a memory budget).
    /// </summary>
    public int HistorySteps { get; set; }

    /// <summary>
    /// The local port the MCP server for Claude listens on.
    /// </summary>
    public int ClaudePort { get; set; } = 5731;

    /// <summary>
    /// The token callers of the MCP server for Claude must send.
    /// </summary>
    public string? ClaudeToken { get; set; }

    /// <summary>
    /// A world in the recent list.
    /// </summary>
    /// <param name="Path">The world's folder.</param>
    /// <param name="Name">The world's name as shown.</param>
    /// <param name="Opened">When it was last opened (local time).</param>
    public sealed record RecentWorld(string Path, string Name, DateTime Opened);

    /// <summary>
    /// The data folder: settings, servers, blueprints, the log (DataDirOverride: another folder, for
    /// the tests' editor). The copied game files stay in the user's (UserDataDir), read only by tests.
    /// </summary>
    public static string DataDir
    {
        get
        {
            if (DataDirOverride is string o)
            {
                Directory.CreateDirectory(o);
                return o;
            }
            return UserDataDir;
        }
    }

    /// <summary>
    /// Another data folder instead of <c>UserDataDir</c> (the tests' editor, set by the --data option); created when
    /// read.
    /// </summary>
    public static string? DataDirOverride { get; set; }

    /// <summary>
    /// The user's own data folder (created if missing): %LOCALAPPDATA%\ValheimWorldEditor on Windows (the variable, as
    /// the tests' pretend homes set it; .NET's LocalApplicationData comes from the shell and ignores it), else
    /// LocalApplicationData/ValheimWorldEditor, or ~/.local/share when that is not set.
    /// </summary>
    public static string UserDataDir
    {
        get
        {
            string root = OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("LOCALAPPDATA") is { Length: > 0 } local
                ? local
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            if (string.IsNullOrEmpty(root))
            {
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            }
            string dir = Path.Combine(root, "ValheimWorldEditor");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// Tests: another file, so they never touch the user's settings.
    /// </summary>
    public static string? PathOverride { get; set; }

    /// <summary>
    /// settings.json in <c>DataDir</c>, or <c>PathOverride</c>.
    /// </summary>
    private static string FilePath => PathOverride ?? Path.Combine(DataDir, "settings.json");

    /// <summary>
    /// Serialises reads and writes of the settings file.
    /// </summary>
    private static readonly Lock Lock = new();

    /// <summary>
    /// Whether the settings file was there but could not be read (locked by another program, no permission): these
    /// are the defaults for now, and <c>Save</c> leaves the file as it is rather than writing them over the user's
    /// settings.
    /// </summary>
    [JsonIgnore]
    public bool FileUnread { get; private init; }

    /// <summary>
    /// Whether there was no settings file when these were loaded: a file there when <c>Save</c> writes was put there
    /// by another program since (a sync program putting the user's back), and is left as it is. Cleared once these
    /// are written.
    /// </summary>
    private bool _fileMissing;

    /// <summary>
    /// These settings with lists the file gave as null ("Recent": null), or entries of them null, made empty or left
    /// out: the rest of the app reads them as lists.
    /// </summary>
    private AppSettings WithoutNulls()
    {
        BepInExFolders = [.. (BepInExFolders ?? []).Where(f => f != null)];
        WorldFolders = [.. (WorldFolders ?? []).Where(f => f != null)];
        AddOnsOff = [.. (AddOnsOff ?? []).Where(f => f != null)];
        Extensions ??= [];
        Recent = [.. (Recent ?? []).Where(r => r is { Path: not null, Name: not null })];
        return this;
    }

    /// <summary>
    /// Puts a world at the top of the recent list (dropping an older entry for the same folder: its full path, without
    /// a separator at the end, case ignored on Windows), keeps the 12 newest and saves.
    /// </summary>
    /// <param name="path">The world's path.</param>
    /// <param name="name">The name to show for it.</param>
    public void AddRecent(string path, string name)
    {
        string full = FullPath(path);
        Recent.RemoveAll(r => string.Equals(FullPath(r.Path), full, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        Recent.Insert(0, new RecentWorld(path, name, DateTime.Now));
        if (Recent.Count > 12)
        {
            Recent.RemoveRange(12, Recent.Count - 12);
        }
        Save();
    }

    /// <summary>A path as its full path without a separator at the end; as it is when it is not a valid path.</summary>
    private static string FullPath(string path)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Log.CaughtBrief(ex, "making a recent world's path full (compared as it is)");
            return path;
        }
    }
}
