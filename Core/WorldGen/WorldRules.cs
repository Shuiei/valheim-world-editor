namespace ValheimGen;

/// <summary>
/// What a world generation mod can change about how a world is made: the Expand World Size settings (radius,
/// stretch, altitude) and the Expand World Rivers settings (water level, lakes, rivers, streams). The generator
/// reads every value the game has as a literal from here; <see cref="Vanilla"/> gives the game's own, and with
/// it every formula collapses to the literal it replaces (a generator with <see cref="Vanilla"/> is the game's
/// to the bit, see WorldGeneratorReferenceTests). The editor reads them from the world file and the mods' settings
/// files (TerrainEditor.App.ExpandWorld).
/// </summary>
/// <remarks>
/// Each value is named after the mod's setting and keeps its unit: metres, or a multiplier. Where the mod puts a
/// value in the game's code in a particular floating-point type (a float where the game had 10000f, a double
/// where it had 6000.0), the generator computes it in that type at the place of use, so this record only
/// carries the settings themselves.
/// </remarks>
public sealed record WorldRules
{
    /// <summary>The game's own values.</summary>
    public static WorldRules Vanilla { get; } = new();

    // ---- Which mods are installed: a few of their patches apply even at their default settings.
    /// <summary>
    /// Expand World Size is installed. Its Ashlands' edge sink is then at the total radius plus 150 m (10650 m at the
    /// defaults) instead of the game's 10150 m: the mod computes it from the total radius and applies it whatever the
    /// settings.
    /// </summary>
    public bool SizeInstalled { get; init; }

    /// <summary>Expand World Data is installed (its data files change the biomes, locations and vegetation).</summary>
    public bool DataInstalled { get; init; }

    /// <summary>Expand World Rivers is installed (its settings change the water level, lakes, rivers and streams).</summary>
    public bool RiversInstalled { get; init; }

    // ---- Expand World Size.
    /// <summary>The world's radius, metres (the game's 10000): the land's reach, where the sea floor starts to drop.</summary>
    public float Radius { get; init; } = 10000f;

    /// <summary>The edge's width past the radius, metres (the game's 500): the sea floor drops to -400 m over it.</summary>
    public float EdgeSize { get; init; } = 500f;

    /// <summary>The radius and the edge together (the game's 10500 m).</summary>
    public float TotalRadius => Radius + EdgeSize;

    /// <summary>
    /// The world stretched over a bigger area: every coordinate the generator reads is divided by it (1: as the
    /// game).
    /// </summary>
    public float Stretch { get; init; } = 1f;

    /// <summary>The land biomes' noise fields stretched, on top of <see cref="Stretch"/> (1: as the game).</summary>
    public float BiomeStretch { get; init; } = 1f;

    /// <summary>The radius in the generator's stretched coordinates (the mod's StrechedWorldRadius).</summary>
    public float StretchedRadius => Radius / Stretch;

    /// <summary>The total radius in the generator's stretched coordinates.</summary>
    public float StretchedTotalRadius => TotalRadius / Stretch;

    /// <summary>Multiplies the continents' height above and below the water level (1: as the game).</summary>
    public float AltitudeMultiplier { get; init; } = 1f;

    /// <summary>Added to the continents' height, metres (0: as the game).</summary>
    public float AltitudeDelta { get; init; }

    /// <summary>Multiplies the ground's depth under the water level (1: as the game).</summary>
    public float WaterDepthMultiplier { get; init; } = 1f;

    /// <summary>Divides the forest factor: more forest with a higher value (1: as the game).</summary>
    public float ForestMultiplier { get; init; } = 1f;

    /// <summary>Multiplies every location kind's quantity (1: as the game).</summary>
    public float LocationsMultiplier { get; init; } = 1f;

    /// <summary>Replaces the seed's first continent offset in the continents' height (null: the seed's own).</summary>
    public float? OffsetX { get; init; }

    /// <summary>Replaces the seed's second continent offset in the continents' height (null: the seed's own).</summary>
    public float? OffsetY { get; init; }

    /// <summary>
    /// Replaces the seed's detail offset in the biomes' heights, on their x coordinate (null: the seed's own). The mod
    /// replaces the first use of the offset in each biome's height, which is the x coordinate's; the z coordinate
    /// keeps the seed's.
    /// </summary>
    public float? HeightSeed { get; init; }

    /// <summary>Whether the Ashlands' terrain is limited to a band near the southern edge (true: as the game).</summary>
    public bool RestrictAshlands { get; init; } = true;

    /// <summary>How wide the Ashlands' band is, metres east to west (the game's 7500).</summary>
    public float AshlandsWidthRestriction { get; init; } = 7500f;

    /// <summary>How deep the Ashlands' band is, metres from the edge (the game's 1000).</summary>
    public float AshlandsLengthRestriction { get; init; } = 1000f;

    /// <summary>Whether the ground sinks to the sea along the Ashlands' edge (true: as the game).</summary>
    public bool AshlandsGap { get; init; } = true;

    /// <summary>Whether the ground sinks to the sea along the Deep North's edge (true: as the game).</summary>
    public bool DeepNorthGap { get; init; } = true;

