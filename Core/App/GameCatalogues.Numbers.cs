namespace TerrainEditor.App;

/// <summary>
/// Numbers and truth of the game data's values, as Python reads them.
/// </summary>
public static partial class GameCatalogues
{
    /// <summary>
    /// A value read by <c>Py</c> as a double (a bool as 0 or 1); throws InvalidDataException for anything else.
    /// </summary>
    internal static double Num(object? v) => v switch
    {
        long l => l,
        double d => d,
        bool b => b ? 1 : 0,
        _ => throw new InvalidDataException($"not a number: {v}"),
    };

    /// <summary>
    /// As Python's int(): floats cut toward zero.
    /// </summary>
    internal static long Int(object? v) => v switch
    {
        long l => l,
        double d => (long)d,
        bool b => b ? 1 : 0,
        _ => throw new InvalidDataException($"not a number: {v}"),
    };

    /// <summary>
    /// Whether a value is true as Python sees it (0, empty strings and empty collections are not).
    /// </summary>
    internal static bool Truthy(object? v) => v switch
    {
        null => false,
        bool b => b,
        long l => l != 0,
        double d => d != 0,
        string s => s.Length > 0,
        System.Collections.ICollection c => c.Count > 0,
        CatalogueJson.Dict d => d.Count > 0,
        _ => true,
    };
}
