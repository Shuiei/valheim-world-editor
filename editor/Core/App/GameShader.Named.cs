// Putting names back into a Vulkan module: the uniform buffer, the textures and the inputs and outputs (see GameShader).

namespace TerrainEditor.App;

public static partial class GameShader
{
    /// <summary>
    /// The SPIR-V with names for the uniform buffer's members, the textures, the inputs (the unnamed one
    /// is vs_COLOR0, as in the OpenGL program) and the outputs (SV_Target0..3).
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The uniform buffer or a texture binding has no match in <paramref name="prm"/>.
    /// </exception>
    internal static uint[] Named(uint[] words, Params prm)
    {
        var decl = SpirvDeclarations.Scan(words);
        var add = new List<(uint Op, List<uint> Words)>();
        AddBlockMemberNames(decl, prm, add);
        AddVariableNames(decl, prm, add);
        return InsertNames(words, add);
    }

    /// <summary>
    /// The names of a module's uniform buffer members, and the buffer's own name (_Globals).
    /// </summary>
    /// <param name="decl">The module's declarations.</param>
    /// <param name="prm">The names of the parameter blob.</param>
    /// <param name="add">The instructions to add to the module, collected here.</param>
    internal static void AddBlockMemberNames(SpirvDeclarations decl, Params prm, List<(uint Op, List<uint> Words)> add)
    {
        foreach (var (v, ptype, storage) in decl.Variables)
        {
            uint st = decl.Pointers.TryGetValue(ptype, out var pt) ? pt.Type : uint.MaxValue;
            if (storage != 2 || !decl.Blocks.Contains(st))
            {
                continue;
            }
            var offs = decl.MemberOffsets.GetValueOrDefault(st) ?? [];
            var buf = prm.Buffers.Values.FirstOrDefault(b => offs.Values.All(b.ContainsKey))
                ?? throw new InvalidDataException("The terrain shader's uniform buffer matches none of its parameter blob's.");
            foreach (var (idx, o) in offs)
            {
                add.Add((6, new List<uint> { st, idx }.Concat(StringWords(buf[o])).ToList()));
            }
            add.Add((5, new List<uint> { v }.Concat(StringWords("_Globals")).ToList()));
        }
    }

    /// <summary>
    /// The module with the names inserted after its existing debug names, before the first decoration.
    /// </summary>
    /// <param name="words">The module.</param>
    /// <param name="add">The instructions to insert.</param>
    internal static uint[] InsertNames(uint[] words, List<(uint Op, List<uint> Words)> add)
    {
        var output = new List<uint>(words.Length + 64);
        output.AddRange(words.AsSpan(0, 5).ToArray());
        bool done = false;
        foreach (var (at, op, _) in Instructions(words))
        {
            if (!done && op is 71 or 72 or 73 or 74 or 75 or 332 or 5632 or 5633)
            {
                foreach (var (o, ws) in add)
                {
                    output.Add(((uint)(ws.Count + 1) << 16) | o);
                    output.AddRange(ws);
                }
                done = true;
            }
            output.AddRange(words.AsSpan(at, Length(words, at) + 1).ToArray());
        }
        return [.. output];
    }
}
