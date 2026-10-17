// CrunchUnpacker: a part of the Crunch decoder (see Crunch.cs for the source and license).

using System.Buffers.Binary;

namespace TerrainEditor.App;

/// <summary>
/// A crn file's palettes and codes read (crn_unpacker in crn_decomp.h), to unpack its levels.
/// </summary>
internal sealed class CrunchUnpacker
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
    /// The colour endpoints (decode_color_endpoints): each the last one plus steps, red, green and blue of the
    /// first colour then of the second, wrapping around.
    /// </summary>
    private uint[] ColorEndpoints()
    {
        var (ofs, size, num) = _h.ColorEndpoints;
        _codec.Start(ofs, size);
        var dm0 = _codec.ReadModel();
        var dm1 = _codec.ReadModel();
        uint[] e = new uint[num];
        uint a = 0, b = 0, c = 0, d = 0, f = 0, g = 0;
        for (int i = 0; i < e.Length; i++)
        {
            a = (a + (uint)_codec.Decode(dm0)) & 31;
            b = (b + (uint)_codec.Decode(dm1)) & 63;
            c = (c + (uint)_codec.Decode(dm0)) & 31;
            d = (d + (uint)_codec.Decode(dm0)) & 31;
            f = (f + (uint)_codec.Decode(dm1)) & 63;
            g = (g + (uint)_codec.Decode(dm0)) & 31;
            e[i] = c | (b << 5) | (a << 11) | (g << 16) | (f << 21) | (d << 27);
        }
        return e;
    }

    /// <summary>
    /// The colour selectors (decode_color_selectors): each the last one with sixteen 2-bit selectors XORed in, in
    /// linear order (0 to 3, first colour to second), turned to DXT1's order.
    /// </summary>
    private uint[] ColorSelectors()
    {
        var (ofs, size, num) = _h.ColorSelectors;
        _codec.Start(ofs, size);
        var dm = _codec.ReadModel();
        uint[] sel = new uint[num];
        uint s = 0;
        for (int i = 0; i < sel.Length; i++)
        {
            for (int j = 0; j < 32; j += 4)
            {
                s ^= (uint)_codec.Decode(dm) << j;
            }
            sel[i] = ((s ^ (s << 1)) & 0xAAAAAAAA) | ((s >> 1) & 0x55555555);
        }
        return sel;
    }

    /// <summary>The alpha endpoints (decode_alpha_endpoints): each the last one plus steps, wrapping.</summary>
    private ushort[] AlphaEndpoints()
    {
        var (ofs, size, num) = _h.AlphaEndpoints;
        _codec.Start(ofs, size);
        var dm = _codec.ReadModel();
        ushort[] e = new ushort[num];
        uint a = 0, b = 0;
        for (int i = 0; i < e.Length; i++)
        {
            a = (a + (uint)_codec.Decode(dm)) & 255;
            b = (b + (uint)_codec.Decode(dm)) & 255;
            e[i] = (ushort)(a | (b << 8));
        }
        return e;
    }

    /// <summary>
    /// The alpha selectors (decode_alpha_selectors): each the last one with sixteen 3-bit selectors XORed in, two
    /// at a time, in linear order, turned to DXT5's order; kept as the 48 bits of a block, three numbers.
    /// </summary>
    private ushort[] AlphaSelectors()
    {
        var (ofs, size, num) = _h.AlphaSelectors;
        _codec.Start(ofs, size);
        var dm = _codec.ReadModel();
        byte[] fromLinear = new byte[64];
        for (int i = 0; i < 64; i++)
        {
            fromLinear[i] = (byte)(CrunchFormat.Dxt5FromLinear[i & 7] | (CrunchFormat.Dxt5FromLinear[i >> 3] << 3));
        }
        ushort[] sel = new ushort[num * 3];
        uint s0Linear = 0, s1Linear = 0;
        for (int i = 0; i < sel.Length; i += 3)
        {
            uint s0 = ReadSelectorHalf(dm, fromLinear, ref s0Linear);
            uint s1 = ReadSelectorHalf(dm, fromLinear, ref s1Linear);
            sel[i] = (ushort)s0;
            sel[i + 1] = (ushort)((s0 >> 16) | (s1 << 8));
            sel[i + 2] = (ushort)(s1 >> 8);
        }
        return sel;
    }

    /// <summary>
    /// Four 3-bit alpha selectors of a block (one half of its 48 bits): each read as a change to the linear order
    /// kept in <paramref name="linear"/>, then turned to DXT5's order through <paramref name="fromLinear"/>.
    /// </summary>
    /// <param name="dm">The model the selectors are coded with.</param>
    /// <param name="fromLinear">The table from linear order to DXT5's order (see <see cref="AlphaSelectors"/>).</param>
    /// <param name="linear">The running linear order, read and kept between halves.</param>
    private uint ReadSelectorHalf(CrunchModel? dm, byte[] fromLinear, ref uint linear)
    {
        uint half = 0;
        for (int j = 0; j < 24; j += 6)
        {
            linear ^= (uint)_codec.Decode(dm) << j;
            half |= (uint)fromLinear[(linear >> j) & 0x3F] << j;
        }
        return half;
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
                var block = output.AsSpan((y * blocksX + x) * blockBytes, blockBytes);
                if (dxt5)
                {
                    int s = alphaSelector * 3;
                    BinaryPrimitives.WriteUInt32LittleEndian(block, _alphaEndpoints[alpha] | ((uint)_alphaSelectors[s] << 16));
                    BinaryPrimitives.WriteUInt32LittleEndian(block[4..], _alphaSelectors[s + 1] | ((uint)_alphaSelectors[s + 2] << 16));
                    block = block[8..];
                }
                BinaryPrimitives.WriteUInt32LittleEndian(block, _colorEndpoints[color]);
                BinaryPrimitives.WriteUInt32LittleEndian(block[4..], _colorSelectors[colorSelector]);
            }
        }
        return output;
    }

    /// <summary>An endpoint index plus a step, wrapping around the palette once.</summary>
    /// <exception cref="InvalidDataException">The step goes past the palette more than once.</exception>
    private static int Step(int index, int step, int count)
    {
        index += step;
        if (index >= count)
        {
            index -= count;
        }
        return index < count ? index : throw new InvalidDataException("crn endpoint out of the palette");
    }
}
