// Reading a Vulkan blob's entries and SPIR-V instructions and strings (see GameShader).

using System.Text;

namespace TerrainEditor.App;

public static partial class GameShader
{
    /// <summary>
    /// The blob's entries (programs and parameter blobs): a count, then an offset and length per entry
    /// (12-byte records).
    /// </summary>
    /// <exception cref="InvalidDataException">The records, or an entry they name, do not fit in the blob.</exception>
    private static List<byte[]> Entries(byte[] blob)
    {
        uint n = blob.Length >= 4 ? BitConverter.ToUInt32(blob, 0) : throw new InvalidDataException("The shader's Vulkan blob is too short.");
        if (n > (blob.Length - 4) / 12)
        {
            throw new InvalidDataException("The shader's Vulkan blob lists more entries than it holds.");
        }
        var res = new List<byte[]>((int)n);
        for (int i = 0; i < n; i++)
        {
            uint o = BitConverter.ToUInt32(blob, 4 + 12 * i), l = BitConverter.ToUInt32(blob, 8 + 12 * i);
            if ((ulong)o + l > (ulong)blob.Length)
            {
                throw new InvalidDataException("An entry of the shader's Vulkan blob lies outside it.");
            }
            res.Add(blob.AsSpan((int)o, (int)l).ToArray());
        }
        return res;
    }

    /// <summary>
    /// (word index, opcode, index of the first operand) of each instruction after the 5-word header.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// No whole header, or an instruction of zero length or running past the end.
    /// </exception>
    private static IEnumerable<(int At, int Op, int Args)> Instructions(uint[] words)
    {
        if (words.Length < 5)
        {
            throw new InvalidDataException("bad SPIR-V: shorter than its header");
        }
        int i = 5;
        while (i < words.Length)
        {
            int n = (int)(words[i] >> 16);
            if (n == 0 || n > words.Length - i)
            {
                throw new InvalidDataException("bad SPIR-V: an instruction of no length, or running past the end");
            }
            yield return (i, (int)(words[i] & 0xFFFF), i + 1);
            i += n;
        }
    }

    /// <summary>Operand word count of the instruction at <paramref name="at"/>.</summary>
    private static int Length(uint[] words, int at) => (int)(words[at] >> 16) - 1;

    /// <summary>A SPIR-V literal string: UTF-8, nul-terminated, zero-padded to whole words.</summary>
    private static List<uint> StringWords(string s)
    {
        var b = Encoding.UTF8.GetBytes(s + "\0").ToList();
        while (b.Count % 4 != 0)
        {
            b.Add(0);
        }
        var res = new List<uint>();
        for (int i = 0; i < b.Count; i += 4)
        {
            res.Add(BitConverter.ToUInt32([.. b], i));
        }
        return res;
    }

    /// <summary>
    /// A SPIR-V literal string from <paramref name="count"/> words at <paramref name="from"/>, up to its nul.
    /// </summary>
    private static string ReadString(uint[] words, int from, int count)
    {
        byte[] b = new byte[count * 4];
        Buffer.BlockCopy(words, from * 4, b, 0, b.Length);
        int end = Array.IndexOf(b, (byte)0);
        return Encoding.Latin1.GetString(b, 0, end < 0 ? b.Length : end);
    }
}
