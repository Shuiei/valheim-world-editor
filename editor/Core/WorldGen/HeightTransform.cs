using System.Text.Json.Serialization;

namespace ValheimGen;

/// <summary>
/// How Expand World Data reshapes a biome's or a territory's ground: its postfix on the game's GetBiomeHeight takes
/// the height as an altitude (metres from the water level), multiplies it, adds the delta, multiplies what is under the
/// water by the depth multiplier, then softens what lies past the maximum or minimum altitude (the excess to the power
/// of the factor, added with the factor's sign). <see cref="None"/> is the mod's defaults, which leave a height as it
/// is but for the rounding of the metres taken off and put back.
/// </summary>
public sealed record HeightTransform
{
    /// <summary>The mod's defaults: a height stays as it is.</summary>
    public static HeightTransform None { get; } = new();

    /// <summary>Multiplies the altitude (1: as it is).</summary>
    public float Multiplier { get; init; } = 1f;

    /// <summary>Added to the altitude, metres (0: nothing).</summary>
    public float Delta { get; init; }

    /// <summary>Multiplies the altitude when it is under the water (1: as it is).</summary>
    public float DepthMultiplier { get; init; } = 1f;

    /// <summary>The lowest altitude kept whole, metres; what lies under is softened (-1000: nothing is).</summary>
    public float Minimum { get; init; } = -1000f;

    /// <summary>The highest altitude kept whole, metres; what lies over is softened (10000: nothing is).</summary>
    public float Maximum { get; init; } = 10000f;

    /// <summary>
    /// The power the excess over the maximum (or under the minimum) is raised to, its sign kept apart: the mod uses its
    /// size as the power and its sign as the direction (0.5: the square root, added).
    /// </summary>
    public float ExcessFactor { get; init; } = 0.5f;

    /// <summary>Whether this transform changes nothing (the mod's defaults).</summary>
    [JsonIgnore]
    public bool IsIdentity => Multiplier == 1f && Delta == 0f && DepthMultiplier == 1f && Minimum == -1000f && Maximum == 10000f;

    /// <summary>
    /// An altitude (metres from the water level) transformed, as the mod's ApplyHeight computes it (floats, each step
    /// rounded as its store rounds it).
    /// </summary>
    /// <param name="altitude">The altitude, metres from the water level.</param>
    public float Apply(float altitude)
    {
        float factor = Mathf.Abs(ExcessFactor), sign = Mathf.Sign(ExcessFactor);
        float h = (float)((double)altitude * Multiplier);
        h = (float)((double)h + Delta);
        if (h < 0f)
        {
            h = (float)((double)h * DepthMultiplier);
        }
        if (h > Maximum)
        {
            h = (float)((double)Maximum + (double)sign * Mathf.Pow((float)((double)h - Maximum), factor));
        }
        if (h < Minimum)
        {
            h = (float)((double)Minimum - (double)sign * Mathf.Pow((float)((double)Minimum - h), factor));
        }
        return h;
    }
}
