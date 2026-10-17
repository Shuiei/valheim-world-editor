using System.Text;

namespace TerrainEditor.Save;

public static partial class Png
{
    /// <summary>Walks the chunks of a PNG: its header (if any), its text entries and its compressed picture data.</summary>
    /// <param name="bytes">The whole picture, signature included.</param>
    /// <returns>The header (null when there is none), the text entries, and the joined image data.</returns>
    private static (PngHeader? Header, Dictionary<string, string> Text, byte[] Idat) ScanChunks(byte[] bytes)
    {
        PngHeader? header = null;
        Dictionary<string, string> text = [];
        using MemoryStream idat = new();
        int p = 8;
        while (p + 8 <= bytes.Length)
        {
            int len = (bytes[p] << 24) | (bytes[p + 1] << 16) | (bytes[p + 2] << 8) | bytes[p + 3];
            string type = Encoding.ASCII.GetString(bytes, p + 4, 4);
            // Compared as what remains: p + 12 + len overflows for a length near int.MaxValue.
            if (len < 0 || len > bytes.Length - p - 12)
            {
                throw new InvalidDataException("the PNG picture is cut short");
            }
            ReadOnlySpan<byte> data = bytes.AsSpan(p + 8, len);
            switch (type)
            {
                case "IHDR":
                    header = ParseHeader(data);
                    break;
                case "tEXt":
                    AddText(text, data);
                    break;
                case "IDAT":
                    idat.Write(data);
                    break;
                default:
                    break;
            }
            p += 12 + len;
            if (type == "IEND")
            {
                break;
            }
        }
        return (header, text, idat.ToArray());
    }

    /// <summary>Reads the header's data; a short or interlaced header is refused.</summary>
    /// <param name="data">The header chunk's data.</param>
    /// <returns>The header's fields.</returns>
    internal static PngHeader ParseHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length < 13)
        {
            throw new InvalidDataException("the PNG picture's header is cut short");
        }
        if (data[12] != 0)
        {
            throw new InvalidDataException("interlaced PNG pictures are not supported; save it without interlacing");
        }
        int width = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];
        int height = (data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7];
        return new PngHeader(width, height, data[8], data[9]);
    }

    /// <summary>Adds a text entry (a keyword, a zero byte, then the text, both Latin-1); an entry with no keyword is skipped.</summary>
    /// <param name="text">The text entries read so far.</param>
    /// <param name="data">The text chunk's data.</param>
    private static void AddText(Dictionary<string, string> text, ReadOnlySpan<byte> data)
    {
        int zero = data.IndexOf((byte)0);
        if (zero > 0)
        {
            text[Encoding.Latin1.GetString(data[..zero])] = Encoding.Latin1.GetString(data[(zero + 1)..]);
        }
    }
}
