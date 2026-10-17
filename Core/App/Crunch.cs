// Crunch.cs: a C# port of the DXT1 and DXT5 decoding in crunch's inc/crn_decomp.h, as DaemonEngine/crunch has it
// (https://github.com/DaemonEngine/crunch, commit 4449e30), the Unity format of crunch
// (https://github.com/Unity-Technologies/crunch/tree/unity). Altered from the original: see the class summary.
//
// crn_decomp.h uses the ZLIB license:
// http://opensource.org/licenses/Zlib
//
// Copyright (c) 2010-2016 Richard Geldreich, Jr. and Binomial LLC
//
// This software is provided 'as-is', without any express or implied
// warranty.  In no event will the authors be held liable for any damages
// arising from the use of this software.
//
// Permission is granted to anyone to use this software for any purpose,
// including commercial applications, and to alter it and redistribute it
// freely, subject to the following restrictions:
//
// 1. The origin of this software must not be misrepresented; you must not
// claim that you wrote the original software. If you use this software
// in a product, an acknowledgment in the product documentation would be
// appreciated but is not required.
//
// 2. Altered source versions must be plainly marked as such, and must not be
// misrepresented as being the original software.
//
// 3. This notice may not be removed or altered from any source distribution.

using System.Buffers.Binary;
using System.Numerics;

namespace TerrainEditor.App;

/// <summary>
/// Unity's crunched textures (DXT1Crunched, DXT5Crunched) to plain DXT1 and DXT5 blocks: a port of the decoder in
/// crunch's crn_decomp.h (DaemonEngine/crunch, from Unity-Technologies/crunch's "unity" branch; zlib license,
/// Copyright (c) 2010-2016 Richard Geldreich, Jr. and Binomial LLC). Altered: only DXT1 and DXT5 (crunch's DXN,
/// DXT5A, ETC1 and ETC2 formats are left out), only one face (no cube maps), only Unity's format (Unity 2017.3 and
/// later; the original crunch format of older Unity versions codes its levels differently and is not read), and every
/// length and index the file gives is checked, as the files come from mods.
/// </summary>
public static class Crunch
{
    /// <summary>The crn formats read: DXT1 and DXT5 (crunch's cCRNFmtDXT1 and cCRNFmtDXT5).</summary>
    private const int Dxt1 = 0, Dxt5 = 2;

    /// <summary>The bytes of a crn header before its levels' offsets.</summary>
    private const int FixedHeader = 70;

    /// <summary>The largest width or height crunch writes.</summary>
    private const int MaxResolution = 4096;

    /// <summary>The most symbols a Huffman code has.</summary>
    private const int MaxSymbols = 8192;

    /// <summary>The longest Huffman code, in bits.</summary>
    private const int MaxCodeSize = 16;

    /// <summary>The most bits a Huffman decoding table looks up at once.</summary>
    private const int MaxTableBits = 11;

    /// <summary>
    /// The code length codes in the order they are sent: zero runs (short, long), repeats of the last length (short,
    /// long), then lengths.
    /// </summary>
    private static readonly byte[] CodeLengthOrder = [17, 18, 19, 20, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15, 16];

    /// <summary>A DXT5 alpha selector from its linear order (0 to 7, first to second endpoint) to a block's.</summary>
    private static readonly byte[] Dxt5FromLinear = [0, 2, 3, 4, 5, 6, 7, 1];

    /// <summary>A crunched texture's size, mip levels and block format, as its header gives them.</summary>
    /// <param name="Width">The top level's width in pixels.</param>
    /// <param name="Height">The top level's height in pixels.</param>
    /// <param name="Levels">How many mip levels it holds.</param>
    /// <param name="Dxt5">DXT5 blocks (16 bytes, with alpha) rather than DXT1 (8 bytes).</param>
    public sealed record Texture(int Width, int Height, int Levels, bool Dxt5);

