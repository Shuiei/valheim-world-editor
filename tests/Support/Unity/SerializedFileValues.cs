using System.Collections;
using System.Text;

namespace WorldEditor.Tests.Unity;

/// <summary>
/// Lays out one object's values as its type tree says (the bytes of a Unity serialized file's objects), for
/// <see cref="SerializedFileWriter"/>.
/// </summary>
internal static class SerializedFileValues
{
    /// <summary>
    /// One value laid out as its field says: a structure field after field (missing ones as zeros), a list or text as
    /// its count then its items, a number in its size; padded after a field marked so.
    /// </summary>
    internal static void Value(BinaryWriter w, UnityField f, object? v)
    {
        if (f.Type == "string")
        {
            byte[] b = Encoding.UTF8.GetBytes(v as string ?? "");
            w.Write(b.Length);
            w.Write(b);
            if (f.Children.Count > 0 && f.Children[0].Align)
            {
                Pad(w);
            }
        }
        else if (f.IsArray)
        {
            var item = f.Children[1];
            if (v is byte[] bytes)
            {
                w.Write(bytes.Length);
                w.Write(bytes);
            }
            else
            {
                var items = v as IList ?? (v is IEnumerable e and not string ? e.Cast<object?>().ToList() : []);
                w.Write(items.Count);
                foreach (object? it in items)
                {
                    Value(w, item, it);
                }
            }
        }
        else if (f.Children.Count > 0)
        {
            var d = v as Fields;
            foreach (var c in f.Children)
            {
                // A list's array node takes the list's own value.
                Value(w, c, c.IsArray && f.Children.Count == 1 ? v : d?.GetValueOrDefault(c.Name));
            }
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
    /// A number of a Unity type (null: 0).
    /// </summary>
    private static void Number(BinaryWriter w, string type, object? v)
    {
        var c = System.Globalization.CultureInfo.InvariantCulture;
        switch (type)
        {
            case "bool":
                w.Write(Convert.ToBoolean(v ?? false, c));
                break;
            case "UInt8" or "char":
                w.Write(Convert.ToByte(v ?? 0, c));
                break;
            case "SInt8":
                w.Write(Convert.ToSByte(v ?? 0, c));
                break;
            case "UInt16" or "unsigned short":
                w.Write(Convert.ToUInt16(v ?? 0, c));
                break;
            case "SInt16" or "short":
                w.Write(Convert.ToInt16(v ?? 0, c));
                break;
            case "int" or "SInt32":
                w.Write(Convert.ToInt32(v ?? 0, c));
                break;
            case "unsigned int" or "UInt32":
                w.Write(Convert.ToUInt32(v ?? 0, c));
                break;
            case "SInt64" or "long long":
                w.Write(Convert.ToInt64(v ?? 0, c));
                break;
            case "UInt64" or "FileSize":
                w.Write(Convert.ToUInt64(v ?? 0, c));
                break;
            case "float":
                w.Write(Convert.ToSingle(v ?? 0f, c));
                break;
            case "double":
                w.Write(Convert.ToDouble(v ?? 0.0, c));
                break;
            default:
                throw new ArgumentException($"no number type {type}");
        }
    }

    /// <summary>
    /// Zeros up to the next multiple of 4 bytes.
    /// </summary>
    internal static void Pad(BinaryWriter w)
    {
        while (w.BaseStream.Position % 4 != 0)
        {
            w.Write((byte)0);
        }
    }
}
