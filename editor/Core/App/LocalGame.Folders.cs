namespace TerrainEditor.App;

/// <summary>
/// Finding the BepInEx folders the game may run from: the Valheim install, the profile the editor runs from and
/// the folders the player added in Settings or in the mod managers.
/// </summary>
public static partial class LocalGame
{
    /// <summary>
    /// Folders that can hold the game's BepInEx: the Valheim install, and mod manager profiles. Each
    /// once: on Linux the application data folder is ~/.config, so r2modman's would come twice.
    /// </summary>
    /// <returns>Each BepInEx folder (which may not exist) and where it is, for the player.</returns>
    public static IEnumerable<(string BepInEx, string Where)> BepInExFolders(AppSettings settings) =>
        AllBepInExFolders(settings).DistinctBy(f => Path.GetFullPath(f.BepInEx).TrimEnd(Path.DirectorySeparatorChar));

    /// <summary>
    /// <see cref="BepInExFolders"/> before duplicates are dropped: the Valheim install, the profile the editor
    /// runs from, the folders added in Settings, then r2modman's and Thunderstore Mod Manager's profiles.
    /// </summary>
    private static IEnumerable<(string BepInEx, string Where)> AllBepInExFolders(AppSettings settings)
    {
        if (SearchDefaultPlaces && GameLook.FindValheim(settings.ValheimPath) is string valheim)
        {
            yield return (Path.Combine(valheim, "BepInEx"), "Valheim folder");
        }
        // Installed by a mod manager (Thunderstore package): the editor sits in <profile>/BepInEx/plugins/...
        if (SearchDefaultPlaces && InstalledIn() is string own)
        {
            yield return (own, "this mod manager profile");
        }
        foreach (var folder in AddedBepInExFolders(settings.BepInExFolders))
        {
            yield return folder;
        }
        if (!SearchDefaultPlaces)
        {
            yield break;
        }
        foreach (var folder in ManagerProfileFolders())
        {
            yield return folder;
        }
    }

    /// <summary>The BepInEx folder holding the editor, when a mod manager installed it into a profile's plugins.</summary>
    /// <param name="from">The folder to look up from; the editor's own by default.</param>
    /// <returns>
    /// The BepInEx folder whose plugins folder is <paramref name="from"/> or holds it, else null.
    /// </returns>
    public static string? InstalledIn(string? from = null)
    {
        for (var d = new DirectoryInfo(from ?? AppContext.BaseDirectory); d?.Parent != null; d = d.Parent)
        {
            if (d.Name.Equals("plugins", StringComparison.OrdinalIgnoreCase) && d.Parent.Name.Equals("BepInEx", StringComparison.OrdinalIgnoreCase))
            {
                return d.Parent.FullName;
            }
        }
        return null;
    }
}
