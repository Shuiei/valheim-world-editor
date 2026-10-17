// CrunchUnpacker: a part of the Crunch decoder (see Crunch.cs for the source and license).

namespace TerrainEditor.App;

/// <summary>
/// A crn file's palettes and codes read (crn_unpacker in crn_decomp.h), to unpack its levels.
/// </summary>
internal sealed partial class CrunchUnpacker
{
    /// <summary>The file's header.</summary>
    private readonly CrunchHeader _h;
    /// <summary>The reader over the file.</summary>
    private readonly CrunchCodec _codec;
    /// <summary>How each pair of rows of blocks shares endpoints (4 blocks, 2 bits each, per symbol).</summary>
    private readonly CrunchModel? _reference;
    /// <summary>Steps from one colour endpoint to the next, and from one alpha endpoint to the next.</summary>
    private readonly CrunchModel? _colorEndpointDelta, _alphaEndpointDelta;
    /// <summary>The colour selectors and alpha selectors of each block.</summary>
    private readonly CrunchModel? _colorSelector, _alphaSelector;
    /// <summary>The colour endpoints: a DXT1 block's first 4 bytes.</summary>
    private readonly uint[] _colorEndpoints;
    /// <summary>The colour selectors: a DXT1 block's last 4 bytes.</summary>
    private readonly uint[] _colorSelectors;
    /// <summary>The alpha endpoints: a DXT5 block's first 2 bytes.</summary>
    private readonly ushort[] _alphaEndpoints = [];
    /// <summary>The alpha selectors: a DXT5 block's next 6 bytes, three numbers per entry.</summary>
    private readonly ushort[] _alphaSelectors = [];

    /// <summary>Reads the codes and palettes.</summary>
    /// <exception cref="InvalidDataException">Broken data.</exception>
    public CrunchUnpacker(byte[] data, CrunchHeader h)
    {
        _h = h;
        _codec = new CrunchCodec(data);
        _codec.Start(h.Tables.Ofs, h.Tables.Size);
        _reference = _codec.ReadModel();
        _colorEndpointDelta = _codec.ReadModel();
        _colorSelector = _codec.ReadModel();
        if (h.Format == CrunchFormat.Dxt5)
        {
            _alphaEndpointDelta = _codec.ReadModel();
            _alphaSelector = _codec.ReadModel();
        }
        _colorEndpoints = ColorEndpoints();
        _colorSelectors = ColorSelectors();
        if (h.Format == CrunchFormat.Dxt5)
        {
            _alphaEndpoints = AlphaEndpoints();
            _alphaSelectors = AlphaSelectors();
        }
    }

    /// <summary>
    /// A level's blocks (unpack_dxt1, unpack_dxt5): read two rows of blocks at a time, as pairs of blocks; each
    /// block takes a new endpoint (the last one plus a step), the one of the block before it, or the one of the
    /// block above it, as the reference code says; then its selectors by index. Blocks past the level's edge (to
    /// make the pairs whole) are read and dropped.
    /// </summary>
    public byte[] Level(int level)
    {
        int width = Math.Max(_h.Width >> level, 1), height = Math.Max(_h.Height >> level, 1);
        int blocksX = (width + 3) / 4, blocksY = (height + 3) / 4;
        bool dxt5 = _h.Format == CrunchFormat.Dxt5;
        int blockBytes = dxt5 ? 16 : 8;
        byte[] output = new byte[blocksX * blocksY * blockBytes];
        int pairedX = (blocksX + 1) & ~1, pairedY = (blocksY + 1) & ~1;
        // Per column: the reference of the block below (odd rows), and its endpoints.
        byte[] belowReference = new byte[pairedX];
        int[] columnColor = new int[pairedX], columnAlpha = new int[pairedX];
        int color = 0, alpha = 0, group = 0;
        _codec.Start(_h.LevelOfs[level], _h.LevelEnd(level) - _h.LevelOfs[level]);
        for (int y = 0; y < pairedY; y++)
        {
            for (int x = 0; x < pairedX; x++)
            {
                if ((y & 1) == 0 && (x & 1) == 0)
                {
                    group = _codec.Decode(_reference) & 0xFF;
                }
                int reference;
                if ((y & 1) != 0)
                {
                    reference = belowReference[x];
                }
                else
                {
                    reference = group & 3;
                    group >>= 2;
                    belowReference[x] = (byte)(group & 3);
                    group >>= 2;
                }
                if (reference == 0)
                {
                    color = Step(color, _codec.Decode(_colorEndpointDelta), _colorEndpoints.Length);
                    columnColor[x] = color;
                    if (dxt5)
                    {
                        alpha = Step(alpha, _codec.Decode(_alphaEndpointDelta), _alphaEndpoints.Length);
                        columnAlpha[x] = alpha;
                    }
                }
                else if (reference == 1)
                {
                    columnColor[x] = color;
                    columnAlpha[x] = alpha;
                }
                else
                {
                    color = columnColor[x];
                    alpha = columnAlpha[x];
                }
                int colorSelector = _codec.Decode(_colorSelector);
                int alphaSelector = dxt5 ? _codec.Decode(_alphaSelector) : 0;
                if (colorSelector >= _colorSelectors.Length || (dxt5 && alphaSelector >= _alphaSelectors.Length / 3))
                {
                    throw new InvalidDataException("crn selector out of the palette");
                }
                if (x >= blocksX || y >= blocksY)
                {
                    continue;
                }
                WriteBlock(output.AsSpan((y * blocksX + x) * blockBytes, blockBytes), dxt5, color, colorSelector, alpha, alphaSelector);
            }
        }
        return output;
    }
}
