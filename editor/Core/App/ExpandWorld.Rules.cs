using ValheimGen;

namespace TerrainEditor.App;

// The rules from a world's keys and a folder's settings files.
public static partial class ExpandWorld
{
    /// <summary>
    /// The rules from a world's keys and a folder's settings and data files: the Size settings from the keys when the
    /// world has any (a key absent is that setting's default, as the mod keeps only the non-default ones), else from
    /// the Size settings file; the Rivers settings from their file; the Data settings and data files
    /// (<see cref="ReadData"/>); which mods are installed from the folder.
    /// </summary>
    /// <param name="keys">The world's Expand World Size keys (<see cref="Keys"/>).</param>
    /// <param name="found">The folder's mods and files (<see cref="Found.None"/> for none).</param>
    public static WorldRules Rules(IReadOnlyDictionary<string, string> keys, Found found) => Rules(keys, found, ReadData(found));

    /// <summary>
    /// The same, with Expand World Data's files read already (their notes get those of the conversion), and the
    /// world's global keys (the vegetation file's rules may need or forbid some).
    /// </summary>
    /// <param name="keys">The world's Expand World Size keys.</param>
    /// <param name="found">The folder's mods and files.</param>
    /// <param name="data">Expand World Data's settings and files.</param>
    /// <param name="globalKeys">The world's global keys as the save keeps them ("name" or "name value"); none by default.</param>
    public static WorldRules Rules(IReadOnlyDictionary<string, string> keys, Found found, DataFiles data, IEnumerable<string>? globalKeys = null)
    {
        var size = SizeSettings(keys, found);
        string? S(string name) => size(name);
        string? D(string name) => data.Cfg.GetValueOrDefault(name);
        var v = WorldRules.Vanilla;
        bool sizeInstalled = found.Size;
        float radius = Float(S("World radius"), v.Radius), edge = Float(S("World edge size"), v.EdgeSize);
        // The Ashlands' limits and the gaps: both Size and Data patch them (the mod's docs say to set them in one);
        // a limit Data sets away from the game's wins, a gap is off when either turns it off.
        float dataWidth = Float(D("Ashlands width restriction"), v.AshlandsWidthRestriction), dataLength = Float(D("Ashlands length restriction"), v.AshlandsLengthRestriction);
        var biomes = data.Biomes is { Count: > 0 } biomeEntries ? BiomesOf(biomeEntries, data.Notes) : null;
        var table = biomes ?? BiomeTable.Vanilla;
        (List<BiomeRule> Rules, bool CheckSectors)? world = data.World == null ? null : WorldOf(data.World.Count > 0 ? data.World : DefaultWorld, table, radius, Wiggles.Of(data.Cfg), data.Notes);
        var sizing = new WorldRules { SizeInstalled = sizeInstalled, DataInstalled = found.Data, Radius = radius, LocationsMultiplier = Float(S("Locations"), v.LocationsMultiplier) };
        var (locations, vegetation) = Places(data, found, table, sizing, globalKeys);
        var rules = new WorldRules
        {
            SizeInstalled = sizeInstalled,
            DataInstalled = found.Data,
            RiversInstalled = found.Rivers,
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
            RestrictAshlands = BothOn(S("Restrict Ashlands position"), D("Restrict Ashlands position"), v.RestrictAshlands),
            AshlandsWidthRestriction = Overriding(dataWidth, v.AshlandsWidthRestriction, Float(S("Ashlands width restriction"), v.AshlandsWidthRestriction)),
            AshlandsLengthRestriction = Overriding(dataLength, v.AshlandsLengthRestriction, Float(S("Ashlands length restriction"), v.AshlandsLengthRestriction)),
            AshlandsGap = BothOn(S("Ashlands gap"), D("Ashlands gap"), v.AshlandsGap),
            DeepNorthGap = BothOn(S("Deep North gap"), D("Deep North gap"), v.DeepNorthGap),
            // The world's keys only ("ews_gridwidth"): the settings file has no such setting.
            GridWidth = Int(S("GridWidth"), GridWidthFor(sizeInstalled, Int(S("Save system size"), 0), radius + edge)),
            LegacyGeneration = Bool(D("Legacy generation"), v.LegacyGeneration),
            WiggleFrequency = Float(D("Wiggle frequency"), v.WiggleFrequency),
            Biomes = table,
            BiomeData = biomes != null,
            World = world?.Rules,
            CheckSectors = world?.CheckSectors ?? false,
            Territories = data.Territories == null ? [] : TerritoriesOf(data.Territories, data.Notes),
            Locations = locations,
            Vegetation = vegetation,
            ZoneSpawners = Bool(D("Zone spawners"), v.ZoneSpawners),
            RandomLocations = Bool(D("Random locations"), v.RandomLocations),
            ScaleLocationAltitude = Bool(D("Scale location altitude requirement"), v.ScaleLocationAltitude),
            CustomWaterColor = Bool(D("Custom water color"), v.CustomWaterColor),
            Dungeons = DungeonsOf(data, BlueprintFolder(data, found)),
        };
        return WithRivers(rules, found.Rivers ? CfgOf(found.RiversCfg) : []);
    }

