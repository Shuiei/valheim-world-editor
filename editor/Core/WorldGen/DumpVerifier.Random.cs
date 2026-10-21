using System.Globalization;
using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

public static partial class DumpVerifier
{
    /// <summary>
    /// Replays one Random call and compares its value and the state after it.
    /// </summary>
    internal static void CompareRandom(string[] p, Tally t)
    {
        int bar = Array.IndexOf(p, "|");
        string expectedState = string.Join(" ", p.Skip(bar + 1));
        string got;
        string expected = string.Join(" ", p.Skip(1).Take(bar - 1));
        if (p[1] == "init")
        {
            Rnd.InitState(int.Parse(p[2], CultureInfo.InvariantCulture));
            got = expected;
        }
        else if (p[1] == "rangei")
        {
            got = $"rangei {p[2]} {p[3]} {Rnd.Range(int.Parse(p[2], CultureInfo.InvariantCulture), int.Parse(p[3], CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture)}";
        }
        else if (p[1] == "rangef")
        {
            got = $"rangef {p[2]} {p[3]} {BitConverter.SingleToInt32Bits(Rnd.Range(float.Parse(p[2], CultureInfo.InvariantCulture), float.Parse(p[3], CultureInfo.InvariantCulture))):x8}";
        }
        else if (p[1] == "value")
        {
            got = $"value {BitConverter.SingleToInt32Bits(Rnd.Value):x8}";
        }
        else // circle
        {
            Vector2 v = Rnd.InsideUnitCircle;
            got = $"circle {BitConverter.SingleToInt32Bits(v.X):x8} {BitConverter.SingleToInt32Bits(v.Y):x8}";
        }
        string gotState = State();
        if (SameValue(p[1], expected, got) && gotState == expectedState)
        {
            t.RandomOk++;
        }
        else
        {
            t.RandomWrong++;
            t.Error($"Random: expected [{expected} | {expectedState}] got [{got} | {gotState}]");
        }
    }

    /// <summary>
    /// Whether a call's value is the game's: exactly, except a point on the unit circle, whose two floats may differ
    /// by a rounding step (the platform's sine, cosine and square root are not all the same bit for bit). The state
    /// after the call is always compared exactly.
    /// </summary>
    /// <param name="kind">The call's kind (the word after "Random").</param>
    /// <param name="expected">The game's value, as recorded.</param>
    /// <param name="got">The value replayed here.</param>
    private static bool SameValue(string kind, string expected, string got)
    {
        if (kind != "circle")
        {
            return got == expected;
        }
        string[] e = expected.Split(' '), g = got.Split(' ');
        if (e.Length != 3 || g.Length != 3)
        {
            return false;
        }
        return Within(e[1], g[1]) && Within(e[2], g[2]);
    }

    /// <summary>Whether two floats, written as recorded hex bits, differ by at most 1e-6.</summary>
    private static bool Within(string expectedHex, string gotHex)
    {
        float a = BitConverter.Int32BitsToSingle(int.Parse(expectedHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        float b = BitConverter.Int32BitsToSingle(int.Parse(gotHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        return MathF.Abs(a - b) <= 1e-6f;
    }
}
