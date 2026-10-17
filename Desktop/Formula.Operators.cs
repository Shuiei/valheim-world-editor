namespace TerrainEditor.Desktop;

/// <summary>
/// The formula's operators by sign, for the levels of the parser that combine values with them.
/// </summary>
public static partial class Formula
{
    /// <summary>
    /// The operators of the product level, by sign.
    /// </summary>
    private static readonly Dictionary<string, Func<double, double, double>> Products = new()
    {
        ["*"] = (x, y) => x * y,
        ["/"] = (x, y) => x / y,
        ["%"] = (x, y) => x % y,
    };

    /// <summary>
    /// The operators of the sum level, by sign.
    /// </summary>
    private static readonly Dictionary<string, Func<double, double, double>> Sums = new()
    {
        ["+"] = (x, y) => x + y,
        ["-"] = (x, y) => x - y,
    };

    /// <summary>
    /// The comparisons, by sign (each gives 1 when it holds, else 0).
    /// </summary>
    private static readonly Dictionary<string, Func<double, double, bool>> Comparisons = new()
    {
        ["<"] = (x, y) => x < y,
        [">"] = (x, y) => x > y,
        ["<="] = (x, y) => x <= y,
        [">="] = (x, y) => x >= y,
        ["=="] = (x, y) => x == y,
        ["!="] = (x, y) => x != y,
    };
}
