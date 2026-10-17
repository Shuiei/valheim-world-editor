using TerrainEditor.App;

namespace WorldEditor.Tests;

/// <summary>
/// The crn test files' helpers: copies of the crn fixtures changed and given their checks back (with the decoder's
/// CRC-16), so tests can change one thing about a file.
/// </summary>
public static class CrnFiles
{
    /// <summary>A fixture's bytes (tests/fixtures).</summary>
    public static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(Fixtures.Root, name));

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
        ushort data = CrunchFormat.Crc16(crn.AsSpan(headerSize, (int)dataSize - headerSize));
        crn[10] = (byte)(data >> 8);
        crn[11] = (byte)data;
        ushort header = CrunchFormat.Crc16(crn.AsSpan(6, headerSize - 6));
        crn[4] = (byte)(header >> 8);
        crn[5] = (byte)header;
        return crn;
    }
}