    /// <summary>The folder Expand World Data reads blueprint files from; null without the mod.</summary>
    private static string? BlueprintFolder(DataFiles data, Found found) => found.Data ? Path.Combine(found.Folder, "config", BlueprintFolderOf(data.Cfg)) : null;

    /// <summary>
    /// Expand World Size's settings in use, by setting name: none without the mod (the game without it passes over
    /// the keys it keeps in the world file); else the world file's keys when it keeps some; else the settings file.
    /// </summary>
    private static Func<string, string?> SizeSettings(IReadOnlyDictionary<string, string> keys, Found found)
    {
        if (!found.Size)
        {
            return _ => null;
        }
        if (keys.Count > 0)
        {
            return name => keys.GetValueOrDefault(KeyOf(name));
        }
        var cfg = CfgOf(found.SizeCfg);
        return cfg.GetValueOrDefault;
    }

    /// <summary>
    /// The location kinds and vegetation rules a world is laid out with: the location file's (when in use, with the
    /// alt biome file's alt biomes when in use) and the vegetation file's, each converted against the game's own
    /// rules; without a location file, the game's kinds as Expand World Size changes them (null when it changes
    /// nothing). The game's rules not read yet: the files cannot be converted, noted.
    /// </summary>
    private static (LocationRuleSet? Locations, IReadOnlyList<VegetationRule>? Vegetation) Places(DataFiles data, Found found, BiomeTable table, WorldRules size, IEnumerable<string>? globalKeys)
    {
        var game = SeedLocations.Current;
        bool gameRead = SeedLocations.Available;
        string? blueprints = BlueprintFolder(data, found);
        var dataTable = new DataTable(data.DataEntries);
        List<LocationRule>? kinds = null;
        if (data.Locations is { Count: > 0 } entries)
        {
            if (gameRead)
            {
                kinds = SizedLocations(LocationsOf(entries, game.Locations, table, size.Radius, blueprints, data.Notes, dataTable), size);
            }
            else
            {
                data.Notes.Add("the game's location rules are not read yet: expand_locations is not used");
            }
        }
        else if (gameRead && (size.LocationsMultiplier != 1f || (!size.DataInstalled && size.Radius != 10000f)))
        {
            kinds = SizedLocations(game.Locations, size);
        }
        else if (gameRead && data.AltBiomes is { Count: > 0 })
        {
            // The game's kinds as they are, so that the alt biome file still says what each keeps out.
            kinds = [.. game.Locations];
        }
        LocationRuleSet? locations = kinds == null ? null : new LocationRuleSet
        {
            Locations = kinds,
            AltBiomes = data.AltBiomes is { Count: > 0 } alts ? AltBiomesOf(alts, table, data.Notes) : [.. game.AltBiomes],
        };
        IReadOnlyList<VegetationRule>? vegetation = null;
        if (data.Vegetation is { Count: > 0 } vegEntries)
        {
            var keys = (globalKeys ?? []).Select(k => k.Split(' ', 2)[0].Trim().ToLowerInvariant()).Where(k => k != "").ToHashSet();
            vegetation = VegetationOf(vegEntries, Regrow.GameRules, table, size.Radius, keys, data.Notes, dataTable, blueprints);
        }
        return (locations, vegetation);
    }

    /// <summary>
    /// The save's zone grid when the world file does not say: as Expand World Size computes it when it is installed
    /// (ZoneGrid.GetWidth: the "Save system size" setting times 64 zones, or from the total radius at 32 m a zone, in
    /// steps of 64 zones; 512 to 2048), else the game's 512.
    /// </summary>
    /// <param name="sizeInstalled">Whether Expand World Size is installed (or the world was made with it).</param>
    /// <param name="saveSystemSize">The setting (0: from the radius).</param>
    /// <param name="totalRadius">The radius and the edge, metres.</param>
    public static int GridWidthFor(bool sizeInstalled, int saveSystemSize, float totalRadius)
    {
        if (!sizeInstalled)
        {
            return WorldRules.Vanilla.GridWidth;
        }
        int zones = saveSystemSize > 0 ? saveSystemSize * 64 : (int)Math.Ceiling(totalRadius / 32f / 64f) * 64;
        return Math.Clamp(zones, 512, 2048);
    }

    /// <summary>A stretch of 0 or less would divide every coordinate by nothing: taken as 1.</summary>
    private static float Positive(float v) => v > 0f ? v : 1f;

    /// <summary>The folder under BepInEx/config Expand World Data reads blueprint files from: its "Blueprint folder" setting, PlanBuild by default.</summary>
    /// <param name="dataCfg">Expand World Data's settings file's values.</param>
    private static string BlueprintFolderOf(IReadOnlyDictionary<string, string> dataCfg) => dataCfg.GetValueOrDefault("Blueprint folder") is { Length: > 0 } folder ? folder : "PlanBuild";
}
