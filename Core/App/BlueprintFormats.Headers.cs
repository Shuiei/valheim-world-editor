namespace TerrainEditor.App;

/// <summary>
/// Reading the header lines of a blueprint file: the #Name-style values and the editor's own #EditorData lines.
/// </summary>
public static partial class BlueprintFormats
{
    /// <summary>
    /// The value of a header line when it is that field's ("#Name:" for "#name: Hut" gives "Hut"), trimmed; the field is
    /// matched ignoring case. Null for another line. One rule for every reader of blueprint headers (a paste and the
    /// Workshop's library), so both show the same name and description.
    /// </summary>
    internal static string? HeaderValue(string line, string field) =>
        line.StartsWith(field, StringComparison.OrdinalIgnoreCase) ? line[field.Length..].Trim() : null;

    /// <summary>
    /// Strips the surrounding quotes PlanBuild writes around a description and undoes its \" and \n escapes.
    /// </summary>
    internal static string Unquote(string s) => s.Length >= 2 && s[0] == '"' && s[^1] == '"' ? s[1..^1].Replace("\\\"", "\"").Replace("\\n", "\n") : s;

    /// <summary>
    /// The section a "#" line that is not a header starts: the snap points, the terrain or the pieces, and any other
    /// mod's section (InfinityHammer's #TerrainHeight...) as "skip", which is left out like PlanBuild does.
    /// </summary>
    /// <param name="line">The "#" line.</param>
    private static string SectionAfter(string line) => line switch
    {
        "#SnapPoints" => "snap",
        "#Terrain" => "terrain",
        "#Pieces" => "pieces",
        _ => "skip",
    };
}
