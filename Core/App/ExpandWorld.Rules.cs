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

    /// <summary>The same, with Expand World Data's files read already (their notes get those of the conversion).</summary>
    /// <param name="keys">The world's Expand World Size keys.</param>
    /// <param name="found">The folder's mods and files.</param>
    /// <param name="data">Expand World Data's settings and files.</param>
    public static WorldRules Rules(IReadOnlyDictionary<string, string> keys, Found found, DataFiles data)
    {
        var size = CfgOf(found.SizeCfg);
        var rivers = CfgOf(found.RiversCfg);
        bool fromKeys = keys.Count > 0;
        string? S(string name) => fromKeys ? keys.GetValueOrDefault(KeyOf(name)) : size.GetValueOrDefault(name);
        string? R(string name) => rivers.GetValueOrDefault(name);
        string? D(string name) => data.Cfg.GetValueOrDefault(name);
        var v = WorldRules.Vanilla;
        bool sizeInstalled = found.Size || fromKeys;
        float radius = Float(S("World radius"), v.Radius), edge = Float(S("World edge size"), v.EdgeSize);
        // The Ashlands' limits and the gaps: both Size and Data patch them (the mod's docs say to set them in one);
        // a limit Data sets away from the game's wins, a gap is off when either turns it off.
        float dataWidth = Float(D("Ashlands width restriction"), v.AshlandsWidthRestriction), dataLength = Float(D("Ashlands length restriction"), v.AshlandsLengthRestriction);
        var biomes = data.Biomes is { Count: > 0 } biomeEntries ? BiomesOf(biomeEntries, data.Notes) : null;
        (List<BiomeRule> Rules, bool CheckSectors)? world = data.World == null ? null : WorldOf(data.World.Count > 0 ? data.World : DefaultWorld, biomes ?? BiomeTable.Vanilla, radius, Wiggles.Of(data.Cfg), data.Notes);
        return new WorldRules
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
            RestrictAshlands = Bool(S("Restrict Ashlands position"), v.RestrictAshlands) && Bool(D("Restrict Ashlands position"), v.RestrictAshlands),
            AshlandsWidthRestriction = dataWidth != v.AshlandsWidthRestriction ? dataWidth : Float(S("Ashlands width restriction"), v.AshlandsWidthRestriction),
            AshlandsLengthRestriction = dataLength != v.AshlandsLengthRestriction ? dataLength : Float(S("Ashlands length restriction"), v.AshlandsLengthRestriction),
            AshlandsGap = Bool(S("Ashlands gap"), v.AshlandsGap) && Bool(D("Ashlands gap"), v.AshlandsGap),
            DeepNorthGap = Bool(S("Deep North gap"), v.DeepNorthGap) && Bool(D("Deep North gap"), v.DeepNorthGap),
            GridWidth = Int(keys.GetValueOrDefault(GridWidthKey), GridWidthFor(sizeInstalled, Int(S("Save system size"), 0), radius + edge)),
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
            StreamMaxAmount = Int(R("Stream max amount"), v.StreamMaxAmount),
            StreamSearchIterations = Int(R("Stream search iterations"), v.StreamSearchIterations),
            StreamStartMinAltitude = Float(R("Stream start min altitude"), v.StreamStartMinAltitude),
            StreamStartMaxAltitude = Float(R("Stream start max altitude"), v.StreamStartMaxAltitude),
            StreamEndMinAltitude = Float(R("Stream end min altitude"), v.StreamEndMinAltitude),
            StreamEndMaxAltitude = Float(R("Stream end max altitude"), v.StreamEndMaxAltitude),
            StreamMaxWidth = Float(R("Stream max width"), v.StreamMaxWidth),
            StreamMinLength = Float(R("Stream min length"), v.StreamMinLength),
            StreamMaxLength = Float(R("Stream max length"), v.StreamMaxLength),
            StreamCurveWidth = Float(R("Stream curve width"), v.StreamCurveWidth),
            StreamCurveWaveLength = Float(R("Stream curve wave length"), v.StreamCurveWaveLength),
            LegacyGeneration = Bool(D("Legacy generation"), v.LegacyGeneration),
            WiggleFrequency = Float(D("Wiggle frequency"), v.WiggleFrequency),
            Biomes = biomes ?? BiomeTable.Vanilla,
            BiomeData = biomes != null,
            World = world?.Rules,
            CheckSectors = world?.CheckSectors ?? false,
            Territories = data.Territories == null ? [] : TerritoriesOf(data.Territories, data.Notes),
        };
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
}
