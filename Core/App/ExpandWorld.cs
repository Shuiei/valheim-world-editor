using TerrainEditor.Save;
using ValheimGen;

namespace TerrainEditor.App;

/// <summary>
/// Expand World, the world generation mods (Expand World Size, Data and Rivers, by JereKuusela), as the editor reads
/// them: which of them a BepInEx folder has, their settings files, and what a world was made with. It gives a world's
/// <see cref="WorldRules"/>: the game's own values unless the Expand World switch in Settings is on
/// (<see cref="AppSettings.ExpandWorld"/>); then the Size settings the world file keeps (every non-default one, as
/// "ews_&lt;setting&gt; &lt;value&gt;" among the starting global keys; a world without any uses the Size settings
/// file), and the Rivers settings file, both from the first BepInEx folder the editor knows that has any of the mods.
/// </summary>
/// <remarks>
/// Followed: Expand World Size 1.45 (expand_world_size.cfg; WorldSettings: the keys), Expand World Rivers 1.11
/// (expand_world_rivers.cfg), Expand World Data 1.79 (expand_world_data.cfg and the expand_world folder, whose data
/// the editor does not read yet: only that the mod is there).
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
        /// <summary>Any of the mods is there.</summary>
        public bool Any => Size || Data || Rivers;
    }

    /// <summary>
    /// The first BepInEx folder the editor knows (<see cref="LocalGame.BepInExFolders"/>) that has any of the mods;
    /// null when none has.
    /// </summary>
    /// <param name="settings">The editor's settings (Valheim path, added BepInEx folders).</param>
    public static Found? Find(AppSettings settings)
    {
        foreach (var (dir, where) in LocalGame.BepInExFolders(settings))
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }
            var found = Look(dir, where);
            if (found.Any)
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>What one BepInEx folder holds of the mods.</summary>
    /// <param name="dir">The BepInEx folder.</param>
    /// <param name="where">Where it is, for the player.</param>
    public static Found Look(string dir, string where)
    {
        string config = Path.Combine(dir, "config");
        string? Cfg(string guid) => File.Exists(Path.Combine(config, guid + ".cfg")) ? Path.Combine(config, guid + ".cfg") : null;
        string? sizeCfg = Cfg(SizeGuid), dataCfg = Cfg(DataGuid), riversCfg = Cfg(RiversGuid);
        string? dataFiles = Directory.Exists(Path.Combine(config, DataFolder)) ? Path.Combine(config, DataFolder) : null;
        string plugins = Path.Combine(dir, "plugins");
        return new Found(dir, where,
            sizeCfg != null || HasPlugin(plugins, "ExpandWorldSize.dll"),
            dataCfg != null || dataFiles != null || HasPlugin(plugins, "ExpandWorldData.dll"),
            riversCfg != null || HasPlugin(plugins, "ExpandWorldRivers.dll"),
            sizeCfg, dataCfg, riversCfg, dataFiles);
    }

    /// <summary>
    /// Whether a plugin's DLL is somewhere in a plugins folder. Subfolders that cannot be read are passed over (another
    /// mod's, without access); a folder that cannot be searched at all is said in the log and has none.
    /// </summary>
    private static bool HasPlugin(string plugins, string dll)
    {
        if (!Directory.Exists(plugins))
        {
            return false;
        }
        try
        {
            return Directory.EnumerateFiles(plugins, dll, new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 }).Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, $"searching a plugins folder for {dll} (taken as not there)");
            return false;
        }
    }

    /// <summary>
    /// A world's rules, and the log line saying where they came from: the game's own values while the Expand World
    /// switch is off (a world made with Expand World Size is noted), else from the world file's keys and the mods'
    /// settings files (<see cref="Rules"/>).
    /// </summary>
    /// <param name="save">The world (its name and starting global keys).</param>
    /// <param name="settings">The editor's settings.</param>
    public static WorldRules Resolve(WorldSave save, AppSettings settings)
    {
        var keys = Keys(save.StartingGlobalKeys);
        if (!settings.ExpandWorld)
        {
            if (keys.Count > 0)
            {
                Console.WriteLine($"Expand World: {save.Name} was made with Expand World Size ({keys.Count} settings in its world file), but the Expand World switch in Settings is off: generated as the game itself does.");
            }
            return WorldRules.Vanilla;
        }
        var found = Find(settings);
        var rules = Rules(keys, found);
        string from = keys.Count > 0 ? $"Size settings from the world file ({keys.Count})" : found?.SizeCfg != null ? "Size settings from " + found.SizeCfg : "no Size settings";
        string folder = found == null ? "no BepInEx folder has Expand World" : $"{found.Folder} ({found.Where}): Size {(found.Size ? "yes" : "no")}, Data {(found.Data ? "yes" : "no")}, Rivers {(found.Rivers ? "yes" : "no")}";
        Console.WriteLine($"Expand World: {save.Name}: {from}; {folder}; {Describe(rules)}.");
        if (keys.Count > 0 && found == null)
        {
            Console.WriteLine("Expand World: the world was made with Expand World Size, but no BepInEx folder the editor knows has the mod: its Rivers and Data settings are unknown (add the folder in Settings).");
        }
        return rules;
    }

    /// <summary>
    /// The rules from a world's keys and a folder's settings files: the Size settings from the keys when the world has
    /// any (a key absent is that setting's default, as the mod keeps only the non-default ones), else from the Size
    /// settings file; the Rivers settings from their file; which mods are installed from the folder.
    /// </summary>
    /// <param name="keys">The world's Expand World Size keys (<see cref="Keys"/>).</param>
    /// <param name="found">The folder's mods and files; null for none.</param>
    public static WorldRules Rules(IReadOnlyDictionary<string, string> keys, Found? found)
    {
        var size = found?.SizeCfg is string s ? ReadCfgFile(s) : null;
        var rivers = found?.RiversCfg is string r ? ReadCfgFile(r) : null;
        bool fromKeys = keys.Count > 0;
        string? S(string name) => fromKeys ? keys.GetValueOrDefault(KeyOf(name)) : size?.GetValueOrDefault(name);
        string? R(string name) => rivers?.GetValueOrDefault(name);
        var v = WorldRules.Vanilla;
        bool sizeInstalled = found?.Size == true || fromKeys;
        float radius = Float(S("World radius"), v.Radius), edge = Float(S("World edge size"), v.EdgeSize);
        var rules = new WorldRules
        {
            SizeInstalled = sizeInstalled,
            DataInstalled = found?.Data == true,
            RiversInstalled = found?.Rivers == true,
            Radius = radius,
            EdgeSize = edge,
            Stretch = Positive(Float(S("Stretch world"), v.Stretch)),
            BiomeStretch = Positive(Float(S("Stretch biomes"), v.BiomeStretch)),
            AltitudeMultiplier = Float(S("Altitude multiplier"), v.AltitudeMultiplier),
            AltitudeDelta = Float(S("Altitude delta"), v.AltitudeDelta),
            WaterDepthMultiplier = Float(S("Water depth multiplier"), v.WaterDepthMultiplier),
            ForestMultiplier = Float(S("Forest multiplier"), v.ForestMultiplier),
            LocationsMultiplier = Float(S("Locations"), v.LocationsMultiplier),
            OffsetX = Float(S("Offset X")),
            OffsetY = Float(S("Offset Y")),
            HeightSeed = Float(S("Height variation seed")),
            RestrictAshlands = Bool(S("Restrict Ashlands position"), v.RestrictAshlands),
            AshlandsWidthRestriction = Float(S("Ashlands width restriction"), v.AshlandsWidthRestriction),
            AshlandsLengthRestriction = Float(S("Ashlands length restriction"), v.AshlandsLengthRestriction),
            AshlandsGap = Bool(S("Ashlands gap"), v.AshlandsGap),
            DeepNorthGap = Bool(S("Deep North gap"), v.DeepNorthGap),
            GridWidth = Int(keys.GetValueOrDefault(GridWidthKey)) ?? (sizeInstalled ? GridWidthFor(Int(S("Save system size")) ?? 0, radius + edge) : v.GridWidth),
            WaterLevel = Float(R("Water level"), v.WaterLevel),
            LakeSearchInterval = Float(R("Lake search interval"), v.LakeSearchInterval),
            LakeDepth = Float(R("Lake depth"), v.LakeDepth),
            LakeMergeRadius = Float(R("Lake merge radius"), v.LakeMergeRadius),
            LakeMaxDistance1 = Float(R("Lake max distance 1"), v.LakeMaxDistance1),
            LakeMaxDistance2 = Float(R("Lake max distance 2"), v.LakeMaxDistance2),
            Rivers = Bool(R("Rivers"), v.Rivers),
            RiverCheckInterval = Float(R("River check interval"), v.RiverCheckInterval),
            RiverMaxAltitude = Float(R("River max altitude"), v.RiverMaxAltitude),
            RiverMinWidth = Float(R("River min width"), v.RiverMinWidth),
            RiverMaxWidth = Float(R("River max width"), v.RiverMaxWidth),
            RiverCurveWidth = Float(R("River curve width"), v.RiverCurveWidth),
            RiverCurveWaveLength = Float(R("River curve wave length"), v.RiverCurveWaveLength),
            RiverSeed = Int(R("River seed")),
            Streams = Bool(R("Streams"), v.Streams),
            StreamSeed = Int(R("Stream seed")),
            StreamMaxAmount = Int(R("Stream max amount")) ?? v.StreamMaxAmount,
            StreamSearchIterations = Int(R("Stream search iterations")) ?? v.StreamSearchIterations,
            StreamStartMinAltitude = Float(R("Stream start min altitude"), v.StreamStartMinAltitude),
            StreamStartMaxAltitude = Float(R("Stream start max altitude"), v.StreamStartMaxAltitude),
            StreamEndMinAltitude = Float(R("Stream end min altitude"), v.StreamEndMinAltitude),
            StreamEndMaxAltitude = Float(R("Stream end max altitude"), v.StreamEndMaxAltitude),
            StreamMaxWidth = Float(R("Stream max width"), v.StreamMaxWidth),
            StreamMinLength = Float(R("Stream min length"), v.StreamMinLength),
            StreamMaxLength = Float(R("Stream max length"), v.StreamMaxLength),
            StreamCurveWidth = Float(R("Stream curve width"), v.StreamCurveWidth),
            StreamCurveWaveLength = Float(R("Stream curve wave length"), v.StreamCurveWaveLength),
        };
        return rules;
    }

    /// <summary>
    /// The save's zone grid as Expand World Size computes it (ZoneGrid.GetWidth): the "Save system size" setting times
    /// 64 zones, or from the total radius (32 m a zone, in steps of 64 zones); 512 to 2048.
    /// </summary>
    /// <param name="saveSystemSize">The setting (0: from the radius).</param>
    /// <param name="totalRadius">The radius and the edge, metres.</param>
    public static int GridWidthFor(int saveSystemSize, float totalRadius)
    {
        int zones = saveSystemSize > 0 ? saveSystemSize * 64 : (int)Math.Ceiling(totalRadius / 32f / 64f) * 64;
        return Math.Max(512, Math.Min(2048, zones));
    }

    /// <summary>What the rules say, in a few words, for the log.</summary>
    public static string Describe(WorldRules r)
    {
        if (r.IsVanilla)
        {
            return "the game's own values";
        }
        List<string> parts = [$"radius {r.Radius:0.#} + {r.EdgeSize:0.#} m", $"grid {r.GridWidth} zones", $"water {r.WaterLevel:0.#} m"];
        if (r.Stretch != 1f || r.BiomeStretch != 1f)
        {
            parts.Add($"stretch {r.Stretch:0.##} (biomes {r.BiomeStretch:0.##})");
        }
        if (r.AltitudeMultiplier != 1f || r.AltitudeDelta != 0f || r.WaterDepthMultiplier != 1f)
        {
            parts.Add($"altitude x{r.AltitudeMultiplier:0.##} {r.AltitudeDelta:+0.#;-0.#;+0} m, depth x{r.WaterDepthMultiplier:0.##}");
        }
        if (!r.Rivers || !r.Streams)
        {
            parts.Add($"rivers {(r.Rivers ? "on" : "off")}, streams {(r.Streams ? "on" : "off")}");
        }
        return string.Join(", ", parts);
    }

    /// <summary>A stretch of 0 or less would divide every coordinate by nothing: taken as 1.</summary>
    private static float Positive(float v) => v > 0f ? v : 1f;
}
