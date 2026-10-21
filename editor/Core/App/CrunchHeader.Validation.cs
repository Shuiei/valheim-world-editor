// CrunchHeader: a part of the Crunch decoder (see Crunch.cs for the source and license).

using System.Numerics;

namespace TerrainEditor.App;

/// <summary>
/// The checks of <see cref="CrunchHeader"/>: its sizes, its CRCs, and the format it is read in.
/// </summary>
internal sealed partial class CrunchHeader
{
    /// <summary>Throws unless the header and data sizes fit each other and the data, with a level for each mip.</summary>
    /// <param name="d">The file's bytes.</param>
    /// <param name="headerSize">The header's size as the file gives it.</param>
    /// <exception cref="InvalidDataException">The sizes do not fit.</exception>
    private void CheckSizes(byte[] d, int headerSize)
    {
        if (headerSize < CrunchFormat.FixedHeader + 4 * Math.Max(1, Levels) || headerSize > d.Length || DataSize < headerSize || DataSize > d.Length)
        {
            throw new InvalidDataException("crn sizes out of the data");
        }
    }

    /// <summary>Throws unless the header's and the data's CRCs are the ones the file gives.</summary>
    /// <param name="d">The file's bytes.</param>
    /// <param name="headerSize">The header's size as the file gives it.</param>
    /// <exception cref="InvalidDataException">A CRC does not match.</exception>
    private void CheckChecksums(byte[] d, int headerSize)
    {
        if (CrunchFormat.Crc16(d.AsSpan(6, headerSize - 6)) != U(d, 4, 2) || CrunchFormat.Crc16(d.AsSpan(headerSize, DataSize - headerSize)) != U(d, 10, 2))
        {
            throw new InvalidDataException("crn CRC check failed");
        }
    }

    /// <summary>Throws unless the file is a single-face DXT1 or DXT5 texture of a size and mip count read here.</summary>
    /// <param name="faces">The number of faces the header gives.</param>
    /// <exception cref="InvalidDataException">Not a texture read here.</exception>
    private void CheckFormat(int faces)
    {
        if (faces != 1 || Format is not (CrunchFormat.Dxt1 or CrunchFormat.Dxt5) || Width is < 1 or > CrunchFormat.MaxResolution || Height is < 1 or > CrunchFormat.MaxResolution
            || Levels < 1 || Levels > 1 + BitOperations.Log2((uint)Math.Max(Width, Height)))
        {
            throw new InvalidDataException("crn texture not read here");
        }
    }
}
