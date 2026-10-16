using System.Text.Json;
using System.Text.Json.Serialization;

namespace TerrainEditor.App;

/// <summary>
/// What the app remembers between runs, per user: settings.json in the app's data folder
/// (~/.local/share/ValheimWorldEditor on Linux, %LOCALAPPDATA%\ValheimWorldEditor on Windows).
/// </summary>
public sealed class AppSettings
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
    /// Windows: draw with the graphics driver's own OpenGL first instead of Direct3D (ANGLE); taken when the editor
    /// starts.
    /// </summary>
    public bool DriverOpenGl { get; set; }

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
    /// The user's own data folder (created if missing): LocalApplicationData/ValheimWorldEditor, or ~/.local/share when
    /// that is not set.
    /// </summary>
    public static string UserDataDir
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
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
    /// Whether there was no settings file when these were loaded: a file there at the next <c>Save</c> was put there
    /// by another program since (a sync program putting the user's back), and is left as it is. Cleared once these
    /// are written.
    /// </summary>
    private bool _fileMissing;

    /// <summary>
    /// Reads the settings file. A missing file gives the defaults; one that is not valid JSON is first copied aside as
    /// settings.json.bad; one that cannot be read gives the defaults with <c>FileUnread</c> set, which are not saved
    /// over it.
    /// </summary>
    public static AppSettings Load()
    {
        lock (Lock)
        {
            string? text;
            try
            {
                text = ReadText();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Settings: {FilePath} could not be read ({ex.Message}); using the defaults for now, and not saving over it.");
                return new AppSettings { FileUnread = true };
            }
            if (text == null)
            {
                return new AppSettings { _fileMissing = true };
            }
            try
            {
                return JsonSerializer.Deserialize<AppSettings>(text) ?? new AppSettings();
            }
            catch (JsonException ex)
            {
                // Not readable: kept aside as settings.json.bad (the next save would write over it).
                Console.WriteLine($"Settings: {FilePath} could not be read ({ex.Message}); kept as {FilePath}.bad, starting from the defaults.");
                try
                {
                    File.Copy(FilePath, FilePath + ".bad", overwrite: true);
                }
                catch (Exception copy) when (copy is IOException or UnauthorizedAccessException)
                {
                    Console.WriteLine($"Settings: {FilePath} could not be kept as {FilePath}.bad: {copy.Message}");
                }
                return new AppSettings();
            }
        }
    }

    /// <summary>
    /// The settings file's text, or null when there is none. A read that fails (the file held by a sync or antivirus
    /// program for a moment) is tried again twice, 100 ms apart, before its exception goes up; a file not there is
    /// looked for once more (one being put back by such a program), and none the second time is none (no data
    /// folder: none at once).
    /// </summary>
    private static string? ReadText()
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return File.ReadAllText(FilePath);
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            catch (FileNotFoundException) when (attempt > 0)
            {
                return null;
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(100);
            }
        }
    }

    /// <summary>
    /// JSON options for writing the settings file, indented so it stays readable by hand.
    /// </summary>
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>
    /// A file that cannot be written (locked, read-only) is left as it was: the settings stay in memory. So is one
    /// that could not be read when these were loaded (<c>FileUnread</c>), or that was not there then and is now
    /// (another program put it back): the user's settings are in it.
    /// </summary>
    public void Save()
    {
        lock (Lock)
        {
            if (FileUnread)
            {
                Console.WriteLine($"Settings: not saved: {FilePath} could not be read when the editor started, and is left as it is.");
                return;
            }
            if (_fileMissing && File.Exists(FilePath))
            {
                Console.WriteLine($"Settings: not saved: {FilePath} was not there when the editor started and is now (put back by another program?), and is left as it is.");
                return;
            }
            try
            {
                // Only the user can read it, from the start: it holds Claude's token.
                SafeFile.WriteAllText(FilePath, JsonSerializer.Serialize(this, Indented), UnixFileMode.UserRead | UnixFileMode.UserWrite);
                _fileMissing = false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Settings: could not write {FilePath}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Puts a world at the top of the recent list (dropping an older entry for the same full path), keeps the 12 newest
    /// and saves.
    /// </summary>
    /// <param name="path">The world's path.</param>
    /// <param name="name">The name to show for it.</param>
    public void AddRecent(string path, string name)
    {
        Recent.RemoveAll(r => string.Equals(Path.GetFullPath(r.Path), Path.GetFullPath(path), StringComparison.Ordinal));
        Recent.Insert(0, new RecentWorld(path, name, DateTime.Now));
        if (Recent.Count > 12)
        {
            Recent.RemoveRange(12, Recent.Count - 12);
        }
        Save();
    }
}
