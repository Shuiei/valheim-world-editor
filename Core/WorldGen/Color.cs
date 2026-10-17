#nullable disable

namespace ValheimGen;

/// <summary>
/// Unity's Color: four 0..1 floats; the generator uses it for the base paint mask.
/// </summary>
public struct Color : IEquatable<Color>
{
    /// <summary>
    /// Red.
    /// </summary>
    public float r;

    /// <summary>
    /// Green.
    /// </summary>
    public float g;

    /// <summary>
    /// Blue.
    /// </summary>
    public float b;

    /// <summary>
    /// Alpha.
    /// </summary>
    public float a;

    /// <summary>
    /// The colour (r, g, b, a).
    /// </summary>
    public Color(float r, float g, float b, float a)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }

    /// <summary>
    /// Opaque black (0, 0, 0, 1): no base paint.
    /// </summary>
    public static Color black => new(0f, 0f, 0f, 1f);

    /// <summary>Equal when each component is equal, the same as the default equality (float.Equals each).</summary>
    public readonly bool Equals(Color other) => r.Equals(other.r) && g.Equals(other.g) && b.Equals(other.b) && a.Equals(other.a);

    /// <summary>Equal to another colour with the same channels.</summary>
    public override readonly bool Equals(object obj) => obj is Color c && Equals(c);

    /// <summary>A hash of the channels, equal for equal colours.</summary>
    public override readonly int GetHashCode() => HashCode.Combine(r, g, b, a);
}
