using ValheimGen;
using ValheimWorldEditor.Sdk;

namespace ExpandWorld.WorldEditor;

/// <summary>
/// Expand World, the world generation mods (Expand World Size, Data and Rivers, by JereKuusela), as the editor reads
/// them: which of them a BepInEx folder has, their settings files, and what a world was made with. It gives a world's
/// <see cref="WorldRules"/>, as the add-on's world rules provider (<see cref="ExpandWorldRules"/>): the Size settings the world file keeps (every non-default one, as
/// "ews_&lt;setting&gt; &lt;value&gt;" among the starting global keys; a world without any uses the Size settings
/// file), the Rivers settings file, and the Data settings file and data files, all from the first BepInEx folder the
/// editor knows that has any of the mods.
/// </summary>
/// <remarks>
/// Followed: Expand World Size 1.45 (expand_world_size.cfg; WorldSettings: the keys), Expand World Rivers 1.11
/// (expand_world_rivers.cfg), Expand World Data 1.79 (expand_world_data.cfg and the expand_world folder: the biome,
/// world, territory, location, vegetation and alt biome files). The parts: this file (the mods in a BepInEx folder),
/// ExpandWorldMod.Resolve (a world's rules and the log), ExpandWorldMod.Live (a live world's, the files through the game's
/// bridge), ExpandWorldMod.Editor (what the readers take from the editor's host), ExpandWorldMod.Rules (the rules from the keys and
/// files), ExpandWorldMod.Keys,
/// ExpandWorldMod.Cfg and ExpandWorldMod.Values (reading what the mods write), ExpandWorldMod.Data (the data files, through
/// <see cref="YamlReader"/>), ExpandWorldMod.Fields (an entry's values), ExpandWorldMod.Biomes, ExpandWorldMod.World,
/// ExpandWorldMod.Territories, ExpandWorldMod.Locations, ExpandWorldMod.Vegetation and ExpandWorldMod.AltBiomes (the files'
/// entries as the rules).
/// </remarks>
public static partial class ExpandWorldMod
{
    /// <summary>The mods' plugin ids (their settings files are named after them).</summary>
    public const string SizeGuid = "expand_world_size", DataGuid = "expand_world_data", RiversGuid = "expand_world_rivers";

    /// <summary>What Expand World Size puts before a setting's name in the world file's keys.</summary>
    public const string KeyPrefix = "ews_";

    /// <summary>The folder under BepInEx/config holding Expand World Data's yaml files.</summary>
    public const string DataFolder = "expand_world";

    /// <summary>
    /// What a BepInEx folder holds of the mods: each one installed and on (its plugin DLL under plugins, by its own
    /// name: a mod manager turns a mod off by renaming it), and the settings files that exist (a mod's are read only
    /// while it is installed: BepInEx leaves them behind).
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
    /// The BepInEx folder with the mods: the first folder the editor knows (IEditorHost.BepInExFolders) that has any of
    /// them; <see cref="Found.None"/> when none has.
    /// </summary>
    /// <param name="folders">The BepInEx folders the editor knows, in order.</param>
    public static Found Find(IEnumerable<BepInExFolder> folders)
    {
        foreach (var folder in folders)
        {
            var found = Look(folder.Path, folder.Where);
            if (found.Any)
            {
                return found;
            }
        }
        return Found.None;
    }

    /// <summary>
    /// The rules a new world is made and looked at with (the New world page): the mods' settings files and data files
    /// of the folder found (a new world has no world file yet, so no keys), said in the log; the game's own values when
    /// no folder has the mods.
    /// </summary>
    /// <param name="folders">The BepInEx folders the editor knows, in order.</param>
    public static WorldRules ForNewWorld(IEnumerable<BepInExFolder> folders)
    {
        var found = Find(folders);
        if (!found.Any)
        {
            return WorldRules.Vanilla;
        }
        var data = ReadData(found);
        var rules = Rules(new Dictionary<string, string>(), found, data);
        Note($"Expand World: a new world: {FolderText(found)}; {WorldRulesText.Describe(rules)}.");
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
        bool size = HasPlugin(plugins, "ExpandWorldSize.dll");
        bool data = HasPlugin(plugins, "ExpandWorldData.dll");
        bool rivers = HasPlugin(plugins, "ExpandWorldRivers.dll");
        return new Found(dir, where, size, data, rivers, sizeCfg, dataCfg, riversCfg, dataFiles);
    }

    /// <summary>The path when the file exists, else null.</summary>
    private static string? Existing(string path) => File.Exists(path) ? path : null;

    /// <summary>Whether a plugin's DLL is in a plugins folder (PluginFiles.Has), a folder that cannot be read said in the log.</summary>
    internal static bool HasPlugin(string plugins, string dll) => PluginFiles.Has(plugins, dll, Note);
}
