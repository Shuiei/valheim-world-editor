// CrunchHeader: a part of the Crunch decoder (see Crunch.cs for the source and license).

namespace TerrainEditor.App;

/// <summary>
/// The layout of <see cref="CrunchHeader"/>: where its palettes, tables and levels sit in the file, checked to be inside it.
/// </summary>
internal sealed partial class CrunchHeader
{
    /// <summary>Throws unless the palettes the colours and alpha use are present.</summary>
    /// <exception cref="InvalidDataException">A palette the format needs is missing.</exception>
    private void CheckPalettes()
    {
        if (ColorEndpoints.Num == 0 || ColorSelectors.Num == 0 || (Format == CrunchFormat.Dxt5 && (AlphaEndpoints.Num == 0 || AlphaSelectors.Num == 0)))
        {
            throw new InvalidDataException("crn palette missing");
        }
    }

    /// <summary>Reads where each level starts, checked to be after the header, inside the data and in order.</summary>
    /// <param name="d">The file's bytes.</param>
    /// <param name="headerSize">The header's size as the file gives it.</param>
    /// <returns>Where each level's data starts.</returns>
    /// <exception cref="InvalidDataException">A level starts outside the data or out of order.</exception>
    private int[] ReadLevelOffsets(byte[] d, int headerSize)
    {
        int[] levelOfs = new int[Levels];
        for (int i = 0; i < Levels; i++)
        {
            levelOfs[i] = U(d, CrunchFormat.FixedHeader + 4 * i, 4);
            if (levelOfs[i] < headerSize || levelOfs[i] >= DataSize || (i > 0 && levelOfs[i] <= levelOfs[i - 1]))
            {
                throw new InvalidDataException("crn level out of the data");
            }
        }
        return levelOfs;
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
