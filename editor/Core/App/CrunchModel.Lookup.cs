// CrunchModel: a part of the Crunch decoder (see Crunch.cs for the source and license).

namespace TerrainEditor.App;

/// <summary>
/// The lookup table of <see cref="CrunchModel"/>: the short codes, by the bits that start them.
/// </summary>
internal sealed partial class CrunchModel
{
    /// <summary>
    /// The lookup table over the next <paramref name="tableBits"/> bits: per entry, the symbol of the short code that
    /// starts those bits and, from bit 16, its length; all ones where none does.
    /// </summary>
    /// <param name="tableBits">The bits the table is indexed by.</param>
    /// <param name="numCodes">How many codes there are per length.</param>
    /// <param name="minCodes">The first code of each length.</param>
    private uint[] BuildLookup(int tableBits, int[] numCodes, uint[] minCodes)
    {
        uint[] lookup = new uint[1 << tableBits];
        Array.Fill(lookup, uint.MaxValue);
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
                    lookup[j + (code << fill)] = sym | ((uint)size << 16);
                }
            }
        }
        return lookup;
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
