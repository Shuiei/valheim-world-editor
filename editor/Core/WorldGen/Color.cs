#nullable disable

namespace ValheimGen;

/// <summary>
/// Unity's Color: four 0..1 floats; the generator uses it for the base paint mask.
/// </summary>
internal struct Color : IEquatable<Color>
{
    /// <summary>
    /// Red.
    /// </summary>
    public float R;

    /// <summary>
    /// Green.
    /// </summary>
    public float G;

    /// <summary>
    /// Blue.
    /// </summary>
    public float B;

    /// <summary>
    /// Alpha.
    /// </summary>
    public float A;

    /// <summary>
    /// The colour (r, g, b, a).
    /// </summary>
    public Color(float r, float g, float b, float a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>
    /// Opaque black (0, 0, 0, 1): no base paint.
    /// </summary>
    public static Color Black => new(0f, 0f, 0f, 1f);

    /// <summary>Equal when each component is equal, the same as the default equality (float.Equals each).</summary>
    public readonly bool Equals(Color other) => R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B) && A.Equals(other.A);

    /// <summary>Equal to another colour with the same channels.</summary>
    public override readonly bool Equals(object obj) => obj is Color c && Equals(c);

    /// <summary>A hash of the channels, equal for equal colours.</summary>
    public override readonly int GetHashCode() => HashCode.Combine(R, G, B, A);
}
