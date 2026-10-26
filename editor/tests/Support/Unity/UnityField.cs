namespace WorldEditor.Tests.Unity;

/// <summary>
/// One field of a Unity type tree, as a serialized file describes it: its type name, its name, its children and its
/// flags. The test files' objects are written field after field as their type trees say (SerializedFileWriter), so a
/// field here is both the description the reader gets and the layout of the bytes it reads.
/// </summary>
/// <param name="Type">The type's name ("int", "string", "GameObject"...).</param>
/// <param name="Name">The field's name.</param>
/// <param name="Children">Its fields, in order (an array: "size" then "data").</param>
/// <param name="Align">Padded to 4 bytes after it (Unity's kAlignBytes meta flag).</param>
/// <param name="IsArray">An array node: a count, then that many "data" items.</param>
public sealed record UnityField(string Type, string Name, IReadOnlyList<UnityField> Children, bool Align = false, bool IsArray = false)
{
    /// <summary>
    /// A number or yes/no of this Unity type ("int", "float", "bool", "UInt8"...).
    /// </summary>
    public static UnityField Of(string type, string name, bool align = false) => new(type, name, [], align);

    /// <summary>
    /// A structure of these fields.
    /// </summary>
    public static UnityField Struct(string type, string name, params UnityField[] fields) => new(type, name, fields);

    /// <summary>
    /// A text: its length, then its UTF-8 bytes, padded to 4 bytes (as Unity's strings are).
    /// </summary>
    public static UnityField Text(string name) => new("string", name, [Array("char", align: true)]);

    /// <summary>
    /// A list (Unity's vector) of items like <paramref name="item"/>; <paramref name="align"/> pads it to 4 bytes.
    /// </summary>
    public static UnityField List(string name, UnityField item, bool align = false) => new("vector", name, [Array(item, align)]);

    /// <summary>
    /// Raw bytes kept in the object (Unity's TypelessData: a count, then the bytes, padded).
    /// </summary>
    public static UnityField Bytes(string name) => new("TypelessData", name, [Of("int", "size"), Of("UInt8", "data")], Align: true, IsArray: true);

    /// <summary>
    /// A reference to an object: its file (0 this one, else one of its externals, from 1) and its path id.
    /// </summary>
    public static UnityField Ref(string name) => Struct("PPtr<Object>", name, Of("int", "m_FileID"), Of("SInt64", "m_PathID"));

    /// <summary>
    /// A Vector3f (x, y, z).
    /// </summary>
    public static UnityField Vector3(string name) => Struct("Vector3f", name, Of("float", "x"), Of("float", "y"), Of("float", "z"));

    /// <summary>
    /// A Vector2f (x, y).
    /// </summary>
    public static UnityField Vector2(string name) => Struct("Vector2f", name, Of("float", "x"), Of("float", "y"));

    /// <summary>
    /// A Quaternionf (x, y, z, w).
    /// </summary>
    public static UnityField Quaternion(string name) => Struct("Quaternionf", name, Of("float", "x"), Of("float", "y"), Of("float", "z"), Of("float", "w"));

    /// <summary>
    /// The array node of a list of simple items of this type.
    /// </summary>
    private static UnityField Array(string itemType, bool align) => Array(Of(itemType, "data"), align);

    /// <summary>
    /// The array node of a list: its count ("size"), then its items ("data").
    /// </summary>
    private static UnityField Array(UnityField item, bool align) => new("Array", "Array", [Of("int", "size"), item with { Name = "data" }], align, IsArray: true);

    /// <summary>
    /// The byte size of a number type, 0 for others (structures, texts, arrays).
    /// </summary>
    public int Size => Children.Count > 0 || IsArray ? 0 : Type switch
    {
        "bool" or "UInt8" or "SInt8" or "char" => 1,
        "UInt16" or "SInt16" or "short" or "unsigned short" => 2,
        "SInt64" or "UInt64" or "double" or "long long" or "FileSize" => 8,
        _ => 4,
    };
}