    /// <summary>The texture a crn file holds, or null when it is not one read here (or broken).</summary>
    public static Texture? Read(byte[] crn)
    {
        try
        {
            var h = new Header(crn);
            return new Texture(h.Width, h.Height, h.Levels, h.Format == Dxt5);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>
    /// One mip level of a crn file as DXT1 or DXT5 blocks (as <see cref="Read"/> says), rows of blocks one after the
    /// other in the file's order. Null when the file is not one read here, the level is not in it, or the data is
    /// broken.
    /// </summary>
    public static byte[]? Unpack(byte[] crn, int level)
    {
        try
        {
            var h = new Header(crn);
            return level < 0 || level >= h.Levels ? null : new Unpacker(crn, h).Level(level);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>crunch's CRC-16 of bytes (crc16 in crn_decomp.h), for the header's and the data's checks.</summary>
    private static ushort Crc16(ReadOnlySpan<byte> data)
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

    /// <summary>
    /// A crn file's header (crn_header in crn_defs.h; its numbers big-endian), checked: the signature, both CRCs, the
    /// size, levels and format, and every palette, table and level inside the data.
    /// </summary>
    private sealed class Header
    {
        /// <summary>The top level's width in pixels.</summary>
        public readonly int Width;
        /// <summary>The top level's height in pixels.</summary>
        public readonly int Height;
        /// <summary>How many mip levels the file holds.</summary>
        public readonly int Levels;
        /// <summary>The crn format (<see cref="Dxt1"/> or <see cref="Dxt5"/>).</summary>
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
        public Header(byte[] d)
        {
            if (d.Length < FixedHeader + 4 || d[0] != 'H' || d[1] != 'x')
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
            if (headerSize < FixedHeader + 4 * Math.Max(1, Levels) || headerSize > d.Length || DataSize < headerSize || DataSize > d.Length)
            {
                throw new InvalidDataException("crn sizes out of the data");
            }
            if (Crc16(d.AsSpan(6, headerSize - 6)) != U(d, 4, 2) || Crc16(d.AsSpan(headerSize, DataSize - headerSize)) != U(d, 10, 2))
            {
                throw new InvalidDataException("crn CRC check failed");
            }
            if (faces != 1 || Format is not (Dxt1 or Dxt5) || Width is < 1 or > MaxResolution || Height is < 1 or > MaxResolution
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
            if (ColorEndpoints.Num == 0 || ColorSelectors.Num == 0 || (Format == Dxt5 && (AlphaEndpoints.Num == 0 || AlphaSelectors.Num == 0)))
            {
                throw new InvalidDataException("crn palette missing");
            }
            LevelOfs = new int[Levels];
            for (int i = 0; i < Levels; i++)
            {
                LevelOfs[i] = U(d, FixedHeader + 4 * i, 4);
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
            if (size <= 0 || ofs < FixedHeader || ofs + size > DataSize)
            {
                throw new InvalidDataException("crn part out of the data");
            }
        }
    }

    /// <summary>
    /// A static Huffman code (static_huffman_data_model and prefix_coding::decoder_tables in crn_decomp.h): code
    /// lengths per symbol, with a lookup table for the short codes and the canonical code's limits for the long ones.
    /// </summary>
    private sealed class Model
    {
        /// <summary>How many symbols the code has.</summary>
        public readonly int Symbols;
        /// <summary>Bits looked up at once in <see cref="Lookup"/> (0: no table).</summary>
        public readonly int TableBits;
        /// <summary>By the next TableBits bits: the symbol and, from bit 16, its code's length; or all ones.</summary>
        public readonly uint[] Lookup = [];
        /// <summary>The largest top 16 bits (plus one) through the table.</summary>
        public readonly uint TableMaxCode;
        /// <summary>The code length to start the search at, past the table.</summary>
        public readonly int StartCodeSize;
        /// <summary>Per code length (at length - 1): one more than its largest code, on 16 bits.</summary>
        public readonly uint[] MaxCodes = new uint[MaxCodeSize + 1];
        /// <summary>Per code length: what to add to a code of that length for its place in Sorted.</summary>
        public readonly int[] ValPtrs = new int[MaxCodeSize + 1];
        /// <summary>The used symbols sorted by code length, then by symbol.</summary>
        public readonly ushort[] Sorted;

        /// <summary>The code of these lengths per symbol (0: not used).</summary>
        /// <exception cref="InvalidDataException">No symbol used, or lengths no prefix code has.</exception>
        public Model(byte[] codeSizes)
        {
            Symbols = codeSizes.Length;
            int[] numCodes = new int[MaxCodeSize + 1];
            foreach (byte c in codeSizes)
            {
                if (c > MaxCodeSize)
                {
                    throw new InvalidDataException("crn code too long");
                }
                if (c != 0)
                {
                    numCodes[c]++;
                }
            }
            uint[] minCodes = new uint[MaxCodeSize];
            int[] sortedPositions = new int[MaxCodeSize + 1];
            uint curCode = 0;
            int used = 0, minCodeSize = int.MaxValue, maxCodeSize = 0;
            for (int i = 1; i <= MaxCodeSize; i++)
            {
                int n = numCodes[i];
                if (n == 0)
                {
                    MaxCodes[i - 1] = 0;
                }
                else
                {
                    // More codes of this length than there is room for: not a prefix code.
                    if (curCode + (uint)n > 1u << i)
                    {
                        throw new InvalidDataException("crn code lengths over-subscribed");
                    }
                    minCodeSize = Math.Min(minCodeSize, i);
                    maxCodeSize = Math.Max(maxCodeSize, i);
                    minCodes[i - 1] = curCode;
                    uint max = curCode + (uint)n - 1;
                    MaxCodes[i - 1] = 1 + ((max << (16 - i)) | ((1u << (16 - i)) - 1));
                    ValPtrs[i - 1] = used;
                    sortedPositions[i] = used;
                    curCode += (uint)n;
                    used += n;
                }
                curCode <<= 1;
            }
            if (used == 0)
            {
                throw new InvalidDataException("crn code with no symbol");
            }
            Sorted = new ushort[used];
            for (int i = 0; i < Symbols; i++)
            {
                if (codeSizes[i] != 0)
                {
                    Sorted[sortedPositions[codeSizes[i]]++] = (ushort)i;
                }
            }

            int tableBits = Symbols > 16 ? Math.Min(1 + CeilLog2(Symbols), MaxTableBits) : 0;
            if (tableBits <= minCodeSize)
            {
                tableBits = 0;
            }
            TableBits = tableBits;
            if (tableBits > 0)
            {
                Lookup = new uint[1 << tableBits];
                Array.Fill(Lookup, uint.MaxValue);
                for (int size = 1; size <= tableBits; size++)
                {
                    if (numCodes[size] == 0)
                    {
                        continue;
                    }
                    int fill = tableBits - size;
                    uint minCode = minCodes[size - 1], maxCode = (MaxCodes[size - 1] - 1) >> (16 - size);
                    int valPtr = ValPtrs[size - 1];
                    for (uint code = minCode; code <= maxCode; code++)
                    {
                        uint sym = Sorted[valPtr + (int)(code - minCode)];
                        for (uint j = 0; j < 1u << fill; j++)
                        {
                            Lookup[j + (code << fill)] = sym | ((uint)size << 16);
                        }
                    }
                }
            }
            for (int i = 0; i < MaxCodeSize; i++)
            {
                ValPtrs[i] -= (int)minCodes[i];
            }
            StartCodeSize = minCodeSize;
            if (tableBits > 0)
            {
                int i = tableBits;
                while (i >= 1 && numCodes[i] == 0)
                {
                    i--;
                }
                if (i >= 1)
                {
                    TableMaxCode = MaxCodes[i - 1];
                    StartCodeSize = tableBits + 1;
                    for (int j = tableBits + 1; j <= maxCodeSize; j++)
                    {
                        if (numCodes[j] != 0)
                        {
                            StartCodeSize = j;
                            break;
                        }
                    }
                }
            }
            // Sentinel: every search stops at the last length.
            MaxCodes[MaxCodeSize] = uint.MaxValue;
            ValPtrs[MaxCodeSize] = 0xFFFFF;
        }

        /// <summary>The bits to write v - 1 (ceil_log2i in crn_decomp.h).</summary>
        private static int CeilLog2(int v)
        {
            int l = 0;
            while (1 << l < v)
            {
                l++;
            }
            return l;
        }
    }

    /// <summary>
    /// The bit reader and Huffman decoder (symbol_codec in crn_decomp.h): bits most significant first; past the end of
    /// its bytes it reads zeros, as crunch does.
    /// </summary>
    private sealed class Codec
    {
        /// <summary>The file's bytes.</summary>
        private readonly byte[] _data;
        /// <summary>The next byte to read.</summary>
        private int _next;
        /// <summary>Where the bytes read end.</summary>
        private int _end;
        /// <summary>Bits read ahead, the first one in the top bit.</summary>
        private uint _bitBuf;
        /// <summary>How many bits <see cref="_bitBuf"/> holds.</summary>
        private int _bitCount;

        /// <summary>A reader over the file's bytes, started with <see cref="Start"/>.</summary>
        public Codec(byte[] data) => _data = data;

        /// <summary>Reads from these bytes of the file on (checked by <see cref="Header"/> to be inside it).</summary>
        public void Start(int ofs, int size)
        {
            _next = ofs;
            _end = ofs + size;
            _bitBuf = 0;
            _bitCount = 0;
        }

        /// <summary>The next byte, or 0 past the end.</summary>
        private uint NextByte() => _next < _end ? _data[_next++] : 0u;

        /// <summary>The next n bits (n at most 16) as a number, first bit highest.</summary>
        public uint Bits(int n)
        {
            if (n == 0)
            {
                return 0;
            }
            while (_bitCount < n)
            {
                _bitCount += 8;
                _bitBuf |= NextByte() << (32 - _bitCount);
            }
            uint result = _bitBuf >> (32 - n);
            _bitBuf <<= n;
            _bitCount -= n;
            return result;
        }

        /// <summary>The next symbol of this code.</summary>
        /// <exception cref="InvalidDataException">No code of the model starts with the next bits.</exception>
        public int Decode(Model? m)
        {
            if (m == null)
            {
                throw new InvalidDataException("crn code missing");
            }
            if (_bitCount < 24)
            {
                if (_bitCount < 16)
                {
                    uint c0 = NextByte(), c1 = NextByte();
                    _bitCount += 16;
                    _bitBuf |= ((c0 << 8) | c1) << (32 - _bitCount);
                }
                else
                {
                    _bitCount += 8;
                    _bitBuf |= NextByte() << (32 - _bitCount);
                }
            }
            uint k = (_bitBuf >> 16) + 1;
            int sym, len;
            if (k <= m.TableMaxCode)
            {
                uint t = m.Lookup[_bitBuf >> (32 - m.TableBits)];
                if (t == uint.MaxValue)
                {
                    throw new InvalidDataException("crn code not found");
                }
                sym = (int)(t & 0xFFFF);
                len = (int)(t >> 16);
            }
            else
            {
                len = m.StartCodeSize;
                while (k > m.MaxCodes[len - 1])
                {
                    len++;
                }
                long valPtr = m.ValPtrs[len - 1] + (_bitBuf >> (32 - len));
                if (valPtr < 0 || valPtr >= m.Sorted.Length)
                {
                    throw new InvalidDataException("crn code not found");
                }
                sym = m.Sorted[valPtr];
            }
            _bitBuf <<= len;
            _bitCount -= len;
            return sym;
        }

        /// <summary>
        /// A Huffman code as the data sends it (decode_receive_static_data_model): how many symbols, then the code
        /// lengths' own code, then the lengths with runs of zeros and repeats. Null when it has no symbol.
        /// </summary>
        /// <exception cref="InvalidDataException">A broken code.</exception>
        public Model? ReadModel()
        {
            int total = (int)Bits(14);
            if (total == 0)
            {
                return null;
            }
            if (total > MaxSymbols)
            {
                throw new InvalidDataException("crn code too large");
            }
            int sent = (int)Bits(5);
            if (sent is < 1 or > 21)
            {
                throw new InvalidDataException("crn code lengths broken");
            }
            byte[] lengthSizes = new byte[21];
            for (int i = 0; i < sent; i++)
            {
                lengthSizes[CodeLengthOrder[i]] = (byte)Bits(3);
            }
            var lengths = new Model(lengthSizes);
            byte[] sizes = new byte[total];
            int ofs = 0;
            while (ofs < total)
            {
                int left = total - ofs, code = Decode(lengths);
                if (code <= 16)
                {
                    sizes[ofs++] = (byte)code;
                    continue;
                }
                int run = code switch
                {
                    17 => (int)Bits(3) + 3,
                    18 => (int)Bits(7) + 11,
                    19 => (int)Bits(2) + 3,
                    _ => (int)Bits(6) + 7,
                };
                if (run > left || (code >= 19 && (ofs == 0 || sizes[ofs - 1] == 0)))
                {
                    throw new InvalidDataException("crn code lengths broken");
                }
                byte value = code >= 19 ? sizes[ofs - 1] : (byte)0;
                sizes.AsSpan(ofs, run).Fill(value);
                ofs += run;
            }
            return new Model(sizes);
        }
    }

    /// <summary>
    /// A crn file's palettes and codes read (crn_unpacker in crn_decomp.h), to unpack its levels.
    /// </summary>
    private sealed class Unpacker
    {
        /// <summary>The file's header.</summary>
        private readonly Header _h;
        /// <summary>The reader over the file.</summary>
        private readonly Codec _codec;
        /// <summary>How each pair of rows of blocks shares endpoints (4 blocks, 2 bits each, per symbol).</summary>
        private readonly Model? _reference;
        /// <summary>Steps from one colour endpoint to the next, and from one alpha endpoint to the next.</summary>
        private readonly Model? _colorEndpointDelta, _alphaEndpointDelta;
        /// <summary>The colour selectors and alpha selectors of each block.</summary>
        private readonly Model? _colorSelector, _alphaSelector;
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
        public Unpacker(byte[] data, Header h)
        {
            _h = h;
            _codec = new Codec(data);
            _codec.Start(h.Tables.Ofs, h.Tables.Size);
            _reference = _codec.ReadModel();
            _colorEndpointDelta = _codec.ReadModel();
            _colorSelector = _codec.ReadModel();
            if (h.Format == Dxt5)
            {
                _alphaEndpointDelta = _codec.ReadModel();
                _alphaSelector = _codec.ReadModel();
            }
            _colorEndpoints = ColorEndpoints();
            _colorSelectors = ColorSelectors();
            if (h.Format == Dxt5)
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
                fromLinear[i] = (byte)(Dxt5FromLinear[i & 7] | (Dxt5FromLinear[i >> 3] << 3));
            }
            ushort[] sel = new ushort[num * 3];
            uint s0Linear = 0, s1Linear = 0;
            for (int i = 0; i < sel.Length; i += 3)
            {
                uint s0 = 0, s1 = 0;
                for (int j = 0; j < 24; j += 6)
                {
                    s0Linear ^= (uint)_codec.Decode(dm) << j;
                    s0 |= (uint)fromLinear[(s0Linear >> j) & 0x3F] << j;
                }
                for (int j = 0; j < 24; j += 6)
                {
                    s1Linear ^= (uint)_codec.Decode(dm) << j;
                    s1 |= (uint)fromLinear[(s1Linear >> j) & 0x3F] << j;
                }
                sel[i] = (ushort)s0;
                sel[i + 1] = (ushort)((s0 >> 16) | (s1 << 8));
                sel[i + 2] = (ushort)(s1 >> 8);
            }
            return sel;
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
            bool dxt5 = _h.Format == Dxt5;
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
}