    /// <summary>
    /// The save's zone grid, zones per side (the game's 512): the chunk files cover this many zones; more with a
    /// bigger world (up to 2048).
    /// </summary>
    public int GridWidth { get; init; } = 512;

    // ---- Expand World Rivers.
    /// <summary>The water's height, metres (the game's 30).</summary>
    public float WaterLevel { get; init; } = 30f;

    /// <summary>How far apart the points scanned for lakes are, metres (the game's 128).</summary>
    public float LakeSearchInterval { get; init; } = 128f;

    /// <summary>How deep under the water level a point must be to count as a lake, metres (the game's -20).</summary>
    public float LakeDepth { get; init; } = -20f;

    /// <summary>Lake points within this distance are one lake, metres (the game's 800).</summary>
    public float LakeMergeRadius { get; init; } = 800f;

    /// <summary>Lakes within this distance get a river between them, metres (the game's 2000).</summary>
    public float LakeMaxDistance1 { get; init; } = 2000f;

    /// <summary>A lake without a river looks this far for one, metres (the game's 5000).</summary>
    public float LakeMaxDistance2 { get; init; } = 5000f;

    /// <summary>Whether rivers are laid out at all (true: as the game).</summary>
    public bool Rivers { get; init; } = true;

    /// <summary>How far apart the points checked along a river's way are, metres (the game's 128).</summary>
    public float RiverCheckInterval { get; init; } = 128f;

    /// <summary>The highest ground a river may cross, metres above the water level (the game's 50).</summary>
    public float RiverMaxAltitude { get; init; } = 50f;

    /// <summary>The narrowest a river's narrowest point may be, metres (the game's 60).</summary>
    public float RiverMinWidth { get; init; } = 60f;

    /// <summary>The widest a river's widest point may be, metres (the game's 100).</summary>
    public float RiverMaxWidth { get; init; } = 100f;

    /// <summary>A river's waves swing a this-th of its length sideways (the game's 15).</summary>
    public float RiverCurveWidth { get; init; } = 15f;

    /// <summary>A river's waves are a this-th of its length long (the game's 20).</summary>
    public float RiverCurveWaveLength { get; init; } = 20f;

    /// <summary>Replaces the seed's river seed (null: the seed's own).</summary>
    public int? RiverSeed { get; init; }

    /// <summary>Whether streams are laid out at all (true: as the game).</summary>
    public bool Streams { get; init; } = true;

    /// <summary>Replaces the seed's stream seed (null: the seed's own).</summary>
    public int? StreamSeed { get; init; }

    /// <summary>How many streams are tried (the game's 3000).</summary>
    public int StreamMaxAmount { get; init; } = 3000;

    /// <summary>How many random points are tried for a stream's start, and for its end (the game's 100).</summary>
    public int StreamSearchIterations { get; init; } = 100;

    /// <summary>A stream starts above this height, metres from the water level (the game's -4).</summary>
    public float StreamStartMinAltitude { get; init; } = -4f;

    /// <summary>A stream starts below this height, metres from the water level (the game's 1).</summary>
    public float StreamStartMaxAltitude { get; init; } = 1f;

    /// <summary>A stream ends above this height, metres from the water level (the game's 6).</summary>
    public float StreamEndMinAltitude { get; init; } = 6f;

    /// <summary>A stream ends below this height, metres from the water level (the game's 14).</summary>
    public float StreamEndMaxAltitude { get; init; } = 14f;

    /// <summary>A stream's widest point, metres (the game's 20; its narrowest stays the game's 20).</summary>
    public float StreamMaxWidth { get; init; } = 20f;

    /// <summary>The shortest a stream is tried at, metres (the game's 80).</summary>
    public float StreamMinLength { get; init; } = 80f;

    /// <summary>The longest a stream is tried at, metres (the game's 200).</summary>
    public float StreamMaxLength { get; init; } = 200f;

    /// <summary>A stream's waves swing a this-th of its length sideways (the game's 15).</summary>
    public float StreamCurveWidth { get; init; } = 15f;

    /// <summary>A stream's waves are a this-th of its length long (the game's 20).</summary>
    public float StreamCurveWaveLength { get; init; } = 20f;

    // ---- Derived.
    /// <summary>Whether these are the game's own values (nothing of the generator changes).</summary>
    public bool IsVanilla => Equals(Vanilla);

    /// <summary>
    /// A height in metres as the generator's units (metres over 200): the mod's conversion, in float.
    /// </summary>
    internal static float ToBaseHeight(float metres) => metres / 200f;

    /// <summary>A height above the water level, metres, as a height in metres: the mod's conversion, in float.</summary>
    internal float AltitudeToHeight(float altitude) => WaterLevel + altitude;

    /// <summary>A height above the water level, metres, as the generator's units: the mod's conversion, in float.</summary>
    internal float AltitudeToBaseHeight(float altitude) => ToBaseHeight(AltitudeToHeight(altitude));

    /// <summary>
    /// What tells these rules from others, for caches keyed by a world's generator: empty for the game's own values.
    /// </summary>
    public string Stamp => IsVanilla ? "" : ToString();
}
