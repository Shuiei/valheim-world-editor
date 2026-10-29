using static TerrainEditor.App.SmolvOpcodes;

namespace TerrainEditor.App;

/// <summary>
/// Decodes the instructions of one SMOL-V program into SPIR-V words: each instruction's op, its result and type ids, and
/// its operands, with the ids kept as deltas from the previous ones.
/// </summary>
internal sealed class SmolvDecoder
{
    /// <summary>Where the program's bytes are read from.</summary>
    private readonly SmolvReader _reader;

    /// <summary>The SPIR-V words decoded so far (the header is already in it).</summary>
    private readonly List<uint> _output;

    /// <summary>The number of words the program decodes to.</summary>
    private readonly int _want;

    /// <summary>The number of ops the encoding version knows (ops from here up have no table data).</summary>
    private readonly int _known;

    /// <summary>The last result id, from which the next ones are counted.</summary>
    private uint _prevResult;

    /// <summary>The last decoration's target id, from which the next ones are counted.</summary>
    private uint _prevDecorate;

    /// <summary>Starts decoding a program's instructions.</summary>
    /// <param name="data">The bytes, which may hold more than the program.</param>
    /// <param name="start">The offset of the first instruction (after the header).</param>
    /// <param name="output">The words decoded so far, which the instructions are added to.</param>
    /// <param name="want">The number of words the program decodes to.</param>
    /// <param name="known">The number of ops the encoding version knows.</param>
    internal SmolvDecoder(byte[] data, int start, List<uint> output, int want, int known)
    {
        _reader = new SmolvReader(data, start);
        _output = output;
        _want = want;
        _known = known;
    }

    /// <summary>The offset where the program ends, once decoded.</summary>
    internal int End => _reader.Position;

    /// <summary>Decodes instructions until the output holds the program's words.</summary>
    /// <exception cref="InvalidDataException">The data ends early.</exception>
    internal void Run()
    {
        while (_output.Count < _want)
        {
            if (_reader.AtEnd)
            {
                throw new InvalidDataException("SMOL-V data ends early");
            }
            Instruction();
        }
    }

    /// <summary>Decodes one instruction: its header word, then its ids and operands.</summary>
    private void Instruction()
    {
        uint v = _reader.Varint();
        int length = (int)(((v >> 20) << 4) | ((v >> 4) & 0xF));
        int raw = (int)(((v >> 4) & 0xFFF0) | (v & 0xF));
        int op = Swap.TryGetValue(raw, out int swapped) ? swapped : raw;
        length += 1;
        if (op is Shuffle or ShuffleCompact)
        {
            length += 4;
        }
        else if (op == Decorate)
        {
            length += 2;
        }
        else if (op is Load or AccessChain)
        {
            length += 3;
        }
        bool swizzle = op == ShuffleCompact;
        if (swizzle)
        {
            op = Shuffle;
        }
        _output.Add(((uint)length << 16) | (uint)op);
        var info = op < _known ? Op(op) : (Result: 0, Type: 0, Deltas: 0, Varints: 0);
        int i = 1;
        if (info.Type != 0)
        {
            _output.Add(_reader.Varint());
            i++;
        }
        if (info.Result != 0)
        {
            _prevResult += SmolvReader.Zig(_reader.Varint());
            _output.Add(_prevResult);
            i++;
        }
        if (op is Decorate or MemberDecorate)
        {
            _prevDecorate += SmolvReader.Zig(_reader.Varint());
            _output.Add(_prevDecorate);
            i++;
        }
        if (op == MemberDecorate)
        {
            SmolvOperands.MemberDecorations(_reader, _output, op, _prevDecorate);
            return;
        }
        SmolvOperands.Operands(_reader, _output, _prevResult, swizzle, length, i, info);
    }
}
