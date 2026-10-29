using ValheimGen;
using Xunit;

using static WorldEditor.Tests.DumpVerifierSupport;

namespace WorldEditor.Tests;

/// <summary>
/// A replayed Random call: the generator is run the way the game ran it, and its value and state after are compared.
/// </summary>
public class DumpVerifierRandomTests
{
    [Fact]
    public void AMatchingInitCallIsCountedOk()
    {
        var t = new DumpVerifier.Tally();

        DumpVerifier.CompareRandom(Split($"R init 42 | {StateAfterInit(42)}"), t);

        Assert.Equal(1, t.RandomOk);
        Assert.Equal(0, t.RandomWrong);
    }

    [Fact]
    public void AnInitCallWithTheWrongStateIsCountedWrong()
    {
        var t = new DumpVerifier.Tally();

        DumpVerifier.CompareRandom(Split("R init 42 | 00000000 00000000 00000000 00000000"), t);

        Assert.Equal(0, t.RandomOk);
        Assert.Equal(1, t.RandomWrong);
        Assert.Contains("Random: expected", Assert.Single(t.Errors));
    }

    [Fact]
    public void ACirclePointOneRoundingStepOffIsCountedOk()
    {
        // Windows' sine, cosine and square root are not the game's bit for bit: a point on the unit circle may land one
        // rounding step away. The state after the call is still compared exactly.
        var (line, _) = CircleLine(42, ulpsOff: 1);
        var t = new DumpVerifier.Tally();
        _ = StateAfterInit(42);

        DumpVerifier.CompareRandom(Split(line), t);

        Assert.Equal(1, t.RandomOk);
        Assert.Equal(0, t.RandomWrong);
    }

    [Fact]
    public void ACirclePointFurtherOffIsCountedWrong()
    {
        // A hundredth away is a different point, not rounding.
        var (line, _) = CircleLine(42, shiftBy: 0.01f);
        var t = new DumpVerifier.Tally();
        _ = StateAfterInit(42);

        DumpVerifier.CompareRandom(Split(line), t);

        Assert.Equal(0, t.RandomOk);
        Assert.Equal(1, t.RandomWrong);
        Assert.Contains("Random: expected", Assert.Single(t.Errors));
    }

    /// <summary>
    /// A recorded circle line for this seed, as the dump would write it, with the point's X moved: by this amount when
    /// one is given, else by this many rounding steps (bit for bit).
    /// </summary>
    private static (string Line, string State) CircleLine(int seed, int ulpsOff = 0, float? shiftBy = null)
    {
        _ = StateAfterInit(seed);
        var v = ValheimGen.UnityEngine.Random.InsideUnitCircle;
        var s = ValheimGen.UnityEngine.Random.State;
        string state = $"{s.S0:x8} {s.S1:x8} {s.S2:x8} {s.S3:x8}";
        float x = shiftBy is float shift ? v.X + shift : BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(v.X) + ulpsOff);
        return ($"R circle {BitConverter.SingleToInt32Bits(x):x8} {BitConverter.SingleToInt32Bits(v.Y):x8} | {state}", state);
    }
}
