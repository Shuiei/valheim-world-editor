
namespace ValheimGen;

/// <summary>
/// Expand World Data's water colours of a biome or a territory ("colorWaterTop", "colorWaterBottom",
/// "colorWaterShallow", "colorWaterSurface"): the colours the game's water material takes while the player is there,
/// with the mod's "Custom water color" on. Each null when the file does not give it.
/// </summary>
/// <param name="Top">The water's colour near its surface.</param>
/// <param name="Bottom">The deep water's colour.</param>
/// <param name="Shallow">The bottom's colour through shallow water.</param>
/// <param name="Surface">The surface's colour.</param>
public sealed record WaterColours(Rgba? Top = null, Rgba? Bottom = null, Rgba? Shallow = null, Rgba? Surface = null)
{
    /// <summary>No colour given: the game's own water.</summary>
    public static readonly WaterColours None = new();

    /// <summary>Whether no colour is given.</summary>
    public bool IsNone => Top == null && Bottom == null && Shallow == null && Surface == null;

    /// <summary>These colours with another's given ones over them (the mod's territory colours over the biome's).</summary>
    /// <param name="other">The colours laid over these; null for none.</param>
    public WaterColours Under(WaterColours? other) =>
        other == null ? this : new(other.Top ?? Top, other.Bottom ?? Bottom, other.Shallow ?? Shallow, other.Surface ?? Surface);
}
