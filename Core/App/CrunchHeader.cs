// CrunchHeader: a part of the Crunch decoder (see Crunch.cs for the source and license).

using System.Numerics;

namespace TerrainEditor.App;

/// <summary>
/// A crn file's header (crn_header in crn_defs.h; its numbers big-endian), checked: the signature, both CRCs, the
/// size, levels and format, and every palette, table and level inside the data.
/// </summary>
internal sealed class CrunchHeader
{
    /// <summary>The top level's width in pixels.</summary>
    public readonly int Width;
    /// <summary>The top level's height in pixels.</summary>
    public readonly int Height;
    /// <summary>How many mip levels the file holds.</summary>
    public readonly int Levels;
    /// <summary>The crn format (<see cref="CrunchFormat.Dxt1"/> or <see cref="CrunchFormat.Dxt5"/>).</summary>
    public readonly int Format;
    /// <summary>The file's size as the header gives it (the data may have more after it).</summary>
    public readonly int DataSize;
    /// <summary>The colour endpoints' palette: where it starts, its size in bytes, its entries.</summary>
    public readonly (int Ofs, int Size, int Num) ColorEndpoints;
    /// <summary>The colour selectors' palette.</summary>
    public readonly (int Ofs, int Size, int Num) ColorSelectors;
    /// <summary>The alpha endpoints' palette.</summary>
    public readonly (int Ofs, int Size, int Num) AlphaEndpoints;
    /// <summary>The alpha selectors' palette.</summary>
    public readonly (int Ofs, int Size, int Num) AlphaSelectors;
    /// <summary>The Huffman codes the levels are coded with: where they start and their size in bytes.</summary>
    public readonly (int Ofs, int Size) Tables;
    /// <summary>Where each level's data starts.</summary>
    public readonly int[] LevelOfs;

    /// <summary>Reads and checks the header.</summary>
    /// <exception cref="InvalidDataException">Not a crn file read here, or broken.</exception>
    public CrunchHeader(byte[] d)
    {
        if (d.Length < CrunchFormat.FixedHeader + 4 || d[0] != 'H' || d[1] != 'x')
        {
            throw new InvalidDataException("not a crn file");
        }
        int headerSize = U(d, 2, 2);
        DataSize = U(d, 6, 4);
        Width = U(d, 12, 2);
        Height = U(d, 14, 2);
        Levels = d[16];
        Format = d[18];
        int faces = d[17];
        if (headerSize < CrunchFormat.FixedHeader + 4 * Math.Max(1, Levels) || headerSize > d.Length || DataSize < headerSize || DataSize > d.Length)
        {
            throw new InvalidDataException("crn sizes out of the data");
        }
        if (CrunchFormat.Crc16(d.AsSpan(6, headerSize - 6)) != U(d, 4, 2) || CrunchFormat.Crc16(d.AsSpan(headerSize, DataSize - headerSize)) != U(d, 10, 2))
        {
            throw new InvalidDataException("crn CRC check failed");
        }
        if (faces != 1 || Format is not (CrunchFormat.Dxt1 or CrunchFormat.Dxt5) || Width is < 1 or > CrunchFormat.MaxResolution || Height is < 1 or > CrunchFormat.MaxResolution
            || Levels < 1 || Levels > 1 + BitOperations.Log2((uint)Math.Max(Width, Height)))
        {
            throw new InvalidDataException("crn texture not read here");
        }
        ColorEndpoints = Palette(d, 33);
        ColorSelectors = Palette(d, 41);
        AlphaEndpoints = Palette(d, 49);
        AlphaSelectors = Palette(d, 57);
        Tables = (U(d, 67, 3), U(d, 65, 2));
        Inside(Tables.Ofs, Tables.Size);
        if (ColorEndpoints.Num == 0 || ColorSelectors.Num == 0 || (Format == CrunchFormat.Dxt5 && (AlphaEndpoints.Num == 0 || AlphaSelectors.Num == 0)))
        {
            throw new InvalidDataException("crn palette missing");
        }
        LevelOfs = new int[Levels];
        for (int i = 0; i < Levels; i++)
        {
            LevelOfs[i] = U(d, CrunchFormat.FixedHeader + 4 * i, 4);
            if (LevelOfs[i] < headerSize || LevelOfs[i] >= DataSize || (i > 0 && LevelOfs[i] <= LevelOfs[i - 1]))
            {
                throw new InvalidDataException("crn level out of the data");
            }
        }
    }

    /// <summary>Where a level's data ends (the next level's start, or the file's end).</summary>
    public int LevelEnd(int level) => level + 1 < Levels ? LevelOfs[level + 1] : DataSize;

    /// <summary>A big-endian number of n bytes at d[at].</summary>
    private static int U(byte[] d, int at, int n)
    {
        int v = 0;
        for (int i = 0; i < n; i++)
        {
            v = (v << 8) | d[at + i];
        }
        return v;
    }

    /// <summary>A palette's place (crn_palette: offset, size, entries), checked to be inside the data.</summary>
    private (int Ofs, int Size, int Num) Palette(byte[] d, int at)
    {
        var p = (Ofs: U(d, at, 3), Size: U(d, at + 3, 3), Num: U(d, at + 6, 2));
        if (p.Num > 0)
        {
            Inside(p.Ofs, p.Size);
        }
        return p;
    }

    /// <summary>Throws unless these bytes are inside the data, after the header.</summary>
    private void Inside(int ofs, int size)
    {
        if (size <= 0 || ofs < CrunchFormat.FixedHeader || ofs + size > DataSize)
        {
            throw new InvalidDataException("crn part out of the data");
        }
    }
}
