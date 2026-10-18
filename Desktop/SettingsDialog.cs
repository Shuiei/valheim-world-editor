using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Settings: folders on this computer (the web start page's settings): the Valheim game folder, more
/// BepInEx folders to look for the plugin in, more world folders to list; Claude's connection; on Windows, what draws.
/// </summary>
public static partial class SettingsDialog
{
    /// <summary>A typed folder made absolute (Places.ExpandHome first).</summary>
    private static string Expand(string path) => Path.GetFullPath(Places.ExpandHome(path));

    /// <summary>
    /// Checks and applies the folder choices, saving the settings; nothing is changed when there is a problem. A
    /// changed game folder starts <c>CheckGameLook</c>.
    /// </summary>
    /// <param name="settings">The settings to change and save.</param>
    /// <param name="valheim">The game folder typed; null or blank to find it automatically.</param>
    /// <param name="bepInEx">More BepInEx folders; blank entries are dropped, each must exist.</param>
    /// <param name="worlds">More world folders; blank entries are dropped, each must exist.</param>
    /// <returns>The problems, as one line, or null when saved.</returns>
    public static string? Apply(AppSettings settings, string? valheim, IEnumerable<string> bepInEx, IEnumerable<string> worlds)
    {
        var problems = new List<string>();
        string? game = string.IsNullOrWhiteSpace(valheim) ? null : Expand(valheim);
        if (game != null)
        {
            // A folder next to it (steamapps, valheim_Data…) is taken as the game folder it leads to.
            game = GameLook.GameFolder(game) ?? game;
            if (GameLook.BundlesDir(game) == null)
            {
                problems.Add(Lang.T("settings.notValheim"));
            }
        }
        List<string> Clean(IEnumerable<string> list, string notFound) => [.. list.Select(p => p.Trim()).Where(p => p.Length > 0).Select(Expand).Distinct().Where(p => { if (Directory.Exists(p)) { return true; } problems.Add(Lang.T(notFound, p)); return false; })];
        var bep = Clean(bepInEx, "settings.bepInExNotFound");
        var wl = Clean(worlds, "settings.worldNotFound");
        if (problems.Count > 0)
        {
            return string.Join(" ", problems);
        }
        bool gameChanged = game != settings.ValheimPath;
        var before = (settings.ValheimPath, settings.BepInExFolders, settings.WorldFolders);
        settings.ValheimPath = game;
        settings.BepInExFolders = bep;
        settings.WorldFolders = wl;
        if (settings.Save() is string notSaved)
        {
            // Not saved, not changed: they would look kept until the next start.
            (settings.ValheimPath, settings.BepInExFolders, settings.WorldFolders) = before;
            return Lang.T("settings.notSaved", notSaved);
        }
        if (gameChanged)
        {
            // Another game folder: its look (copied again when it is another version).
            CheckGameLook(settings);
        }
        return null;
    }

    /// <summary>Tests: what a changed game folder starts (reading the game's files otherwise).</summary>
    internal static Action<AppSettings> CheckGameLook { get; set; } = GameLook.Check;

    /// <summary>Whether the Drawing part is shown (Windows only; tests set it).</summary>
    internal static bool OnWindows { get; set; } = OperatingSystem.IsWindows();

    /// <summary>Tests: the folder picker (null: the system's).</summary>
    internal static Func<string, Task<string?>>? PickFolder { get; set; }

    /// <summary>Shows the Settings dialog over <paramref name="owner"/> and waits for it to close.</summary>
    /// <returns>True when the user saved (the folders, Claude's connection settings and what draws), false when cancelled or closed.</returns>
    public static async Task<bool> Show(Window owner, AppSettings settings)
    {
        var form = new Form(settings);
        // The mods' editor extensions: found again now (a mod installed since). Away from the window's thread: every
        // extension file is read and hashed.
        await Task.Run(() =>
        {
            ModManifests.Refresh(settings);
            ModExtensions.Refresh(settings);
        });
        form.Build();
        await form.Dialog.ShowDialog(owner);
        return form.Saved;
    }
}
