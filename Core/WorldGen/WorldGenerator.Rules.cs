namespace ValheimGen;

// What a world generation mod changed about the world (Expand World Size and Rivers), as the generator's
// formulas take it. Each value stands in for a literal of the game's code, in that literal's floating-point
// type, computed as the mod computes it: at the game's own values, each is the literal to the bit, so a world
// without a mod is the game's exactly (the reference tests say so).
public sealed partial class WorldGenerator
{
    /// <summary>
    /// What a world generation mod changed about this world (the game's own values by default).
    /// </summary>
    public WorldRules Rules { get; }

    /// <summary>
    /// The rules' values where the generator's formulas use them.
    /// </summary>
    private readonly RuleValues _v;

    /// <summary>
    /// The world's radius in the generator's coordinates (metres; the game's 10000, or the rules' radius over
    /// their stretch): past it, the sea deepens to the edge of the world.
    /// </summary>
    public float WorldSize => _v.WorldSize;

    /// <summary>
    /// A world coordinate as the generator's formulas take it: divided by the rules' stretch (Expand World
    /// Size's prefix on the game's GetBiome, GetBiomeHeight and the map's mask colour). The same coordinate
    /// with the game's own rules.
    /// </summary>
    internal float Stretched(float v) => v / _v.Stretch;

    /// <summary>
    /// How much of a forest the game would grow here (lower: denser; under 1.15 counts as forest). Given the
    /// point as the game's callers give it: its zone system asks with the world position as it is, its map with
    /// the point stretched (<see cref="Stretched"/>). Expand World Size's forest multiplier divides it (its
    /// postfix, applied only when set).
    /// </summary>
    internal float GetForestFactor(Vector3 pos) => _v.Forest(GenMath.Layers(pos * 0.01f * 0.4f, 3, 1.6f, 0.7f));

    /// <summary>
    /// Whether the game counts a point as forest (<see cref="GetForestFactor"/> under 1.15).
    /// </summary>
    internal bool InForest(Vector3 pos) => GetForestFactor(pos) < 1.15f;

    /// <summary>
    /// The rules as the formulas take them, and the seed's draws the rules replace. Where the mod leaves the game's
    /// code alone at a setting's default and its formula would round differently from the literal, a switch
    /// (AltitudeOn, WaterDepthOn, ForestOn) says whether it is applied at all, as the mod only applies it when the
    /// setting is set.
    /// </summary>
    private sealed partial class RuleValues
    {
        /// <summary>The values for a world's rules.</summary>
        /// <param name="r">The rules.</param>
        /// <param name="offsetA">The seed's first continent offset.</param>
        /// <param name="offsetB">The seed's second continent offset.</param>
        /// <param name="offsetDetail">The seed's detail offset.</param>
        /// <param name="riverSeed">The seed's river seed.</param>
        /// <param name="streamSeed">The seed's stream seed.</param>
        public RuleValues(WorldRules r, float offsetA, float offsetB, float offsetDetail, int riverSeed, int streamSeed)
        {
            float sr = r.StretchedRadius;
            Radius = r.Radius;
            Stretch = r.Stretch;
            BiomeStretch = r.BiomeStretch;
            WorldSize = sr;
            EdgeOfWorld = r.StretchedTotalRadius;
            EdgeInner = (r.TotalRadius - 10f) / r.Stretch;
            // The Ashlands' edge circle: the game's two static fields (12000 and -4000), the mod's float products.
            AshlandsMinDistance = 1.2f * sr;
            AshlandsYOffset = -0.4f * sr;
            // The Deep North's edge circle as IsDeepnorth has it (4000.0 and 12000.0, doubles)...
            DeepNorthShift = 0.4 * sr;
            DeepNorthRadius = 1.2 * sr;
            // ...and as its gap has it (4000f, 12000.0 from a float product, 400.0).
            DeepNorthGapShift = 0.4f * sr;
            DeepNorthGapRadius = 1.2f * sr;
            DeepNorthGapWidth = 0.04 * sr;
            AshlandsGapWidth = 0.04 * sr;
            AshlandsGap = r.AshlandsGap;
            DeepNorthGap = r.DeepNorthGap;
            // The Ashlands' sink to the sea floor: the mod computes it from the total radius whatever the settings.
            AshlandsSink = r.SizeInstalled ? (r.TotalRadius + 150f) / r.Stretch : 10150.0;
            AshlandsWidth = r.RestrictAshlands ? r.AshlandsWidthRestriction : double.PositiveInfinity;
            AshlandsLength = r.RestrictAshlands ? r.AshlandsLengthRestriction : double.PositiveInfinity;
            // The land biomes' distance bands: fractions of the stretched radius, in the game's literals' types.
            SwampMin = 0.2f * sr;
            MistMin = 0.6 * sr;
            MistMax = sr;
            PlainsMin = 0.3 * sr;
            PlainsMax = 0.8f * sr;
            ForestMin = 0.06 * sr;
            ForestMax = 0.6f * sr;
            MeadowsEdge = 0.5 * sr;
            AltitudeOn = r.AltitudeDelta != 0f || r.AltitudeMultiplier != 1f;
            BaseWater = WorldRules.ToBaseHeight(r.WaterLevel);
            AltitudeMultiplier = r.AltitudeMultiplier;
            BaseAltitudeDelta = WorldRules.ToBaseHeight(r.AltitudeDelta);
            WaterLevel = r.WaterLevel;
            WaterDepth = r.WaterDepthMultiplier;
            WaterDepthOn = r.WaterDepthMultiplier != 1f;
            ForestMultiplier = r.ForestMultiplier == 0f ? 1f : r.ForestMultiplier;
            ForestOn = ForestMultiplier != 1f;
            SetSeeds(r, offsetA, offsetB, offsetDetail, riverSeed, streamSeed);
            SetWater(r);
            SetBiomes(r);
        }

