#nullable disable

namespace ValheimGen;

/// <summary>
/// Valheim's Vector2i: an integer grid cell (the river cells' dictionary key).
/// </summary>
internal struct Vector2i : IEquatable<Vector2i>
{
    /// <summary>
    /// The cell's x.
    /// </summary>
    public int X;

    /// <summary>
    /// The cell's y (world z).
    /// </summary>
    public int Y;

    /// <summary>
    /// The cell (x, y).
    /// </summary>
    public Vector2i(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <inheritdoc/>
    public readonly bool Equals(Vector2i other) => X == other.X && Y == other.Y;

    /// <inheritdoc/>
    public override readonly bool Equals(object obj) => obj is Vector2i v && Equals(v);

    /// <summary>
    /// Spread out (x ^ y gave the world's grid cells only about 512 hashes: long chains in a dictionary).
    /// </summary>
    public override readonly int GetHashCode() => unchecked(X * 73856093 ^ Y * 19349663);

    /// <summary>
    /// The same cell.
    /// </summary>
    public static bool operator ==(Vector2i a, Vector2i b) => a.Equals(b);

    /// <summary>
    /// Different cells.
    /// </summary>
    public static bool operator !=(Vector2i a, Vector2i b) => !a.Equals(b);
}
