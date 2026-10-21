using ValheimGen;

namespace TerrainEditor.App;

// The height transform a biome or territory entry gives (the same six fields in both files).
public static partial class ExpandWorld
{
    /// <summary>
    /// An entry's height transform: its altitude multiplier and delta, water depth multiplier, lowest and highest
    /// altitudes kept whole and excess factor, each the mod's default when absent.
    /// </summary>
    /// <param name="f">The entry's fields.</param>
    private static HeightTransform HeightOf(Fields f) => new()
    {
        Multiplier = f.F("altitudeMultiplier", 1f),
        Delta = f.F("altitudeDelta", 0f),
        DepthMultiplier = f.F("waterDepthMultiplier", 1f),
        Minimum = f.F("minimumAltitude", -1000f),
        Maximum = f.F("maximumAltitude", 10000f),
        ExcessFactor = f.F("excessFactor", 0.5f),
    };
}
