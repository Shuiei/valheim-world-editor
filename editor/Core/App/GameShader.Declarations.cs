// What a SPIR-V module declares, read for putting its names back (see GameShader).

namespace TerrainEditor.App;

public static partial class GameShader
{
    /// <summary>
    /// What a SPIR-V module declares that its names need: its debug names, decorations, member offsets, pointer types,
    /// variables and block structs.
    /// </summary>
    /// <param name="Names">Each id's debug name.</param>
    /// <param name="Decorations">Each id's decorations: the decoration number, then its operands.</param>
    /// <param name="MemberOffsets">Each struct's members' byte offsets, by member index.</param>
    /// <param name="Pointers">Each pointer type's storage class and pointee type.</param>
    /// <param name="Variables">Each variable: its id, its pointer type and its storage class.</param>
    /// <param name="Blocks">The structs decorated as blocks.</param>
    internal sealed record SpirvDeclarations(Dictionary<uint, string> Names, Dictionary<uint, Dictionary<uint, uint[]>> Decorations,
        Dictionary<uint, Dictionary<uint, uint>> MemberOffsets, Dictionary<uint, (uint Storage, uint Type)> Pointers,
        List<(uint Var, uint Ptype, uint Storage)> Variables, HashSet<uint> Blocks)
    {
        /// <summary>
        /// Reads a module's declarations from its instructions.
        /// </summary>
        /// <param name="words">The module.</param>
        public static SpirvDeclarations Scan(uint[] words)
        {
            var names = new Dictionary<uint, string>();
            var deco = new Dictionary<uint, Dictionary<uint, uint[]>>();
            var memberOff = new Dictionary<uint, Dictionary<uint, uint>>();
            var pointer = new Dictionary<uint, (uint Storage, uint Type)>();
            var variables = new List<(uint Var, uint Ptype, uint Storage)>();
            var block = new HashSet<uint>();
            foreach (var (at, op, a) in Instructions(words))
            {
                int len = Length(words, at);
                switch (op)
                {
                    case 5:
                        names[words[a]] = ReadString(words, a + 1, len - 1);
                        break;
                    case 71:
                        if (!deco.TryGetValue(words[a], out var d))
                        {
                            deco[words[a]] = d = [];
                        }
                        d[words[a + 1]] = words.AsSpan(a + 2, len - 2).ToArray();
                        if (words[a + 1] == 2)
                        {
                            block.Add(words[a]);
                        }
                        break;
                    case 72 when words[a + 2] == 35:
                        if (!memberOff.TryGetValue(words[a], out var mo))
                        {
                            memberOff[words[a]] = mo = [];
                        }
                        mo[words[a + 1]] = words[a + 3];
                        break;
                    case 32:
                        pointer[words[a]] = (words[a + 1], words[a + 2]);
                        break;
                    case 59:
                        variables.Add((words[a + 1], words[a], words[a + 2]));
                        break;
                    default:
                        break;
                }
            }
            return new SpirvDeclarations(names, deco, memberOff, pointer, variables, block);
        }
    }
}
