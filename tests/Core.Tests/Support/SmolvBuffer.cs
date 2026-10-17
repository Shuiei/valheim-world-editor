namespace WorldEditor.Tests;

/// <summary>
/// The bytes SMOL-V is written into: words, varints and single bytes, in the order they are added.
/// </summary>
internal sealed class SmolvBuffer
{
    private readonly List<byte> _bytes = [];

    /// <summary>
    /// Adds one byte.
    /// </summary>
    public void Add(byte b) => _bytes.Add(b);

    /// <summary>
    /// Adds bytes as they are.
    /// </summary>
    public void AddBytes(byte[] b) => _bytes.AddRange(b);

    /// <summary>
    /// Adds a 32-bit word, little-endian.
    /// </summary>
    public void Word(uint v) => _bytes.AddRange(BitConverter.GetBytes(v));

    /// <summary>
    /// Adds a number as a varint: seven bits a byte, the low ones first, the top bit set on all but the last byte.
    /// </summary>
    public void Varint(uint v)
    {
        while (v >= 128)
        {
            _bytes.Add((byte)(v | 128));
            v >>= 7;
        }
        _bytes.Add((byte)v);
    }

    /// <summary>
    /// The bytes written so far.
    /// </summary>
    public byte[] ToArray() => [.. _bytes];
}
