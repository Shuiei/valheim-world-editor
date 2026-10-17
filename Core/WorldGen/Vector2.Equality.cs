#nullable disable

namespace ValheimGen;

/// <summary>The equality members of Vector2: exact component equality, hashing and the approximate ==.</summary>
internal partial struct Vector2 : IEquatable<Vector2>
{
    /// <summary>
    /// Unity's approximate equality: the points are less than 1e-5 apart. (Equals stays exact.)
    /// </summary>
    public static bool operator ==(Vector2 a, Vector2 b) => (a - b).SqrLength < 9.99999944E-11f;

    /// <summary>
    /// Not approximately equal (see ==).
    /// </summary>
    public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);

    /// <summary>Exact equality, unlike the approximate ==: equal when both components are equal (float.Equals each).</summary>
    public readonly bool Equals(Vector2 other) => X.Equals(other.X) && Y.Equals(other.Y);

    /// <summary>Equal to another vector with the same components.</summary>
    public override readonly bool Equals(object obj) => obj is Vector2 v && Equals(v);

    /// <inheritdoc/>
    public override readonly int GetHashCode() => X.GetHashCode() ^ (Y.GetHashCode() << 2);
}
