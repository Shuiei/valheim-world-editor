using ValheimGen;

namespace TerrainEditor.App;

// The world rules from the world files' entries (expand_world.yaml).
public static partial class ExpandWorld
{
    /// <summary>
    /// The settings file's wave defaults a rule takes unless it says otherwise (the mod's names cross: a rule's distance
    /// wave takes "Wiggle frequency" and "Wiggle width", its sector wave "Distance wiggle length" and "Distance wiggle
    /// width").
    /// </summary>
    /// <param name="DistanceLength">"Distance wiggle length" (500).</param>
    /// <param name="DistanceWidth">"Distance wiggle width" (0.01).</param>
    /// <param name="Frequency">"Wiggle frequency" (20).</param>
    /// <param name="Width">"Wiggle width" (100).</param>
    internal sealed record Wiggles(float DistanceLength, float DistanceWidth, float Frequency, float Width)
    {
        /// <summary>The mod's defaults.</summary>
        public static Wiggles Default { get; } = new(500f, 0.01f, 20f, 100f);

        /// <summary>The settings file's values.</summary>
        public static Wiggles Of(IReadOnlyDictionary<string, string> cfg) => new(
            Float(cfg.GetValueOrDefault("Distance wiggle length"), 500f), Float(cfg.GetValueOrDefault("Distance wiggle width"), 0.01f),
            Float(cfg.GetValueOrDefault("Wiggle frequency"), 20f), Float(cfg.GetValueOrDefault("Wiggle width"), 100f));
    }

    /// <summary>
    /// The world rules from the world files' entries, converted as the mod's WorldEntry converts them (distances and
    /// centres times the radius, sectors wrapped, the waves' defaults filled in, the seed and the boiling resolved),
    /// and whether any rule limits its sector.
    /// </summary>
    /// <param name="entries">The entries, in order.</param>
    /// <param name="biomes">The world's biomes (for the names).</param>
    /// <param name="radius">The world's radius, metres.</param>
    /// <param name="wiggles">The settings file's wave defaults.</param>
    /// <param name="notes">Where a value not understood is noted.</param>
    internal static (List<BiomeRule> Rules, bool CheckSectors) WorldOf(IReadOnlyList<Entry> entries, BiomeTable biomes, float radius, Wiggles wiggles, List<string> notes)
    {
        List<BiomeRule> rules = [];
        bool sectors = false;
        foreach (var entry in entries)
        {
            var f = new Fields(entry, "world", notes);
            float minSector = f.F("minSector", 0f), maxSector = f.F("maxSector", 1f);
            sectors |= minSector != 0f || maxSector != 1f;
            rules.Add(RuleOf(f, biomes, radius, wiggles, notes));
        }
        return (rules, sectors);
    }

