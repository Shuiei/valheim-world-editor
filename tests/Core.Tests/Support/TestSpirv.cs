using System.Text;

namespace WorldEditor.Tests;

/// <summary>
/// Small SPIR-V modules for the tests of the terrain shader's Vulkan program (GameShader.FromVulkan): a fragment
/// program as Unity leaves it (its uniform block, texture, unnamed colour input and outputs without names), programs
/// of other stages, a parameter blob naming what the program uses, and a platform blob holding them all.
/// </summary>
public static class TestSpirv
{
    /// <summary>
    /// The ids of the fragment program's parts.
    /// </summary>
    public const uint Main = 1, Target = 2, Block = 11, Globals = 13, Texture = 17, Color = 19, Uv = 20;

    /// <summary>
    /// SPIR-V execution models: vertex, tessellation control, fragment.
    /// </summary>
    public const uint Vertex = 0, TessControl = 1, Fragment = 4;

    /// <summary>
    /// The words of an instruction: its length and op, then its operands.
    /// </summary>
    private static void Op(List<uint> w, ushort op, params uint[] a)
    {
        w.Add(((uint)(a.Length + 1) << 16) | op);
        w.AddRange(a);
    }

    /// <summary>
    /// A SPIR-V literal string as words: UTF-8, nul-terminated, padded to whole words.
    /// </summary>
    public static uint[] Text(string s)
    {
        byte[] b = Encoding.UTF8.GetBytes(s + "\0");
        Array.Resize(ref b, (b.Length + 3) / 4 * 4);
        uint[] w = new uint[b.Length / 4];
        Buffer.BlockCopy(b, 0, w, 0, b.Length);
        return w;
    }

    /// <summary>
    /// A program of this stage: the fragment one has a uniform block (a float at 0, a vec4 at 16), a texture at
    /// binding 1, a colour input at location 0 without a name, a named UV input, an output at location 1; it writes
    /// white. Other stages are the same program under another execution model.
    /// </summary>
    public static uint[] Program(uint stage = Fragment, uint textureBinding = 1)
    {
        var w = new List<uint> { 0x07230203, 0x00010000, 0, 30, 0 };
        const uint TVoid = 3, TFn = 4, TFloat = 5, TVec4 = 6, TPtrOut = 7, One = 8, White = 9, Label = 10, TPtrBlock = 12, TImage = 14, TSampled = 15, TPtrTex = 16, TPtrIn = 18;
        Op(w, 17, 1); // OpCapability Shader
        Op(w, 14, 0, 1); // OpMemoryModel Logical GLSL450
        Op(w, 15, [stage, Main, .. Text("main"), Target, Color, Uv]); // OpEntryPoint
        Op(w, 16, Main, 7); // OpExecutionMode OriginUpperLeft
        Op(w, 5, [Main, .. Text("main")]); // OpName: the debug names Unity keeps
        Op(w, 5, [Uv, .. Text("vs_TEXCOORD0")]);
        Op(w, 71, Target, 30, 1); // OpDecorate Location 1
        Op(w, 71, Color, 30, 0);
        Op(w, 71, Uv, 30, 1);
        Op(w, 71, Texture, 34, 0); // DescriptorSet 0
        Op(w, 71, Texture, 33, textureBinding); // Binding
        Op(w, 71, Block, 2); // Block
        Op(w, 72, Block, 0, 35, 0); // OpMemberDecorate Offset 0
        Op(w, 72, Block, 1, 35, 16); // Offset 16
        Op(w, 19, TVoid);
        Op(w, 33, TFn, TVoid);
        Op(w, 22, TFloat, 32);
        Op(w, 23, TVec4, TFloat, 4);
        Op(w, 30, Block, TFloat, TVec4);
        Op(w, 32, TPtrBlock, 2, Block); // Uniform
        Op(w, 59, TPtrBlock, Globals, 2);
        Op(w, 25, TImage, TFloat, 1, 0, 0, 0, 1, 0); // 2D image
        Op(w, 27, TSampled, TImage);
        Op(w, 32, TPtrTex, 0, TSampled); // UniformConstant
        Op(w, 59, TPtrTex, Texture, 0);
        Op(w, 32, TPtrIn, 1, TVec4); // Input
        Op(w, 59, TPtrIn, Color, 1);
        Op(w, 59, TPtrIn, Uv, 1);
        Op(w, 32, TPtrOut, 3, TVec4); // Output
        Op(w, 59, TPtrOut, Target, 3);
        Op(w, 43, TFloat, One, 0x3F800000);
        Op(w, 44, TVec4, White, One, One, One, One);
        Op(w, 54, TVoid, Main, 0, TFn);
        Op(w, 248, Label);
        Op(w, 62, Target, White);
        Op(w, 253);
        Op(w, 56);
        return [.. w];
    }

    /// <summary>
    /// A parameter blob as Unity writes it after a 24-byte header: records of a length-prefixed name (padded to 4
    /// bytes) followed by numbers. A constant buffer (size, member count) then its members (type, rows, columns,
    /// matrix, array size, offset); a texture (0, binding, sampler, dimension).
    /// </summary>
    public static byte[] Parameters(string buffer, (string Name, uint Offset)[] members, (string Name, uint Binding)[] textures)
    {
        var b = new List<byte>(new byte[24]);
        void Name(string s)
        {
            b.AddRange(BitConverter.GetBytes((uint)s.Length));
            b.AddRange(Encoding.ASCII.GetBytes(s));
            while (b.Count % 4 != 0)
            {
                b.Add(0);
            }
        }
        void Nums(params uint[] n) => b.AddRange(n.SelectMany(BitConverter.GetBytes));
        Name(buffer);
        Nums(32, (uint)members.Length);
        foreach (var (name, offset) in members)
        {
            Name(name);
            Nums(0, 1, 4, 0, 0, offset);
        }
        foreach (var (name, binding) in textures)
        {
            Name(name);
            Nums(0, binding | 0x01000000, 0, 2);
        }
        return [.. b];
    }

    /// <summary>
    /// A platform blob: its entries (programs and parameter blobs) after a table of (offset, length, 0) records.
    /// </summary>
    public static byte[] Blob(params byte[][] entries)
    {
        var b = new List<byte>();
        b.AddRange(BitConverter.GetBytes((uint)entries.Length));
        int at = 4 + 12 * entries.Length;
        foreach (byte[] e in entries)
        {
            b.AddRange(BitConverter.GetBytes((uint)at));
            b.AddRange(BitConverter.GetBytes((uint)e.Length));
            b.AddRange(BitConverter.GetBytes(0u));
            at += e.Length;
        }
        foreach (byte[] e in entries)
        {
            b.AddRange(e);
        }
        return [.. b];
    }

    /// <summary>
    /// The names a module gives (OpName and OpMemberName): "id" or "id.member" to the name.
    /// </summary>
    public static Dictionary<string, string> Names(uint[] words)
    {
        var res = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 5; i < words.Length; i += (int)(words[i] >> 16))
        {
            int op = (int)(words[i] & 0xFFFF), len = (int)(words[i] >> 16);
            if (op is 5 or 6)
            {
                int skip = op == 5 ? 2 : 3;
                byte[] text = new byte[(len - skip) * 4];
                Buffer.BlockCopy(words, (i + skip) * 4, text, 0, text.Length);
                string name = Encoding.UTF8.GetString(text).TrimEnd('\0');
                res[op == 5 ? $"{words[i + 1]}" : $"{words[i + 1]}.{words[i + 2]}"] = name;
            }
        }
        return res;
    }
}
