using System.Text.Json.Serialization;

namespace ValheimGen;

/// <summary>
/// One biome as the editor knows it: one of the game's nine, or one Expand World Data's biome file adds (a new flag
/// value past Mistlands). What the file says about it: its name, the biome whose ground shape and look it borrows, its
/// colours, its height transform, its lava.
/// </summary>
public sealed record BiomeInfo
{
    /// <summary>The biome's flag value (the game's own for its nine; 0x400, 0x800... then 0x80 for the file's).</summary>
    public required Heightmap.Biome Value { get; init; }

    /// <summary>The biome's name as the file names it (the game's own names for its nine: "Meadows", "BlackForest"...).</summary>
    public required string Id { get; init; }

    /// <summary>The name shown to players when the file gives one ("name"), else empty.</summary>
    public string Name { get; init; } = "";

    /// <summary>The biome whose ground shape and ground look it uses (itself unless the file says "terrain").</summary>
    public Heightmap.Biome Terrain { get; init; }

    /// <summary>The biome whose plants and planting rules it uses (its terrain's unless the file says "nature").</summary>
    public Heightmap.Biome Nature { get; init; }

    /// <summary>
    /// Whether the file gives the biome anything beyond the defaults (the mod's BiomeData.IsValid: a height transform,
    /// a map colour, a forest or map colour multiplier, a lava amount, status effects): only then do its height transform,
    /// its map colour and its lava amount apply.
    /// </summary>
    public bool HasData { get; init; }

    /// <summary>How its ground is reshaped (the mod's defaults for the game's own).</summary>
    public HeightTransform Height { get; init; } = HeightTransform.None;

    /// <summary>Divides its forest factor (1: as the game).</summary>
    public float ForestMultiplier { get; init; } = 1f;

    /// <summary>Multiplies its height above the water on the map's height texture (1: as the game).</summary>
    public float MapColorMultiplier { get; init; } = 1f;

    /// <summary>Its colour on the map ("colorMap"), when the file gives one.</summary>
    public Rgba? MapColor { get; init; }

    /// <summary>Its ground colour, the mask the ground shader reads ("colorTerrain"), when the file gives one.</summary>
    public Rgba? TerrainColor { get; init; }

    /// <summary>The paint its ground gets instead of the generated mask ("paint"), when the file gives one.</summary>
    public Rgba? Paint { get; init; }

    /// <summary>Whether its ground has lava (boiling water at its edge, heat): the Ashlands by default.</summary>
    public bool Lava { get; init; }

    /// <summary>How much of the lava the mask keeps (1: all of it, as the game).</summary>
    public float LavaAmount { get; init; } = 1f;

    /// <summary>Its water colours (used only when <see cref="HasData"/>: the mod keeps no data of a biome without).</summary>
    public WaterColours Water { get; init; } = WaterColours.None;

    /// <summary>The status effects it puts on players (used only when <see cref="HasData"/>, as its water colours).</summary>
    public IReadOnlyList<StatusEffect> StatusEffects { get; init; } = [];

    /// <summary>How stretched the lava noise is ("lavaStretch", 1: as the game); the generator divides 0.01 by it.</summary>
    public float LavaStretch { get; init; } = 1f;

    /// <summary>Whether this is one of the game's nine with none of the file's settings.</summary>
    [JsonIgnore]
    public bool IsGames => Terrain == Value && Nature == Value && !HasData && MapColor == null && TerrainColor == null && Paint == null && Name == "";
}
