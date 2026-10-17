using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>The same folder, written either way (a trailing separator, and on Windows the letters' case, aside).</summary>
    private static bool SameFolder(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// create_world: a new world of a name and seed in the first worlds folder (or the one of the game's given), then
    /// opened (the New world page's Create).
    /// </summary>
    [McpServerTool(Name = "create_world", Title = "Make a new world")]
    [Description("Makes a new world, as the New world page's Create does: its name, its seed (preview_seed first), in the game's worlds folder (or another of the game's worlds folders given), and opens it on its map. Nothing existing is changed: a name already taken is refused, and so is leaving a world or the Workshop with changes not saved. The game lays out the start, traders and dungeons the first time it loads the world, and makes each place as players reach it: shaping an area before anyone played there works, but locations come later.")]
    public Task<string> CreateWorld(
        [Description("The world's name (it must be a valid file name).")] string name,
        [Description("The seed: 1 to 10 characters, as in the game.")] string seed,
        [Description("The game's worlds folder to make it in (optional: the first; another folder is refused).")] string? folder = null) => OnUi(async () =>
    {
        if (Unsaved() is string why)
        {
            return why;
        }
        name = name.Trim();
        seed = seed.Trim();
        List<string> roots = WorldFolders();
        string root;
        if (folder == null)
        {
            root = roots.FirstOrDefault() ?? Places.WorldRoots().First();
        }
        else if (roots.FirstOrDefault(r => SameFolder(r, folder)) is string known)
        {
            root = known;
        }
        else
        {
            return $"{folder} is not one of the game's worlds folders: {(roots.Count == 0 ? "there is none yet (leave folder out)" : string.Join(", ", roots))}.";
        }
        if (NewWorldPage.Check(root, name, seed, english: true) is string problem)
        {
            return problem;
        }
        if (await _w.CreateWorld(Path.Combine(root, name), name, seed) is string failed)
        {
            return failed;
        }
        return _w.World?.World.Name == name ? $"{_w.LastMessageEn} {ToJson(StateOf(_w))}" : $"Made {name}, but it could not be opened: {_w.LastMessageEn}";
    });
}
