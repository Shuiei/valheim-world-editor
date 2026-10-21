using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The operands of one SMOL-V instruction (SmolvOperands): varints, fixed words, id deltas and member decorations, each
/// read from hand-written bytes.
/// </summary>
public class SmolvOperandsTests
{
    private static readonly (int Result, int Type, int Deltas, int Varints) NoLayout = (0, 0, 0, 0);

    [Fact]
    public void OperandsAreReadAsVarintsUpToTheLength()
    {
        var output = new List<uint>();
        var reader = new SmolvReader([5, 7], 0);
        SmolvOperands.Operands(reader, output, 0, false, 3, 1, (0, 0, 0, 1));
        Assert.Equal([5u, 7u], output);
    }

    [Fact]
    public void OperandsWithoutAVarintLayoutAreFixedWords()
    {
        var output = new List<uint>();
        var reader = new SmolvReader(BitConverter.GetBytes(0x11223344u), 0);
        SmolvOperands.Operands(reader, output, 0, false, 2, 1, NoLayout);
        Assert.Equal([0x11223344u], output);
    }

    [Fact]
    public void AnIdDeltaCountsFromThePreviousResult()
    {
        var output = new List<uint>();
        var reader = new SmolvReader([6], 0);
        SmolvOperands.Operands(reader, output, 10, false, 2, 1, (1, 0, 1, 0));
        Assert.Equal([7u], output);
    }

    [Fact]
    public void AMemberDecorationWritesItsIndexAndDecoration()
    {
        var output = new List<uint>();
        var reader = new SmolvReader([1, 2, 0], 0);
        SmolvOperands.MemberDecorations(reader, output, 72, 0);
        Assert.Equal([2u, 0u], output);
    }

    [Fact]
    public void AnOffsetDecorationIsWrittenAsItsValue()
    {
        var output = new List<uint>();
        var reader = new SmolvReader([1, 0, 35, 16], 0);
        SmolvOperands.MemberDecorations(reader, output, 72, 0);
        Assert.Equal([0u, 35u, 16u], output);
    }
}
