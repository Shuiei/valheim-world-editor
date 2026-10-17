#nullable disable

namespace ValheimGen;

/// <summary>The equality members of Vector3: exact component equality, hashing and the approximate ==.</summary>
public partial struct Vector3 : IEquatable<Vector3>
{
    /// <summary>Equal when each component is equal, the same as the default equality (float.Equals each).</summary>
    public readonly bool Equals(Vector3 other) => x.Equals(other.x) && y.Equals(other.y) && z.Equals(other.z);

    /// <summary>Equal to another vector with the same components.</summary>
    public override readonly bool Equals(object obj) => obj is Vector3 v && Equals(v);

    /// <summary>A hash of the components, equal for equal vectors.</summary>
    public override readonly int GetHashCode() => HashCode.Combine(x, y, z);
}
