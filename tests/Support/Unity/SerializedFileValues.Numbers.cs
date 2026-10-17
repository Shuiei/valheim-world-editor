using System.Globalization;

namespace WorldEditor.Tests.Unity;

/// <summary>
/// Lays out a number of a Unity type as its bytes, for the values of <see cref="SerializedFileValues"/>.
/// </summary>
internal static partial class SerializedFileValues
{
    /// <summary>
    /// The value, or the fallback when there is none (a number of a Unity type is then zero).
    /// </summary>
    private static object OrDefault(object? v, object fallback) => v ?? fallback;

    /// <summary>
    /// A number of a Unity type (null: 0).
    /// </summary>
    private static void Number(BinaryWriter w, string type, object? v)
    {
        var c = CultureInfo.InvariantCulture;
        switch (type)
        {
            case "bool":
                w.Write(Convert.ToBoolean(OrDefault(v, false), c));
                break;
            case "UInt8" or "char":
                w.Write(Convert.ToByte(OrDefault(v, 0), c));
                break;
            case "SInt8":
                w.Write(Convert.ToSByte(OrDefault(v, 0), c));
                break;
            case "UInt16" or "unsigned short":
                w.Write(Convert.ToUInt16(OrDefault(v, 0), c));
                break;
            case "SInt16" or "short":
                w.Write(Convert.ToInt16(OrDefault(v, 0), c));
                break;
            case "int" or "SInt32":
                w.Write(Convert.ToInt32(OrDefault(v, 0), c));
                break;
            case "unsigned int" or "UInt32":
                w.Write(Convert.ToUInt32(OrDefault(v, 0), c));
                break;
            case "SInt64" or "long long":
                w.Write(Convert.ToInt64(OrDefault(v, 0), c));
                break;
            case "UInt64" or "FileSize":
                w.Write(Convert.ToUInt64(OrDefault(v, 0), c));
                break;
            case "float":
                w.Write(Convert.ToSingle(OrDefault(v, 0f), c));
                break;
            case "double":
                w.Write(Convert.ToDouble(OrDefault(v, 0.0), c));
                break;
            default:
                throw new ArgumentException($"no number type {type}");
        }
    }
}
