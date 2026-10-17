// CrunchHeader: a part of the Crunch decoder (see Crunch.cs for the source and license).

namespace TerrainEditor.App;

/// <summary>
/// A crn file's header (crn_header in crn_defs.h; its numbers big-endian), checked: the signature, both CRCs, the
/// size, levels and format, and every palette, table and level inside the data.
/// </summary>
internal sealed partial class CrunchHeader
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
        CheckSizes(d, headerSize);
        CheckChecksums(d, headerSize);
        CheckFormat(d[17]);
        ColorEndpoints = Palette(d, 33);
        ColorSelectors = Palette(d, 41);
        AlphaEndpoints = Palette(d, 49);
        AlphaSelectors = Palette(d, 57);
        Tables = (U(d, 67, 3), U(d, 65, 2));
        Inside(Tables.Ofs, Tables.Size);
        CheckPalettes();
        LevelOfs = ReadLevelOffsets(d, headerSize);
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
}
