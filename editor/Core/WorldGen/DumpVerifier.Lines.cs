namespace ValheimGen;

public static partial class DumpVerifier
{
    /// <summary>
    /// The values between a Random call's name and its "|" (the call's arguments and result), by call; -1 for a call
    /// the verifier does not know.
    /// </summary>
    private static int RandomValues(string call) => call switch
    {
        "init" or "value" => 1,
        "circle" => 2,
        "rangei" or "rangef" => 3,
        _ => -1,
    };

    /// <summary>
    /// The values a line of this kind needs at least, its kind included (0: any line of it can be read).
    /// </summary>
    private static int Needed(string kind) => kind switch
    {
        "P" => 4,
        "H" => 5,
        "Z" => 3 + (BaseTerrain.Width + 1) * (BaseTerrain.Width + 1),
        _ => 0,
    };

    /// <summary>
    /// Whether a line of the dump has the values its kind needs: an "R" line has its call's values, its "|" and the
    /// state after it; the other kinds need a count of values (see <see cref="Needed"/>).
    /// </summary>
    internal static bool Readable(string[] p)
    {
        if (p[0] != "R")
        {
            return p.Length >= Needed(p[0]);
        }
        int bar = Array.IndexOf(p, "|");
        return p.Length > 1 && RandomValues(p[1]) is int values and >= 0 && bar == 2 + values && p.Length == bar + 5;
    }
}
