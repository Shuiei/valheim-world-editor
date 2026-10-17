#nullable disable

namespace ValheimGen;

/// <summary>
/// Unity's Mathf: the few helpers the generator calls, with sine and cosine worked out in double.
/// </summary>
public static partial class Mathf
{
    /// <summary>
    /// value held to min..max.
    /// </summary>
    public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

    /// <summary>
    /// value held to min..max.
    /// </summary>
    public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
}
