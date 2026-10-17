#nullable disable

namespace ValheimGen;

/// <summary>
/// Valheim's Vector2s: a pair of shorts (zone corners, as the biome caches key them).
/// </summary>
internal struct Vector2s : IEquatable<Vector2s>
{
    /// <summary>
    /// The x.
    /// </summary>
    public short X;

    /// <summary>
    /// The y (world z).
    /// </summary>
    public short Y;

    /// <summary>
    /// The pair (x, y), each cut to a short.
    /// </summary>
    public Vector2s(int x, int y)
    {
        X = (short)x;
        Y = (short)y;
    }

    /// <summary>
    /// The difference, cut to shorts.
    /// </summary>
    public static Vector2s operator -(Vector2s a, Vector2s b) => new(a.X - b.X, a.Y - b.Y);

    /// <inheritdoc/>
    public readonly bool Equals(Vector2s other) => X == other.X && Y == other.Y;

    /// <inheritdoc/>
    public override readonly bool Equals(object obj) => obj is Vector2s v && Equals(v);

    /// <inheritdoc/>
    public override readonly int GetHashCode() => (ushort)X | (Y << 16);
}
