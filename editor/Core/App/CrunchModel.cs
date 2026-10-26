// CrunchModel: a part of the Crunch decoder (see Crunch.cs for the source and license).

namespace TerrainEditor.App;

/// <summary>
/// A static Huffman code (static_huffman_data_model and prefix_coding::decoder_tables in crn_decomp.h): code
/// lengths per symbol, with a lookup table for the short codes and the canonical code's limits for the long ones.
/// </summary>
internal sealed partial class CrunchModel
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
    public readonly uint[] MaxCodes = new uint[CrunchFormat.MaxCodeSize + 1];
    /// <summary>Per code length: what to add to a code of that length for its place in Sorted.</summary>
    public readonly int[] ValPtrs = new int[CrunchFormat.MaxCodeSize + 1];
    /// <summary>The used symbols sorted by code length, then by symbol.</summary>
    public readonly ushort[] Sorted;

    /// <summary>The code of these lengths per symbol (0: not used).</summary>
    /// <exception cref="InvalidDataException">No symbol used, or lengths no prefix code has.</exception>
    public CrunchModel(byte[] codeSizes)
    {
        Symbols = codeSizes.Length;
        int[] numCodes = new int[CrunchFormat.MaxCodeSize + 1];
        foreach (byte c in codeSizes)
        {
            if (c > CrunchFormat.MaxCodeSize)
            {
                throw new InvalidDataException("crn code too long");
            }
            if (c != 0)
            {
                numCodes[c]++;
            }
        }
        uint[] minCodes = new uint[CrunchFormat.MaxCodeSize];
        int[] sortedPositions = new int[CrunchFormat.MaxCodeSize + 1];
        uint curCode = 0;
        int used = 0, minCodeSize = int.MaxValue, maxCodeSize = 0;
        for (int i = 1; i <= CrunchFormat.MaxCodeSize; i++)
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

        int tableBits = Symbols > 16 ? Math.Min(1 + CeilLog2(Symbols), CrunchFormat.MaxTableBits) : 0;
        if (tableBits <= minCodeSize)
        {
            tableBits = 0;
        }
        TableBits = tableBits;
        if (tableBits > 0)
        {
            Lookup = BuildLookup(tableBits, numCodes, minCodes);
        }
        for (int i = 0; i < CrunchFormat.MaxCodeSize; i++)
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
        MaxCodes[CrunchFormat.MaxCodeSize] = uint.MaxValue;
        ValPtrs[CrunchFormat.MaxCodeSize] = 0xFFFFF;
    }
}
