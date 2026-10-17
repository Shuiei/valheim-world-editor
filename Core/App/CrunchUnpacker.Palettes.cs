// CrunchUnpacker: a part of the Crunch decoder (see Crunch.cs for the source and license).

using System.Buffers.Binary;

namespace TerrainEditor.App;

/// <summary>
/// The palettes of <see cref="CrunchUnpacker"/>: the endpoints and selectors read from the file, and the blocks written from them.
/// </summary>
internal sealed partial class CrunchUnpacker
{
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
    /// Writes one block from the palettes: its colour endpoint and selectors, and for DXT5 its alpha endpoint and
    /// selectors first, as the format lays them out.
    /// </summary>
    /// <param name="block">The block's bytes.</param>
    /// <param name="dxt5">Whether the block is DXT5 (alpha first).</param>
    /// <param name="color">The colour endpoint's index.</param>
    /// <param name="colorSelector">The colour selectors' index.</param>
    /// <param name="alpha">The alpha endpoint's index (DXT5 only).</param>
    /// <param name="alphaSelector">The alpha selectors' index (DXT5 only).</param>
    private void WriteBlock(Span<byte> block, bool dxt5, int color, int colorSelector, int alpha, int alphaSelector)
    {
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