        /// <summary>The world's radius in world metres (the lakes and streams are laid out over it).</summary>
        public float Radius { get; }
        /// <summary>The world's stretch: world coordinates over it are the generator's.</summary>
        public float Stretch { get; }
        /// <summary>The land biomes' noise fields' stretch (a double: the mod divides the fields' double sums by it).</summary>
        public double BiomeStretch { get; }
        /// <summary>The radius in the generator's coordinates (the game's 10000f).</summary>
        public float WorldSize { get; }
        /// <summary>The edge of the world in the generator's coordinates (the game's 10500f).</summary>
        public float EdgeOfWorld { get; }
        /// <summary>Where the sea floor's last drop starts (the game's 10490f).</summary>
        public float EdgeInner { get; }
        /// <summary>The Ashlands' edge circle's radius (the game's static field, 12000f).</summary>
        public float AshlandsMinDistance { get; }
        /// <summary>The Ashlands' edge circle's middle, metres south (the game's static field, -4000f).</summary>
        public float AshlandsYOffset { get; }
        /// <summary>The Deep North's edge circle's middle, metres north, as IsDeepnorth has it (4000.0).</summary>
        public double DeepNorthShift { get; }
        /// <summary>The Deep North's edge circle's radius as IsDeepnorth has it (12000.0).</summary>
        public double DeepNorthRadius { get; }
        /// <summary>The Deep North's edge circle's middle as its gap has it (4000f).</summary>
        public float DeepNorthGapShift { get; }
        /// <summary>The Deep North's edge circle's radius as its gap has it (12000.0).</summary>
        public double DeepNorthGapRadius { get; }
        /// <summary>How far from the Deep North's edge the ground sinks (400.0).</summary>
        public double DeepNorthGapWidth { get; }
        /// <summary>How far from the Ashlands' edge the ground sinks (400.0).</summary>
        public double AshlandsGapWidth { get; }
        /// <summary>Whether the ground sinks along the Ashlands' edge.</summary>
        public bool AshlandsGap { get; }
        /// <summary>Whether the ground sinks along the Deep North's edge.</summary>
        public bool DeepNorthGap { get; }
        /// <summary>Where the Ashlands sink to the sea floor (the game's 10150.0).</summary>
        public double AshlandsSink { get; }
        /// <summary>How far east and west of the middle line the Ashlands' crags reach (7500.0; infinite: no limit).</summary>
        public double AshlandsWidth { get; }
        /// <summary>How far from the edge the Ashlands' crags reach (1000.0; infinite: no limit).</summary>
        public double AshlandsLength { get; }
        /// <summary>The swamps' least distance from the middle (2000f).</summary>
        public float SwampMin { get; }
        /// <summary>The mists' least distance, before the edge wave (6000.0).</summary>
        public double MistMin { get; }
        /// <summary>The mists' greatest distance (10000f).</summary>
        public float MistMax { get; }
        /// <summary>The plains' least distance, before the edge wave (3000.0).</summary>
        public double PlainsMin { get; }
        /// <summary>The plains' greatest distance (8000f).</summary>
        public float PlainsMax { get; }
        /// <summary>The black forest's least distance, before the edge wave (600.0).</summary>
        public double ForestMin { get; }
        /// <summary>The black forest's greatest distance (6000f).</summary>
        public float ForestMax { get; }
        /// <summary>Past it (plus the edge wave), the plain land is black forest instead of meadows (5000.0).</summary>
        public double MeadowsEdge { get; }
        /// <summary>Whether the continents' height is transformed (the altitude delta or multiplier is set).</summary>
        public bool AltitudeOn { get; }
        /// <summary>The water level in the generator's units.</summary>
        public float BaseWater { get; }
        /// <summary>The continents' height multiplier about the water level.</summary>
        public float AltitudeMultiplier { get; }
        /// <summary>Added to the continents' height, generator units.</summary>
        public float BaseAltitudeDelta { get; }
        /// <summary>The water level, metres.</summary>
        public float WaterLevel { get; }
        /// <summary>The ground's depth multiplier under the water level.</summary>
        public float WaterDepth { get; }
        /// <summary>Whether the depth multiplier is applied (it is set).</summary>
        public bool WaterDepthOn { get; }
        /// <summary>The forest factor's divisor.</summary>
        public float ForestMultiplier { get; }
        /// <summary>Whether the forest factor is divided (the multiplier is set).</summary>
        public bool ForestOn { get; }
    }
}