    /// <summary>One rule from its entry.</summary>
    private static BiomeRule RuleOf(Fields f, BiomeTable biomes, float radius, Wiggles wiggles, List<string> notes)
    {
        float Dist(float fraction) => (float)((double)fraction * radius);
        string biomeText = f.Str("biome");
        var biome = biomeText == "" ? Heightmap.Biome.None : biomes.Parse(biomeText, name => notes.Add($"expand_world: {name} is no biome (left out)"));
        var seedBiome = biomes.Terrain(biome);
        int? seed = Int(f.Str("seed"));
        if (seed == null && biomes.TryGet(f.Str("seed"), out var named))
        {
            seedBiome = named;
        }
        float minSector = f.F("minSector", 0f), maxSector = f.F("maxSector", 1f);
        float? sizeX = f.FOpt("sizeX"), sizeY = f.FOpt("sizeY");
        float radians = (float)((double)f.F("rotation", 0f) * Mathf.Deg2Rad);
        return new BiomeRule
        {
            Biome = biome,
            Territory = TerritoryKey(f.Str("territory")),
            AltBiomes = SplitList(f.Str("altBiome")),
            MaxAltitude = f.F("maxAltitude", 10000f),
            MinAltitude = f.F("minAltitude", -1000f),
            MaxDistance = Dist(f.F("maxDistance", 1f)),
            MinDistance = Dist(f.F("minDistance", 0f)),
            MaxSector = maxSector > 1f ? maxSector - 1f : maxSector,
            MinSector = minSector < 0f ? minSector + 1f : minSector,
            CenterX = Dist(f.F("centerX", 0f)),
            CenterY = Dist(f.F("centerY", 0f)),
            Amount = f.F("amount", 1f),
            Stretch = f.F("stretch", 1f),
            SeedBiome = seedBiome,
            Seed = seed,
            WiggleDistanceLength = f.FOpt("wiggleDistanceLength") ?? wiggles.Frequency,
            WiggleDistanceWidth = f.FOpt("wiggleDistanceWidth") ?? (f.B("wiggleDistance", true) ? wiggles.Width : 0f),
            WiggleSectorLength = f.FOpt("wiggleSectorLength") ?? wiggles.DistanceLength,
            WiggleSectorWidth = f.FOpt("wiggleSectorWidth") ?? (f.B("wiggleSector", true) ? wiggles.DistanceWidth : 0f),
            HasRectangle = sizeX.HasValue || sizeY.HasValue,
            HalfSizeX = sizeX is { } sx ? (float)((double)Dist(sx) / 2.0) : float.PositiveInfinity,
            HalfSizeY = sizeY is { } sy ? (float)((double)Dist(sy) / 2.0) : float.PositiveInfinity,
            RotationCos = Mathf.Cos(radians),
            RotationSin = Mathf.Sin(radians),
            WiggleRectangleLength = f.FOpt("wiggleRectangleLength") ?? wiggles.DistanceLength,
            WiggleRectangleWidth = f.B("wiggleRectangle", true) ? f.FOpt("wiggleRectangleWidth") ?? wiggles.Width : 0f,
            Boiling = BoilingOf(f.Str("boiling"), biome, biomes),
        };
    }

    /// <summary>
    /// A rule's boiling as the mod reads it: 1 when left out for a biome with lava (the Ashlands, or the biome file's),
    /// 1 for "true", a number as it is, else 0.
    /// </summary>
    private static float BoilingOf(string text, Heightmap.Biome biome, BiomeTable biomes)
    {
        if (text == "" && (biome & biomes.LavaBiomes) != 0)
        {
            return 1f;
        }
        return text == "true" ? 1f : Float(text) ?? 0f;
    }

    /// <summary>
    /// The mod's default world rules, what it writes to a missing world file (and so what a world has when the switch
    /// is on and no file exists yet). Not the game's layout: the Ashlands and the Deep North as plain circles, the ocean
    /// under -26 m, the mountains above 50 m.
    /// </summary>
    internal static readonly IReadOnlyList<Entry> DefaultWorld =
    [
        Rule(("biome", "ashlands"), ("centerY", "0.4"), ("minDistance", "1.2"), ("maxDistance", "1.6"), ("boiling", "true")),
        Rule(("biome", "ocean"), ("maxAltitude", "-26")),
        Rule(("biome", "deepnorth"), ("centerY", "-0.4"), ("minDistance", "1.2"), ("maxDistance", "1.6")),
        Rule(("biome", "mountain"), ("minAltitude", "50")),
        Rule(("biome", "swamp"), ("wiggleDistance", "false"), ("minDistance", "0.2"), ("maxDistance", "0.6"), ("minAltitude", "-20"), ("maxAltitude", "20"), ("amount", "0.4")),
        Rule(("biome", "mistlands"), ("minDistance", "0.6"), ("amount", "0.6")),
        Rule(("biome", "plains"), ("minDistance", "0.3"), ("maxDistance", "0.8"), ("amount", "0.6")),
        Rule(("biome", "blackforest"), ("minDistance", "0.06"), ("maxDistance", "0.6"), ("amount", "0.6")),
        Rule(("biome", "blackforest"), ("minDistance", "0.5")),
        Rule(("biome", "meadows")),
    ];

    /// <summary>A rule entry from its fields.</summary>
    private static Entry Rule(params (string Key, string Value)[] fields) => new(fields.ToDictionary(f => f.Key, f => (object?)f.Value));
}
