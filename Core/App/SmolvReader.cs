namespace TerrainEditor.App;

/// <summary>
/// A cursor over the bytes of a SMOL-V program: the varints, bytes and words it reads in order. Reading past the end of
/// the data (for bytes and words) is a broken program.
/// </summary>
internal sealed class SmolvReader
{
    /// <summary>The program's bytes.</summary>
    private readonly byte[] _data;

    /// <summary>The offset where the data ends.</summary>
    private readonly int _end;

    /// <summary>Starts reading at an offset of the data.</summary>
    /// <param name="data">The bytes, which may hold more than the program.</param>
    /// <param name="position">The offset of the first byte to read.</param>
    internal SmolvReader(byte[] data, int position)
    {
        _data = data;
        _end = data.Length;
        Position = position;
    }

    /// <summary>The offset of the next byte to read (where the program ends, once it is all read).</summary>
    internal int Position { get; private set; }

    /// <summary>Whether every byte has been read.</summary>
    internal bool AtEnd => Position >= _end;

    /// <summary>Reads a variable-length integer: seven bits a byte, low bits first, while the high bit is set.</summary>
    /// <returns>The integer; a varint cut short by the end of the data is read as far as it goes.</returns>
    internal uint Varint()
    {
        uint v = 0;
        int shift = 0;
        while (Position < _end)
        {
            byte b = _data[Position++];
            v |= (uint)(b & 127) << shift;
            shift += 7;
            if ((b & 128) == 0)
            {
                break;
            }
        }
        return v;
    }

    /// <summary>Undoes the zig-zag encoding of a signed delta: odd values are negative.</summary>
    /// <param name="u">The encoded value.</param>
    /// <returns>The delta.</returns>
    internal static uint Zig(uint u) => (u & 1) != 0 ? (u >> 1) ^ 0xFFFFFFFF : u >> 1;

    /// <summary>Reads one byte.</summary>
    /// <returns>The byte.</returns>
    /// <exception cref="InvalidDataException">The data ends early.</exception>
    internal byte Byte()
    {
        if (Position >= _end)
        {
            throw new InvalidDataException("SMOL-V data ends early");
        }
        return _data[Position++];
    }

    /// <summary>Reads one little-endian 32-bit word.</summary>
    /// <returns>The word.</returns>
    /// <exception cref="InvalidDataException">The data ends early.</exception>
    internal uint Word()
    {
        if (Position + 4 > _end)
        {
            throw new InvalidDataException("SMOL-V data ends early");
        }
        uint v = BitConverter.ToUInt32(_data, Position);
        Position += 4;
        return v;
    }
}
