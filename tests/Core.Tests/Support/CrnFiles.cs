namespace WorldEditor.Tests;

/// <summary>
/// The crn test files' helpers: a copy of the decoder's CRC-16, and copies of the crn fixtures changed and given their
/// checks back, so tests can change one thing about a file.
/// </summary>
public static class CrnFiles
{
    /// <summary>A fixture's bytes (tests/fixtures).</summary>
    public static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(Fixtures.Root, name));

    /// <summary>
    /// crunch's CRC-16 (a copy of the decoder's, so that the tests can change a file's header or data and keep its
    /// checks right).
    /// </summary>
    public static ushort Crc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in data)
        {
            ushort q = (ushort)(b ^ (crc >> 8));
            crc <<= 8;
            ushort r = (ushort)((q >> 4) ^ q);
            crc ^= r;
            r <<= 5;
            crc ^= r;
            r <<= 7;
            crc ^= r;
        }
        return (ushort)~crc;
    }

    /// <summary>A copy of a file, changed as given.</summary>
    public static byte[] Copy(byte[] crn, Action<byte[]> change)
    {
        byte[] copy = (byte[])crn.Clone();
        change(copy);
        return copy;
    }

    /// <summary>The file with its data's and header's CRCs made right again (when its sizes are inside it).</summary>
    public static byte[] Recheck(byte[] crn)
    {
        int headerSize = (crn[2] << 8) | crn[3];
        long dataSize = ((long)crn[6] << 24) | ((long)crn[7] << 16) | ((long)crn[8] << 8) | crn[9];
        if (headerSize < 12 || dataSize < headerSize || dataSize > crn.Length)
        {
            return crn;
        }
        ushort data = Crc16(crn.AsSpan(headerSize, (int)dataSize - headerSize));
        crn[10] = (byte)(data >> 8);
        crn[11] = (byte)data;
        ushort header = Crc16(crn.AsSpan(6, headerSize - 6));
        crn[4] = (byte)(header >> 8);
        crn[5] = (byte)header;
        return crn;
    }
}
