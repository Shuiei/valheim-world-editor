using ValheimGen;

namespace WorldEditor.Tests;

/// <summary>
/// The seed the alt biome layout is checked on (FzdYdU9HGv, whose areas and alt biomes the game wrote to
/// tests/fixtures/altbiomes-reference.txt): its generator and biome point map, made once for a test class.
/// </summary>
public sealed class AltBiomeSeed
{
    /// <summary>The seed's name.</summary>
    public const string Name = "FzdYdU9HGv";

    /// <summary>The seed's generator.</summary>
    public WorldGenerator Gen { get; } = TerrainEditor.App.SeedPreview.Generator(Name);

    /// <summary>The seed's biome point map.</summary>
    internal SeedLocations.BiomeMap Map { get; }

    /// <summary>Makes the generator and the map.</summary>
    public AltBiomeSeed() => Map = new SeedLocations.BiomeMap(Gen, parallel: true, CancellationToken.None);
}
