namespace TerrainEditor.Save;

/// <summary>
/// The read side of Valheim's ZPackage for single values: bytes, numbers, strings and counted byte arrays.
/// </summary>
public sealed partial class ValheimReader
{
    /// <summary>One byte.</summary>
    public byte ReadByte() => _reader.ReadByte();

    /// <summary>A bool stored as one byte.</summary>
    public bool ReadBool() => _reader.ReadBoolean();

    /// <summary>A little-endian 16-bit int.</summary>
    public short ReadShort() => _reader.ReadInt16();

    /// <summary>A little-endian unsigned 16-bit int.</summary>
    public ushort ReadUShort() => _reader.ReadUInt16();

    /// <summary>A little-endian 32-bit int.</summary>
    public int ReadInt() => _reader.ReadInt32();

    /// <summary>A little-endian unsigned 32-bit int.</summary>
    public uint ReadUInt() => _reader.ReadUInt32();

    /// <summary>A little-endian 64-bit int.</summary>
    public long ReadLong() => _reader.ReadInt64();

    /// <summary>A little-endian 32-bit float.</summary>
    public float ReadSingle() => _reader.ReadSingle();

    /// <summary>A string with a 7-bit encoded length prefix, as ZPackage and BinaryWriter write it; its length checked.</summary>
    public string ReadString() => _reader.ReadCheckedString();

    /// <summary>An int byte count followed by that many bytes; a count that is negative or past the end is refused.</summary>
    public byte[] ReadByteArray() => _reader.ReadExactBytes(_reader.ReadInt32());

    /// <summary>Exactly that many raw bytes; fewer left than that is refused.</summary>
    public byte[] ReadBytes(int count) => _reader.ReadExactBytes(count);

    /// <summary>An int count of things that each take at least <paramref name="bytesEach"/> bytes, checked against what is left.</summary>
    public int ReadCount(int bytesEach) => _reader.CheckCount(_reader.ReadInt32(), bytesEach);
}
