using System.Globalization;
using System.Numerics;

namespace TerrainEditor.App;

/// <summary>
/// Python's rounding of floats for <c>CatalogueJson</c>, as the catalogues round them.
/// </summary>
internal static partial class CatalogueJson
{
    /// <summary>
    /// Powers of ten that are exact doubles (10^0 to 10^22), for dividing back without a rounding step.
    /// </summary>
    private static readonly double[] Pow10 = [.. Enumerable.Range(0, 23).Select(i => Math.Pow(10, i))];

    /// <summary>
    /// Python's round(x, n): the exact value rounded half to even at n decimals, read back as the nearest
    /// double (the sign kept, -0.0 included).
    /// </summary>
    public static double Round(double x, int n)
    {
        if (!double.IsFinite(x) || x == 0)
        {
            return x;
        }
        long bits = BitConverter.DoubleToInt64Bits(Math.Abs(x));
        int be = (int)((bits >> 52) & 0x7FF);
        long mant = bits & 0xFFFFFFFFFFFFFL;
        if (be == 0)
        {
            be = 1;
        }
        else
        {
            mant |= 1L << 52;
        }
        int shift = 1075 - be;
        if (shift <= 0)
        {
            // A whole number already.
            return x;
        }
        // |x| = mant / 2^shift; scaled = mant * 10^n / 2^shift, rounded half to even.
        BigInteger num = new BigInteger(mant) * BigInteger.Pow(10, n);
        BigInteger q = num >> shift;
        BigInteger rem = num - (q << shift);
        BigInteger half = BigInteger.One << (shift - 1);
        if (rem > half || rem == half && !q.IsEven)
        {
            q += 1;
        }
        double r = q < (BigInteger.One << 53) && n < Pow10.Length
            ? (double)q / Pow10[n]
            : double.Parse($"{q}E-{n}", CultureInfo.InvariantCulture);
        return double.IsNegative(x) ? -r : r;
    }

    /// <summary>
    /// Python's float("%.9G" % x): x to 9 significant digits (UnityPy's mesh export writes vertices so).
    /// </summary>
    public static double Significant9(double x)
    {
        if (!double.IsFinite(x) || x == 0)
        {
            return x;
        }
        int e = (int)Math.Floor(Math.Log10(Math.Abs(x)));
        return e <= 8 ? Round(x, 8 - e) : double.Parse(x.ToString("E8", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }
}
