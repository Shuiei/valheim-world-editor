namespace TerrainEditor.App;

/// <summary>
/// The BepInEx folders behind the folders the player added in Settings and behind the mod managers' profiles.
/// </summary>
public static partial class LocalGame
{
    /// <summary>
    /// The BepInEx folders of the folders the player added: a BepInEx folder itself, a profile holding one, or a
    /// folder of profiles.
    /// </summary>
    /// <param name="folders">The folders added in Settings.</param>
    private static IEnumerable<(string BepInEx, string Where)> AddedBepInExFolders(IEnumerable<string> folders)
    {
        foreach (string extra in folders)
        {
            if (!Directory.Exists(extra))
            {
                continue;
            }
            if (Path.GetFileName(extra.TrimEnd('/', '\\')).Equals("BepInEx", StringComparison.OrdinalIgnoreCase))
            {
                yield return (extra, "your folder");
            }
            else if (Directory.Exists(Path.Combine(extra, "BepInEx")))
            {
                yield return (Path.Combine(extra, "BepInEx"), $"\"{Path.GetFileName(extra.TrimEnd('/', '\\'))}\"");
            }
            else
            {
                foreach (string profile in Places.Subfolders(extra).Where(d => Directory.Exists(Path.Combine(d, "BepInEx"))))
                {
                    yield return (Path.Combine(profile, "BepInEx"), $"profile \"{Path.GetFileName(profile)}\"");
                }
            }
        }
    }

    /// <summary>The BepInEx folders of the profiles of r2modman and Thunderstore Mod Manager on this computer.</summary>
    private static IEnumerable<(string BepInEx, string Where)> ManagerProfileFolders()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var managers = new List<(string Root, string Name)>
        {
            (Path.Combine(appData, "r2modmanPlus-local", "Valheim", "profiles"), "r2modman"),
            (Path.Combine(appData, "Thunderstore Mod Manager", "DataFolder", "Valheim", "profiles"), "Thunderstore Mod Manager"),
        };
        if (OperatingSystem.IsMacOS())
        {
            managers.Add((Path.Combine(home, "Library", "Application Support", "r2modmanPlus-local", "Valheim", "profiles"), "r2modman"));
        }
        if (!OperatingSystem.IsWindows())
        {
            managers.Add((Path.Combine(home, ".config", "r2modmanPlus-local", "Valheim", "profiles"), "r2modman"));
            managers.Add((Path.Combine(home, ".var", "app", "com.github.ebkr.r2modman", "config", "r2modmanPlus-local", "Valheim", "profiles"), "r2modman"));
        }
        foreach (var (root, name) in managers)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }
            foreach (string profile in Places.Subfolders(root))
            {
                yield return (Path.Combine(profile, "BepInEx"), $"{name} profile \"{Path.GetFileName(profile)}\"");
            }
        }
    }
}
