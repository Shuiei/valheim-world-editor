namespace WorldEditor.Tests.Unity;

/// <summary>
/// An object's or structure's values by field name, as SerializedFileWriter writes them: numbers, texts, yes/no,
/// byte arrays, lists, and other Fields for structures. Fields left out are written as zeros or empty.
/// </summary>
public sealed class Fields : Dictionary<string, object?>
{
    /// <summary>
    /// No values yet; field names are matched as written.
    /// </summary>
    public Fields()
        : base(StringComparer.Ordinal)
    {
    }

    /// <summary>
    /// A reference's values: its file id (0 this file) and path id.
    /// </summary>
    public static Fields Ref(long pathId, int fileId = 0) => new() { ["m_FileID"] = fileId, ["m_PathID"] = pathId };

    /// <summary>
    /// A vector's values (x, y, z).
    /// </summary>
    public static Fields Vector3(double x, double y, double z) => new() { ["x"] = x, ["y"] = y, ["z"] = z };

    /// <summary>
    /// A quaternion's values (x, y, z, w).
    /// </summary>
    public static Fields Quaternion(double x, double y, double z, double w) => new() { ["x"] = x, ["y"] = y, ["z"] = z, ["w"] = w };
}
