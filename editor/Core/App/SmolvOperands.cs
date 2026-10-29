namespace TerrainEditor.App;

/// <summary>
/// The operands of a SMOL-V instruction after its header and ids: the id deltas, the varints or words, the swizzle's
/// bytes, and the member decorations, each of which SMOL-V writes its own way.
/// </summary>
internal static class SmolvOperands
{
    /// <summary>Decodes the operands of an instruction that are not member decorations.</summary>
    /// <param name="reader">Where the operands are read from.</param>
    /// <param name="output">The words decoded so far.</param>
    /// <param name="prevResult">The last result id, from which the id deltas count.</param>
    /// <param name="swizzle">Whether the instruction is a compact shuffle (its swizzle is one byte).</param>
    /// <param name="length">The instruction's length in words, header included.</param>
    /// <param name="i">The number of words of the instruction already written.</param>
    /// <param name="info">The op's table data: its result, type, delta and varint layout.</param>
    internal static void Operands(SmolvReader reader, List<uint> output, uint prevResult, bool swizzle, int length, int i, (int Result, int Type, int Deltas, int Varints) info)
    {
        for (int k = 0; k < info.Deltas && i < length; k++, i++)
        {
            output.Add(prevResult - SmolvReader.Zig(reader.Varint()));
        }
        if (swizzle && length <= 9)
        {
            Swizzle(reader, output, length);
        }
        else if (info.Varints != 0)
        {
            for (; i < length; i++)
            {
                output.Add(reader.Varint());
            }
        }
        else
        {
            for (; i < length; i++)
            {
                output.Add(reader.Word());
            }
        }
    }

    /// <summary>Decodes a compact shuffle's swizzle: its components are packed two bits each in one byte.</summary>
    /// <param name="reader">Where the byte is read from.</param>
    /// <param name="output">The words decoded so far.</param>
    /// <param name="length">The instruction's length in words, header included.</param>
    private static void Swizzle(SmolvReader reader, List<uint> output, int length)
    {
        byte s = reader.Byte();
        foreach (var (at2, sh) in new[] { (5, 6), (6, 4), (7, 2), (8, 0) })
        {
            if (length > at2)
            {
                output.Add((uint)(s >> sh) & 3);
            }
        }
    }

    /// <summary>Decodes an OpMemberDecorate: each member's index and decoration, with the offset and the extra words of its decoration.</summary>
    /// <param name="reader">Where the operands are read from.</param>
    /// <param name="output">The words decoded so far.</param>
    /// <param name="op">The op (OpMemberDecorate), written again for each member after the first.</param>
    /// <param name="prevDecorate">The last decoration's target id, written for each member after the first.</param>
    /// <exception cref="InvalidDataException">An offset decoration of the wrong length.</exception>
    internal static void MemberDecorations(SmolvReader reader, List<uint> output, int op, uint prevDecorate)
    {
        int count = reader.Byte();
        uint prevIndex = 0, prevOffset = 0;
        for (int m = 0; m < count; m++)
        {
            uint index = reader.Varint() + prevIndex;
            prevIndex = index;
            uint dec = reader.Varint();
            int extra = dec is 0 or >= 2 and <= 5 ? 0 : dec is >= 29 and <= 37 ? 1 : -1;
            int mlen = extra < 0 ? (int)reader.Varint() + 4 : 4 + extra;
            if (m > 0)
            {
                output.Add(((uint)mlen << 16) | (uint)op);
                output.Add(prevDecorate);
            }
            output.Add(index);
            output.Add(dec);
            if (dec == 35)
            {
                // Offset
                if (mlen != 5)
                {
                    throw new InvalidDataException("bad SMOL-V member offset");
                }
                prevOffset = reader.Varint() + prevOffset;
                output.Add(prevOffset);
            }
            else
            {
                for (int k = 4; k < mlen; k++)
                {
                    output.Add(reader.Varint());
                }
            }
        }
    }
}
