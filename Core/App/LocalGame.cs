using System.Text.RegularExpressions;

namespace TerrainEditor.App;

/// <summary>
/// Live editing of the player's own game (single player or hosting): finds the WorldEditorBridge
/// plugin's settings in the Valheim install or a mod manager profile, and checks whether the game
/// is running with it, so the start page can offer one click (the token goes only to the plugin, on
/// 127.0.0.1; the player never has to copy it).
/// </summary>
public static partial class LocalGame
{
    /// <summary>An installed bridge plugin's settings.</summary>
    /// <param name="ConfigPath">Its .cfg file.</param>
    /// <param name="Where">Where it is installed, for the player ("Valheim folder", "r2modman profile "x""...).</param>
    /// <param name="Port">The local port it listens on (5182 when the file does not say).</param>
    /// <param name="Token">The secret the bridge wants in X-Bridge-Token.</param>
    public sealed record Bridge(string ConfigPath, string Where, int Port, string Token);

    /// <summary>A bridge that answered: the game is running with it.</summary>
    /// <param name="Bridge">The bridge that answered.</param>
    /// <param name="World">The world loaded, empty when it did not say.</param>
    /// <param name="Players">How many players are on it.</param>
    public sealed record Running(Bridge Bridge, string World, int Players);

    /// <summary>The plugin's settings file in BepInEx/config, and its name before 0.41.0 (an older plugin).</summary>
    public const string ConfigName = "Tie.WorldEditorBridge.cfg", OldConfigName = "local.worldeditorbridge.cfg";

    /// <summary>Tests: only the folders of the settings given, never the player's own install or profiles.</summary>
    public static bool SearchDefaultPlaces { get; set; } = true;

    /// <summary>
    /// The bridge settings found in every BepInEx folder (the current file name first, else the old one).
    /// </summary>
    /// <param name="settings">The editor's settings (Valheim path, added BepInEx folders).</param>
    /// <param name="bepInEx">Whether any of the folders has BepInEx installed (core or plugins).</param>
    /// <param name="plugin">Whether any has WorldEditorBridge.dll in its plugins.</param>
    /// <exception cref="IOException">A settings file could not be read (being written, say): asked again later.</exception>
    /// <exception cref="UnauthorizedAccessException">A settings file could not be read (no access).</exception>
    public static List<Bridge> FindBridges(AppSettings settings, out bool bepInEx, out bool plugin)
    {
        bepInEx = false;
        plugin = false;
        var list = new List<Bridge>();
        foreach (var (dir, where) in BepInExFolders(settings))
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }
            bepInEx |= Directory.Exists(Path.Combine(dir, "core")) || Directory.Exists(Path.Combine(dir, "plugins"));
            string plugins = Path.Combine(dir, "plugins");
            plugin |= Directory.Exists(plugins) && HasPlugin(plugins);
            string cfg = Path.Combine(dir, "config", ConfigName);
            if (!File.Exists(cfg))
            {
                cfg = Path.Combine(dir, "config", OldConfigName);
            }
            if (File.Exists(cfg) && Parse(cfg, where) is Bridge b)
            {
                list.Add(b);
            }
        }
        return list;
    }

    /// <summary>
    /// Whether WorldEditorBridge.dll is somewhere in a plugins folder. Subfolders that cannot be read are passed over
    /// (another mod's, without access); a folder that cannot be searched at all is said in the log and has none.
    /// </summary>
    private static bool HasPlugin(string plugins)
    {
        try
        {
            return Directory.EnumerateFiles(plugins, "WorldEditorBridge.dll", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 }).Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "searching a plugins folder for WorldEditorBridge (taken as not there)");
            return false;
        }
    }

    /// <summary>The plugin's settings file (BepInEx .cfg: "Key = value" lines).</summary>
    public static Bridge? Parse(string path, string where) => ParseText(File.ReadAllText(path), path, where);

    /// <summary>
    /// The bridge settings in a .cfg file's text: its Token and Port (5182 when missing or not a number). Null
    /// when there is no token.
    /// </summary>
    public static Bridge? ParseText(string text, string path, string where)
    {
        string? Get(string key) => Regex.Match(text, $@"^\s*{key}\s*=\s*(.*?)\s*$", RegexOptions.Multiline) is { Success: true } m ? m.Groups[1].Value : null;
        string? token = Get("Token");
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }
        return new Bridge(path, where, int.TryParse(Get("Port"), out int p) ? p : 5182, token);
    }
}
