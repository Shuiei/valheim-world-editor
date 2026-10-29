using System.Globalization;
using Rnd = ValheimGen.UnityEngine.Random;

namespace WorldEditor.Tests;

/// <summary>
/// The dump verifier tests' helpers: a float written as the dump writes it (its bits in hex), and the verifier's
/// tally of a replay line.
/// </summary>
public static class DumpVerifierSupport
{
    /// <summary>
    /// A float as the dump writes it: its bits as eight hex digits.
    /// </summary>
    public static string Hex(float v) => BitConverter.SingleToInt32Bits(v).ToString("x8", CultureInfo.InvariantCulture);

    /// <summary>
    /// The values of one dump line, split as the verifier splits them.
    /// </summary>
    public static string[] Split(string line) => line.Split(' ');

    /// <summary>
    /// The random state as the verifier writes it, after the generator was started from this seed.
    /// </summary>
    public static string StateAfterInit(int seed)
    {
        Rnd.InitState(seed);
        var s = Rnd.State;
        return $"{s.S0:x8} {s.S1:x8} {s.S2:x8} {s.S3:x8}";
    }
}
