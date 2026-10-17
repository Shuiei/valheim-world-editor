using System.Globalization;

namespace TerrainEditor.App;

/// <summary>
/// A float as <c>CatalogueJson</c> writes it, the way Python prints it.
/// </summary>
internal static partial class CatalogueJson
{
    /// <summary>
    /// A float as Python prints it: the shortest digits that read back the same, with ".0" on whole
    /// numbers, and an exponent below 1e-4 and from 1e16. NaN and infinities as Python writes them (NaN, Infinity).
    /// </summary>
    public static string Repr(double d)
    {
        if (double.IsNaN(d))
        {
            return "NaN";
        }
        if (double.IsInfinity(d))
        {
            return d > 0 ? "Infinity" : "-Infinity";
        }
        string sign = double.IsNegative(d) ? "-" : "";
        if (d == 0)
        {
            return sign + "0.0";
        }
        string r = Math.Abs(d).ToString("R", CultureInfo.InvariantCulture);
        int e = 0, ei = r.IndexOf('E', StringComparison.Ordinal);
        if (ei >= 0)
        {
            e = int.Parse(r.AsSpan(ei + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            r = r[..ei];
        }
        int dot = r.IndexOf('.', StringComparison.Ordinal);
        string digits = dot < 0 ? r : r.Remove(dot, 1);
        // The decimal point's place after the first digit.
        int point = (dot < 0 ? r.Length : dot) + e;
        int lead = 0;
        while (lead < digits.Length - 1 && digits[lead] == '0')
        {
            lead++;
        }
        digits = digits[lead..].TrimEnd('0');
        point -= lead;
        int exp = point - 1;
        if (exp is < (-4) or >= 16)
        {
            string mant = digits.Length > 1 ? digits[..1] + "." + digits[1..] : digits;
            return $"{sign}{mant}e{(exp < 0 ? '-' : '+')}{Math.Abs(exp):00}";
        }
        if (point <= 0)
        {
            return $"{sign}0.{new string('0', -point)}{digits}";
        }
        if (point >= digits.Length)
        {
            return $"{sign}{digits}{new string('0', point - digits.Length)}.0";
        }
        return $"{sign}{digits[..point]}.{digits[point..]}";
    }
}
