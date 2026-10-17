using System.Text;

namespace WorldEditor.Tests.Unity;

/// <summary>
/// Lays out a text as its bytes and pads the layout to 4 bytes, for the values of <see cref="SerializedFileValues"/>.
/// </summary>
internal static partial class SerializedFileValues
{
    /// <summary>
    /// A text as its byte count then its UTF-8 bytes, padded after when its field is marked so.
    /// </summary>
    private static void Text(BinaryWriter w, UnityField f, object? v)
    {
        byte[] b = Encoding.UTF8.GetBytes(v as string ?? "");
        w.Write(b.Length);
        w.Write(b);
        if (f.Children.Count > 0 && f.Children[0].Align)
        {
            Pad(w);
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
