using System.Collections.Generic;

namespace ValheimGen;

/// <summary>
/// A territory of Expand World Data's territory file (expand_territories.yaml): a named area the world rules pick (a
/// rule's "territory"), with its own height transform laid over the biome's, its own colours and forest. Only
/// territories the file gives something to are kept (the mod's TerritoryData.IsValid).
/// </summary>
public sealed record Territory
{
    /// <summary>The territory's name, trimmed and lower-cased (the rules name it the same way).</summary>
    public required string Name { get; init; }

    /// <summary>How its ground is reshaped, after the biome's own transform.</summary>
    public HeightTransform Height { get; init; } = HeightTransform.None;

    /// <summary>Divides its forest factor (1: as the biome's).</summary>
    public float ForestMultiplier { get; init; } = 1f;

    /// <summary>Its ground colour (the mask), when the file gives one.</summary>
    public Rgba? TerrainColor { get; init; }

    /// <summary>Its colour on the map, when the file gives one.</summary>
    public Rgba? MapColor { get; init; }

    /// <summary>Its water colours, over its biome's.</summary>
    public WaterColours Water { get; init; } = WaterColours.None;

    /// <summary>The status effects it puts on players, in place of its biome's when it has any.</summary>
    public IReadOnlyList<StatusEffect> StatusEffects { get; init; } = [];

    /// <summary>Whether building is forbidden in it.</summary>
    public bool NoBuild { get; init; }
}
