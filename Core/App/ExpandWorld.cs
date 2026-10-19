using ValheimGen;

namespace TerrainEditor.App;

/// <summary>
/// Expand World, the world generation mods (Expand World Size, Data and Rivers, by JereKuusela), as the editor reads
/// them: which of them a BepInEx folder has, their settings files, and what a world was made with. It gives a world's
/// <see cref="WorldRules"/>: the game's own values unless the Expand World switch in Settings is on
/// (<see cref="AppSettings.ExpandWorld"/>); then the Size settings the world file keeps (every non-default one, as
/// "ews_&lt;setting&gt; &lt;value&gt;" among the starting global keys; a world without any uses the Size settings
/// file), the Rivers settings file, and the Data settings file and data files, all from the first BepInEx folder the
/// editor knows that has any of the mods.
/// </summary>
/// <remarks>
/// Followed: Expand World Size 1.45 (expand_world_size.cfg; WorldSettings: the keys), Expand World Rivers 1.11
/// (expand_world_rivers.cfg), Expand World Data 1.79 (expand_world_data.cfg and the expand_world folder: the biome,
/// world, territory, location, vegetation and alt biome files). The parts: this file (the mods in a BepInEx folder),
/// ExpandWorld.Resolve (a world's rules and the log), ExpandWorld.Live (a live world's, the files through the game's
/// bridge), ExpandWorld.Rules (the rules from the keys and files), ExpandWorld.Describe, ExpandWorld.Keys,
/// ExpandWorld.Cfg and ExpandWorld.Values (reading what the mods write), ExpandWorld.Data (the data files, through
/// <see cref="YamlReader"/>), ExpandWorld.Fields (an entry's values), ExpandWorld.Biomes, ExpandWorld.World,
/// ExpandWorld.Territories, ExpandWorld.Locations, ExpandWorld.Vegetation and ExpandWorld.AltBiomes (the files'
/// entries as the rules).
/// </remarks>
public static partial class ExpandWorld
{
    /// <summary>The mods' plugin ids (their settings files are named after them).</summary>
    public const string SizeGuid = "expand_world_size", DataGuid = "expand_world_data", RiversGuid = "expand_world_rivers";

    /// <summary>What Expand World Size puts before a setting's name in the world file's keys.</summary>
    public const string KeyPrefix = "ews_";

    /// <summary>The key Expand World Size keeps the save's zone grid under (zones per side).</summary>
    public const string GridWidthKey = KeyPrefix + "gridwidth";

    /// <summary>The folder under BepInEx/config holding Expand World Data's yaml files.</summary>
    public const string DataFolder = "expand_world";

    /// <summary>
    /// What a BepInEx folder holds of the mods: each one installed (its plugin DLL under plugins, or its settings
    /// file), and the settings files that exist.
    /// </summary>
    /// <param name="Folder">The BepInEx folder.</param>
    /// <param name="Where">Where it is, for the player ("Valheim folder", "r2modman profile "x""...).</param>
    /// <param name="Size">Expand World Size is installed there.</param>
    /// <param name="Data">Expand World Data is installed there.</param>
    /// <param name="Rivers">Expand World Rivers is installed there.</param>
    /// <param name="SizeCfg">Expand World Size's settings file, when it exists.</param>
    /// <param name="DataCfg">Expand World Data's settings file, when it exists.</param>
    /// <param name="RiversCfg">Expand World Rivers' settings file, when it exists.</param>
    /// <param name="DataFiles">Expand World Data's yaml folder, when it exists.</param>
    public sealed record Found(string Folder, string Where, bool Size, bool Data, bool Rivers, string? SizeCfg, string? DataCfg, string? RiversCfg, string? DataFiles)
    {
        /// <summary>No folder with any of the mods.</summary>
        public static Found None { get; } = new("", "", false, false, false, null, null, null, null);

        /// <summary>Any of the mods is there.</summary>
        public bool Any => Size || Data || Rivers;

        /// <summary>
        /// Where the YamlDotNet library Expand World Data installs is looked for, when not under the folder's own
        /// plugins: a live game's files are copied to a folder without plugins, and the library is taken from a local
        /// BepInEx folder (<see cref="ResolveLive"/>).
        /// </summary>
        public string? PluginsFolder { get; init; }

