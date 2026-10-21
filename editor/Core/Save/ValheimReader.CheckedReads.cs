using System.IO.Compression;
using System.Text;

namespace TerrainEditor.Save;

/// <summary>
/// Lengths and counts read from a file, checked before they are used: a damaged file is refused
/// (InvalidDataException, EndOfStreamException) instead of making the editor allocate gigabytes or
/// fail with an unrelated error.
/// </summary>
public static partial class CheckedReads
{
    /// <summary>
    /// Whether a read of saved bytes failed because they are damaged or in a form the editor does not read: short
    /// (EndOfStream), a count or length that does not fit (InvalidData), a bad 7-bit length prefix (Format) or an
    /// item format older than the editor reads (NotSupported). What a reader of one object catches to skip it and
    /// go on with the rest. A plain IOException is not one: the bytes come from memory once read, so it says the file
    /// could not be read at all, which goes up like any other bug.
    /// </summary>
    public static bool Damaged(Exception ex) =>
        ex is EndOfStreamException or InvalidDataException or FormatException or NotSupportedException;

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
