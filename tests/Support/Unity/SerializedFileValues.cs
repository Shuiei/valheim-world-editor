using System.Collections;

namespace WorldEditor.Tests.Unity;

/// <summary>
/// Lays out one object's values as its type tree says (the bytes of a Unity serialized file's objects), for
/// <see cref="SerializedFileWriter"/>.
/// </summary>
internal static partial class SerializedFileValues
{
    /// <summary>
    /// One value laid out as its field says: a structure field after field (missing ones as zeros), a list or text as
    /// its count then its items, a number in its size; padded after a field marked so.
    /// </summary>
    internal static void Value(BinaryWriter w, UnityField f, object? v)
    {
        if (f.Type == "string")
        {
            Text(w, f, v);
        }
        else if (f.IsArray)
        {
            Items(w, f, v);
        }
        else if (f.Children.Count > 0)
        {
            Structure(w, f, v);
        }
        else
        {
            Number(w, f.Type, v);
        }
        if (f.Align)
        {
            Pad(w);
        }
    }

    /// <summary>
    /// A list as its item count then its items, or a byte array as its length then its bytes.
    /// </summary>
    private static void Items(BinaryWriter w, UnityField f, object? v)
    {
        var item = f.Children[1];
        if (v is byte[] bytes)
        {
            w.Write(bytes.Length);
            w.Write(bytes);
            return;
        }
        var items = v as IList ?? (v is IEnumerable e and not string ? e.Cast<object?>().ToList() : []);
        w.Write(items.Count);
        foreach (object? it in items)
        {
            Value(w, item, it);
        }
    }

    /// <summary>
    /// A structure's fields in order; a list's own node takes the list's value, and a missing field is zeros.
    /// </summary>
    private static void Structure(BinaryWriter w, UnityField f, object? v)
    {
        var d = v as Fields;
        foreach (var c in f.Children)
        {
            // A list's array node takes the list's own value.
            Value(w, c, c.IsArray && f.Children.Count == 1 ? v : d?.GetValueOrDefault(c.Name));
        }
    }
}