        /// <summary>The plugins folder the library is looked for in: <see cref="PluginsFolder"/>, else the folder's own.</summary>
        public string Plugins => PluginsFolder ?? Path.Combine(Folder, "plugins");
    }

    /// <summary>
    /// The BepInEx folder with the mods: the one named in Settings (<see cref="AppSettings.ExpandWorldFolder"/>) when it
    /// has any of them, else the first folder the editor knows (<see cref="LocalGame.BepInExFolders"/>) that has any;
    /// <see cref="Found.None"/> when none has.
    /// </summary>
    /// <param name="settings">The editor's settings (the Expand World folder, the Valheim path, added BepInEx folders).</param>
    public static Found Find(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.ExpandWorldFolder))
        {
            var chosen = Look(settings.ExpandWorldFolder, "the Expand World folder in Settings");
            if (chosen.Any)
            {
                return chosen;
            }
            Console.WriteLine($"Expand World: the folder in Settings, {settings.ExpandWorldFolder}, has none of the mods: the other BepInEx folders are looked at.");
        }
        foreach (var (dir, where) in LocalGame.BepInExFolders(settings))
        {
            var found = Look(dir, where);
            if (found.Any)
            {
                return found;
            }
        }
        return Found.None;
    }

    /// <summary>
    /// The rules a new world is made and looked at with (the New world page): the game's own values while the Expand
    /// World switch is off, else the mods' settings files and data files of the folder found (a new world has no world
    /// file yet, so no keys); said in the log.
    /// </summary>
    /// <param name="settings">The editor's settings.</param>
    public static WorldRules ForNewWorld(AppSettings settings)
    {
        if (!settings.ExpandWorld)
        {
            return WorldRules.Vanilla;
        }
        var found = Find(settings);
        if (!found.Any)
        {
            return WorldRules.Vanilla;
        }
        var data = ReadData(found);
        var rules = Rules(new Dictionary<string, string>(), found, data);
        Console.WriteLine($"Expand World: a new world: {FolderText(found)}; {Describe(rules)}.");
        NoteData(data, rules);
        return rules;
    }

    /// <summary>What one BepInEx folder holds of the mods (a folder that does not exist holds none).</summary>
    /// <param name="dir">The BepInEx folder.</param>
    /// <param name="where">Where it is, for the player.</param>
    public static Found Look(string dir, string where)
    {
        string config = Path.Combine(dir, "config"), plugins = Path.Combine(dir, "plugins");
        string? sizeCfg = Existing(Path.Combine(config, SizeGuid + ".cfg"));
        string? dataCfg = Existing(Path.Combine(config, DataGuid + ".cfg"));
        string? riversCfg = Existing(Path.Combine(config, RiversGuid + ".cfg"));
        string? dataFiles = Directory.Exists(Path.Combine(config, DataFolder)) ? Path.Combine(config, DataFolder) : null;
        bool size = sizeCfg != null || HasPlugin(plugins, "ExpandWorldSize.dll");
        bool data = dataCfg != null || dataFiles != null || HasPlugin(plugins, "ExpandWorldData.dll");
        bool rivers = riversCfg != null || HasPlugin(plugins, "ExpandWorldRivers.dll");
        return new Found(dir, where, size, data, rivers, sizeCfg, dataCfg, riversCfg, dataFiles);
    }

    /// <summary>The path when the file exists, else null.</summary>
    private static string? Existing(string path) => File.Exists(path) ? path : null;

    /// <summary>
    /// Whether a plugin's DLL is somewhere in a plugins folder. Subfolders that cannot be read are passed over (another
    /// mod's, without access); a folder that cannot be searched at all is said in the log and has none.
    /// </summary>
    private static bool HasPlugin(string plugins, string dll)
    {
        try
        {
            return Directory.Exists(plugins) && Directory.EnumerateFiles(plugins, dll, new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 }).Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, $"searching a plugins folder for {dll} (taken as not there)");
            return false;
        }
    }
}
