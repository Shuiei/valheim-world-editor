using System.Text;

namespace Tunnelheim.WorldEditor;

/// <summary>
/// Reads what the game's ZPackage writes (little-endian numbers, int-prefixed byte arrays), refusing counts that are
/// negative or reach past the end.
/// </summary>
internal sealed class PackageReader : IDisposable
{
    private readonly BinaryReader _reader;

    /// <summary>A reader over a stream.</summary>
    /// <param name="stream">The bytes.</param>
    public PackageReader(Stream stream) => _reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

    /// <summary>A bool, one byte.</summary>
    public bool ReadBool() => _reader.ReadBoolean();

    /// <summary>A 32-bit int.</summary>
    public int ReadInt() => _reader.ReadInt32();

    /// <summary>A 64-bit int.</summary>
    public long ReadLong() => _reader.ReadInt64();

    /// <summary>A 32-bit float.</summary>
    public float ReadSingle() => _reader.ReadSingle();

    /// <summary>An int byte count, then that many bytes.</summary>
    /// <exception cref="InvalidDataException">The count is negative or past the end.</exception>
    public byte[] ReadByteArray() => ReadExactly(ReadCount(1));

    /// <summary>An int count of things that each take at least <paramref name="bytesEach"/> bytes, checked against what is left.</summary>
    /// <exception cref="InvalidDataException">The count is negative or past the end.</exception>
    public int ReadCount(int bytesEach)
    {
        int count = _reader.ReadInt32();
        long left = _reader.BaseStream.Length - _reader.BaseStream.Position;
        return count < 0 || (long)count * bytesEach > left
            ? throw new InvalidDataException($"a count of {count} does not fit the {left} bytes left")
            : count;
    }

    private byte[] ReadExactly(int count)
    {
        byte[] bytes = _reader.ReadBytes(count);
        return bytes.Length == count ? bytes : throw new EndOfStreamException($"{count} bytes asked, {bytes.Length} left");
    }

    /// <inheritdoc/>
    public void Dispose() => _reader.Dispose();
}
