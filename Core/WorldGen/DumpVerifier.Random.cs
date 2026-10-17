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
            got = $"value {BitConverter.SingleToInt32Bits(Rnd.value):x8}";
        }
        else // circle
        {
            Vector2 v = Rnd.insideUnitCircle;
            got = $"circle {BitConverter.SingleToInt32Bits(v.x):x8} {BitConverter.SingleToInt32Bits(v.y):x8}";
        }
        string gotState = State();
        if (got == expected && gotState == expectedState)
        {
            t.RandomOk++;
        }
        else
        {
            t.RandomWrong++;
            t.Error($"Random: expected [{expected} | {expectedState}] got [{got} | {gotState}]");
        }
    }
}
