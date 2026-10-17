#nullable disable

namespace ValheimGen;

/// <summary>
/// Valheim's Vector2s: a pair of shorts (zone corners, as the biome caches key them).
/// </summary>
public struct Vector2s : IEquatable<Vector2s>
{
    /// <summary>
    /// The x.
    /// </summary>
    public short x;

    /// <summary>
    /// The y (world z).
    /// </summary>
    public short y;

    /// <summary>
    /// The pair (x, y), each cut to a short.
    /// </summary>
    public Vector2s(int x, int y)
    {
        this.x = (short)x;
        this.y = (short)y;
    }

    /// <summary>
    /// The difference, cut to shorts.
    /// </summary>
    public static Vector2s operator -(Vector2s a, Vector2s b) => new(a.x - b.x, a.y - b.y);

    /// <inheritdoc/>
    public readonly bool Equals(Vector2s other) => x == other.x && y == other.y;

    /// <inheritdoc/>
    public override readonly bool Equals(object obj) => obj is Vector2s v && Equals(v);

    /// <inheritdoc/>
    public override readonly int GetHashCode() => (ushort)x | (y << 16);
}
