using System.Text.Json.Serialization;

namespace ValheimGen;

public static partial class SeedLocations
{
    /// <summary>
    /// An alt biome (Dark Meadows, Lox Plains...): the areas of its biomes it may be given to (AltBiomeWorldData's
    /// GenerateAltBiomes, see <see cref="AltBiomeLayout"/>), and the location kinds and vegetation it keeps out of
    /// them. Each property is the game's field of the same name (m_ before it), its default the game's.
    /// </summary>
    public sealed class AltBiomeRule
    {
        /// <summary>The alt biome's name (what a kind or a vegetation rule it adds names as its parent).</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>Off: never given to an area.</summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>The biomes whose areas it may be given to (flags).</summary>
        [JsonPropertyName("biome")]
        public int Biome { get; set; }

        /// <summary>The nearest an area's middle may be to the world's middle, metres.</summary>
        [JsonPropertyName("minDistanceFromCenter")]
        public float MinDistanceFromCenter { get; set; } = 1000f;

        /// <summary>The areas it is given to before its chance counts.</summary>
        [JsonPropertyName("minAmountSpawned")]
        public int MinAmountSpawned { get; set; } = 1;

        /// <summary>The most areas it is given to.</summary>
        [JsonPropertyName("maxAmountSpawned")]
        public int MaxAmountSpawned { get; set; } = 10;

        /// <summary>The chance (0..1) an area that may have it gets it, past the minimum.</summary>
        [JsonPropertyName("chance")]
        public float Chance { get; set; } = 0.1f;

        /// <summary>Biomes an area must border (flags; see <see cref="AltBiomeLayout"/> for how the game reads them).</summary>
        [JsonPropertyName("requireNeighbor")]
        public int RequireNeighbor { get; set; }

        /// <summary>Biomes an area must not border (flags, read as <see cref="RequireNeighbor"/>).</summary>
        [JsonPropertyName("notNeighbor")]
        public int NotNeighbor { get; set; }

        /// <summary>Alt biomes it is never given to an area together with.</summary>
        [JsonPropertyName("incompatibleAltBiomes")]
        public List<string> IncompatibleAltBiomes { get; set; } = [];

        /// <summary>The fewest edge points an area may have.</summary>
        [JsonPropertyName("minEdgeSize")]
        public int MinEdgeSize { get; set; } = 50;

        /// <summary>One more than the most edge points an area may have.</summary>
        [JsonPropertyName("maxEdgeSize")]
        public int MaxEdgeSize { get; set; } = 1500;

        /// <summary>The lowest average height of an area's edge.</summary>
        [JsonPropertyName("minAvgHeight")]
        public float MinAvgHeight { get; set; } = 30f;

        /// <summary>Just above the highest average height of an area's edge.</summary>
        [JsonPropertyName("maxAvgHeight")]
        public float MaxAvgHeight { get; set; } = 10000f;

        /// <summary>When not 0: the greatest x an area's middle may have.</summary>
        [JsonPropertyName("belowWorldX")]
        public float BelowWorldX { get; set; }

        /// <summary>When not 0: the least x an area's middle may have.</summary>
        [JsonPropertyName("aboveWorldX")]
        public float AboveWorldX { get; set; }

        /// <summary>When not 0: the greatest z an area's middle may have.</summary>
        [JsonPropertyName("belowWorldY")]
        public float BelowWorldY { get; set; }

        /// <summary>When not 0: the least z an area's middle may have.</summary>
        [JsonPropertyName("aboveWorldY")]
        public float AboveWorldY { get; set; }

        /// <summary>Names (VegetationRule.name) of the vegetation it keeps out of its areas.</summary>
        [JsonPropertyName("blockVegetationNames")]
        public List<string> BlockVegetationNames { get; set; } = [];

        /// <summary>Names (Rule.name) of the location kinds it keeps out of its areas.</summary>
        [JsonPropertyName("blockLocationNames")]
        public List<string> BlockLocationNames { get; set; } = [];

        /// <summary>
        /// The biome whose ground look its areas take (AltBiome.m_terrainTextureOverride: the ground mask's colour of that
        /// biome); 0 (None) keeps the area's own.
        /// </summary>
        [JsonPropertyName("terrainTextureOverride")]
        public int TerrainTextureOverride { get; set; }
    }
}
