// CrunchCodec: a part of the Crunch decoder (see Crunch.cs for the source and license).

namespace TerrainEditor.App;

/// <summary>
/// The bit reader and Huffman decoder (symbol_codec in crn_decomp.h): bits most significant first; past the end of
/// its bytes it reads zeros, as crunch does.
/// </summary>
internal sealed class CrunchCodec
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
    public CrunchCodec(byte[] data) => _data = data;

    /// <summary>Reads from these bytes of the file on (checked by <see cref="CrunchHeader"/> to be inside it).</summary>
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
    public int Decode(CrunchModel? m)
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
    public CrunchModel? ReadModel()
    {
        int total = (int)Bits(14);
        if (total == 0)
        {
            return null;
        }
        if (total > CrunchFormat.MaxSymbols)
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
            lengthSizes[CrunchFormat.CodeLengthOrder[i]] = (byte)Bits(3);
        }
        var lengths = new CrunchModel(lengthSizes);
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
        return new CrunchModel(sizes);
    }
}
