using System.IO.Compression;
using System.Numerics;
using System.Text;

namespace TerrainEditor.Save;

/// <summary>
/// Mirrors the read side of Valheim's ZPackage (little-endian BinaryReader plus a few custom encodings).
/// </summary>
/// <param name="stream">The data to read; disposing the reader closes it.</param>
public sealed class ValheimReader(Stream stream) : IDisposable
{
    private readonly BinaryReader _reader = new(stream);

    /// <summary>The read position in the underlying stream, in bytes.</summary>
    public long Position => _reader.BaseStream.Position;

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

    /// <summary>Three floats: x, y, z.</summary>
    public Vector3 ReadVector3() => new(ReadSingle(), ReadSingle(), ReadSingle());

    /// <summary>Skips a full quaternion (four floats) the editor does not need.</summary>
    public void SkipQuaternion() => _reader.BaseStream.Seek(16, SeekOrigin.Current);

    /// <summary>A Vector2s: two shorts, such as a sector's x and y.</summary>
    public (short X, short Y) ReadVector2s() => (_reader.ReadInt16(), _reader.ReadInt16());

    /// <summary>ZPackage.ReadSmallRotation: Euler angles in half degrees; 2 bytes for a pure Y rotation.</summary>
    /// <returns>The Euler angles in degrees.</returns>
    public Vector3 ReadSmallRotation()
    {
        uint num = _reader.ReadUInt16();
        if ((num & 0x8000) != 0)
        {
            return new Vector3(0f, (num & 0x7FFF) * 0.5f, 0f);
        }
        num = (num << 16) | _reader.ReadUInt16();
        return new Vector3(num & 0x3FF, (num >> 10) & 0x3FF, (num >> 20) & 0x3FF) * 0.5f;
    }

    /// <summary>ZPackage.ReadNumItems: 1 byte, or 2 bytes when the high bit is set (world version &gt;= 33).</summary>
    public int ReadNumItems(int worldVersion)
    {
        if (worldVersion < WorldVersion.NumItems)
        {
            return _reader.ReadByte();
        }
        int num = _reader.ReadByte();
        if ((num & 0x80) != 0)
        {
            num = ((num & 0x7F) << 8) | _reader.ReadByte();
        }
        return num;
    }

    /// <inheritdoc/>
    public void Dispose() => _reader.Dispose();
}

/// <summary>
/// Lengths and counts read from a file, checked before they are used: a damaged file is refused
/// (InvalidDataException, EndOfStreamException) instead of making the editor allocate gigabytes or
/// fail with an unrelated error.
/// </summary>
public static class CheckedReads
{
    /// <summary>How many bytes are left (as many as asked for when the stream cannot tell).</summary>
    public static long Remaining(this BinaryReader r) =>
        r.BaseStream.CanSeek ? Math.Max(0, r.BaseStream.Length - r.BaseStream.Position) : long.MaxValue;

    /// <summary>
    /// <paramref name="count"/>, when it is not negative and that many things of at least
    /// <paramref name="bytesEach"/> bytes fit in what is left; else refused.
    /// </summary>
    public static int CheckCount(this BinaryReader r, int count, int bytesEach)
    {
        if (count < 0)
        {
            throw new InvalidDataException($"negative count ({count})");
        }
        if ((long)count * bytesEach > r.Remaining())
        {
            throw new EndOfStreamException($"{count} items do not fit in the {r.Remaining()} bytes left");
        }
        return count;
    }

    /// <summary>
    /// Exactly <paramref name="count"/> bytes. A stream that cannot tell its length is read a megabyte at a
    /// time, so a wrong length runs into its end before much is allocated.
    /// </summary>
    public static byte[] ReadExactBytes(this BinaryReader r, int count)
    {
        r.CheckCount(count, 1);
        if (r.BaseStream.CanSeek || count <= 1 << 20)
        {
            byte[] bytes = r.ReadBytes(count);
            return bytes.Length == count ? bytes : throw new EndOfStreamException();
        }
        using MemoryStream all = new();
        while (all.Length < count)
        {
            byte[] part = r.ReadBytes((int)Math.Min(count - all.Length, 1 << 20));
            if (part.Length == 0)
            {
                throw new EndOfStreamException();
            }
            all.Write(part);
        }
        return all.ToArray();
    }

    /// <summary>
    /// BinaryReader.ReadString (a 7-bit encoded byte length, then UTF-8), with the length checked: a
    /// negative one makes BinaryReader throw a plain IOException.
    /// </summary>
    public static string ReadCheckedString(this BinaryReader r) => Encoding.UTF8.GetString(r.ReadExactBytes(r.Read7BitEncodedInt()));

    /// <summary>
    /// The content of gzip data, refused past <paramref name="limit"/> bytes (a few kilobytes can unpack to
    /// gigabytes) or when the compressed data is damaged.
    /// </summary>
    public static byte[] Gunzip(byte[] gzipped, int limit)
    {
        using GZipStream gz = new(new MemoryStream(gzipped), CompressionMode.Decompress);
        using MemoryStream raw = new();
        byte[] buffer = new byte[81920];
        int n;
        try
        {
            while ((n = gz.Read(buffer)) > 0)
            {
                if (raw.Length + n > limit)
                {
                    throw new InvalidDataException($"the data unpacks to more than {limit} bytes");
                }
                raw.Write(buffer, 0, n);
            }
        }
        catch (IOException e)
        {
            // Damaged compressed data (ZLibException): read from memory, nothing else can fail.
            throw new InvalidDataException("the compressed data is damaged", e);
        }
        return raw.ToArray();
    }
}

/// <summary>World save format versions at which the encoding changed.</summary>
public static class WorldVersion
{
    /// <summary>From this version counts read by ReadNumItems can take the two-byte form.</summary>
    public const int NumItems = 33;

    /// <summary>
    /// From this version the world is saved in chunk files, and objects no longer carry their sector and store their
    /// rotation in the small form.
    /// </summary>
    public const int ChunkedSave = 40;
}

/// <summary>The game's string hashing, for prefab names and data keys.</summary>
public static class StableHash
{
    /// <summary>Valheim's StringExtensionMethods.GetStableHashCode, used for prefab and data keys.</summary>
    public static int Of(string str)
    {
        unchecked
        {
            int num = 5381;
            int num2 = num;
            for (int i = 0; i < str.Length && str[i] != 0; i += 2)
            {
                num = ((num << 5) + num) ^ str[i];
                if (i == str.Length - 1 || str[i + 1] == '\0')
                {
                    break;
                }
                num2 = ((num2 << 5) + num2) ^ str[i + 1];
            }
            return num + num2 * 1566083941;
        }
    }

    /// <summary>
    /// The prefab an object's name in a copy or blueprint stands for: a kind with no known name (from a mod, or not in
    /// the catalogue) is written as its hash, a number, which stands for itself.
    /// </summary>
    public static int OfName(string name) =>
        int.TryParse(name, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out int hash) ? hash : Of(name);
}
