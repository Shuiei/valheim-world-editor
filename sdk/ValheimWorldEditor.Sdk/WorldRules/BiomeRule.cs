using System.Collections.Generic;

namespace ValheimGen;

/// <summary>
/// One rule of Expand World Data's world file (expand_world.yaml): where a biome (or a territory, or forced alt biomes)
/// lies, as the mod's WorldEntry keeps it once converted. The generator walks the rules in order and the first one a
/// point passes names its biome; none: the ocean. Distances and centres are in world metres already (the file's
/// fractions of the radius times it); sectors are fractions of the circle from south, clockwise, wrapped to 0..1.
/// </summary>
public sealed record BiomeRule
{
    /// <summary>The biome (or biomes, as flags) the rule gives; none for a territory or alt biome rule.</summary>
    public Heightmap.Biome Biome { get; init; }

    /// <summary>The territory the rule gives (its name, as written), empty for none.</summary>
    public string Territory { get; init; } = "";

    /// <summary>The alt biomes the rule forces onto the biome sectors whose middle passes it; none by default.</summary>
    public IReadOnlyList<string> AltBiomes { get; init; } = [];

    /// <summary>The altitude (metres from the water level) a point must be under (exclusive; inclusive in the legacy evaluation).</summary>
    public float MaxAltitude { get; init; } = 10000f;

    /// <summary>The altitude a point must be over (exclusive; inclusive in the legacy evaluation).</summary>
    public float MinAltitude { get; init; } = -1000f;

    /// <summary>The distance from the middle (or the rule's centre) a point must be under, metres (at or past the radius: no limit).</summary>
    public float MaxDistance { get; init; } = 10000f;

    /// <summary>The distance a point must be over, metres (0: the middle itself passes).</summary>
    public float MinDistance { get; init; }

    /// <summary>The sector a point must be under, a fraction of the circle (1: no limit).</summary>
    public float MaxSector { get; init; } = 1f;

    /// <summary>The sector a point must be at or over, a fraction of the circle.</summary>
    public float MinSector { get; init; }

    /// <summary>The rule's own centre's x, metres (0 with CenterY 0: the world's middle).</summary>
    public float CenterX { get; init; }

    /// <summary>The rule's own centre's z, metres.</summary>
    public float CenterY { get; init; }

    /// <summary>How much of the area passes: under 1, a noise field decides (1: all of it).</summary>
    public float Amount { get; init; } = 1f;

    /// <summary>The noise field's stretch (1: as the land biomes').</summary>
    public float Stretch { get; init; } = 1f;

    /// <summary>The biome whose seed the noise field uses (the rule's biome's terrain, or the biome the file names as "seed").</summary>
    public Heightmap.Biome SeedBiome { get; init; }

    /// <summary>The noise field's own seed, when the file gives a number; null: <see cref="SeedBiome"/>'s.</summary>
    public int? Seed { get; init; }

    /// <summary>How many waves the distance limits ride on round the world (the mod's "Wiggle frequency", 20).</summary>
    public float WiggleDistanceLength { get; init; } = 20f;

    /// <summary>How far the distance limits wave, metres (the mod's "Wiggle width", 100; 0: no wave).</summary>
    public float WiggleDistanceWidth { get; init; } = 100f;

    /// <summary>How long the sector limits' wave is along the radius, metres (the mod's "Distance wiggle length", 500).</summary>
    public float WiggleSectorLength { get; init; } = 500f;

    /// <summary>How far the sector limits wave, a fraction of the circle (the mod's "Distance wiggle width", 0.01; 0: no wave).</summary>
    public float WiggleSectorWidth { get; init; } = 0.01f;

    /// <summary>Whether the rule has a rectangle (a size on either axis).</summary>
    public bool HasRectangle { get; init; }

    /// <summary>Half the rectangle's width, metres (infinite: no limit).</summary>
    public float HalfSizeX { get; init; } = float.PositiveInfinity;

    /// <summary>Half the rectangle's depth, metres (infinite: no limit).</summary>
    public float HalfSizeY { get; init; } = float.PositiveInfinity;

    /// <summary>The cosine of the rectangle's rotation.</summary>
    public float RotationCos { get; init; } = 1f;

    /// <summary>The sine of the rectangle's rotation.</summary>
    public float RotationSin { get; init; }

    /// <summary>How long the rectangle's edges' wave is, metres (500).</summary>
    public float WiggleRectangleLength { get; init; } = 500f;

    /// <summary>How far the rectangle's edges wave, metres (100; 0: no wave).</summary>
    public float WiggleRectangleWidth { get; init; } = 100f;

    /// <summary>
    /// How hot the water is past the rule's inner edge (the Ashlands' boiling sea): the rule's boiling times the metres
    /// past the edge over 300; 0 for none. With world rules, the generator's "is Ashlands" and ocean gradient are this.
    /// </summary>
    public float Boiling { get; init; }
}
