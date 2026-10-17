using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Homestead (sighsorry-Homestead), the in-game building mod: its blueprints are the editor's building
/// library. They are PlanBuild .blueprint files with Homestead's own header lines, a .png picture
/// beside each, in Homestead/Blueprints of Valheim's save folder (next to worlds_local), where Homestead
/// lists them in its hammer tab and players build them in game with a materials chest. Each piece is
/// "prefab;category;x;y;z;qx;qy;qz;qw;"text";sx;sy;sz", measured from the blueprint's anchor: the
/// middle of the saved area at ground level, turned with it (ZoneBlueprintFileFormat.cs, Homestead's
/// source on GitHub).
/// </summary>
public static partial class Homestead
{
    /// <summary>Homestead's Thunderstore page, for players who do not have it.</summary>
    public const string PageUrl = "https://thunderstore.io/c/valheim/p/sighsorry/Homestead/";
    /// <summary>The #HomesteadVersion written into blueprints.</summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// Whether Homestead is installed (in the game's BepInEx or a mod manager profile, its version and
    /// where), and its blueprint folder (made when the first blueprint is saved).
    /// </summary>
    /// <param name="Installed">Whether a Homestead.dll was found in any BepInEx plugins folder.</param>
    /// <param name="Version">The first found copy's version, null when unknown.</param>
    /// <param name="Where">The places (game, profile names) that have it.</param>
    /// <param name="Folder">The blueprint folder (<c>Homestead.Folder()</c>), which may not exist yet.</param>
    public sealed record Status(bool Installed, string? Version, List<string> Where, string Folder);

    /// <summary>Looks for Homestead.dll in every BepInEx folder <c>LocalGame.BepInExFolders</c> knows.</summary>
    public static Status Find(AppSettings settings)
    {
        var where = new List<string>();
        string? version = null;
        foreach (var (dir, place) in LocalGame.BepInExFolders(settings))
        {
            string plugins = Path.Combine(dir, "plugins");
            if (!Directory.Exists(plugins))
            {
                continue;
            }
            try
            {
                foreach (string dll in Directory.EnumerateFiles(plugins, "Homestead.dll", SearchOption.AllDirectories))
                {
                    where.Add(place);
                    version ??= VersionOf(dll);
                    break;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.CaughtBrief(ex, "searching a plugins folder for Homestead (passed over)");
            }
        }
        return new Status(where.Count > 0, version, where, Folder());
    }

    /// <summary>
    /// The mod package's manifest.json (as mod managers install it), else the DLL's own version; null when
    /// neither can be read.
    /// </summary>
    private static string? VersionOf(string dll)
    {
        try
        {
            string manifest = Path.Combine(Path.GetDirectoryName(dll)!, "manifest.json");
            if (File.Exists(manifest) && JsonNode.Parse(File.ReadAllText(manifest))?["version_number"] is JsonNode v)
            {
                return (string?)v;
            }
            return FileVersionInfo.GetVersionInfo(dll).FileVersion;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log.CaughtBrief(ex, "reading Homestead's version (unknown)");
            return null;
        }
    }

    /// <summary>Another folder than the player's (the tests': they must never write into the game's folders).</summary>
    public static string? FolderOverride { get; set; }

    /// <summary>
    /// Homestead/Blueprints in Valheim's save folder: the one that has it already, else the first save
    /// folder the game made (where its worlds are), else the usual one for this system.
    /// </summary>
    public static string Folder()
    {
        if (FolderOverride != null)
        {
            return FolderOverride;
        }
        var saves = Places.WorldRoots().Select(r => Path.GetDirectoryName(r)!).Distinct().ToList();
        static string Of(string save) => Path.Combine(save, "Homestead", "Blueprints");
        return saves.Select(Of).FirstOrDefault(Directory.Exists) ?? Of(saves.FirstOrDefault(Directory.Exists) ?? saves[0]);
    }
}
