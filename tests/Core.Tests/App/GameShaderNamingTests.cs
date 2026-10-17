using System.Text;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Putting names back into a Vulkan module: its declarations are read from the instructions, the uniform buffer's
/// members and the textures, inputs and outputs are named, and the names go in before the decorations.
/// </summary>
public class GameShaderNamingTests
{
    /// <summary>
    /// The first word of an instruction: its word count, then its opcode.
    /// </summary>
    private static uint Head(int words, int op) => (uint)(words << 16 | op);

    /// <summary>
    /// A SPIR-V literal string of up to three characters, as one word (nul-padded).
    /// </summary>
    private static uint Word(string s)
    {
        byte[] b = new byte[4];
        Encoding.UTF8.GetBytes(s, 0, s.Length, b, 0);
        return BitConverter.ToUInt32(b, 0);
    }

    /// <summary>
    /// The text of a literal string given as words, up to its nul.
    /// </summary>
    private static string Text(IEnumerable<uint> words)
    {
        byte[] bytes = [.. words.SelectMany(BitConverter.GetBytes).TakeWhile(x => x != 0)];
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// The decorations of one id: a single decoration of it, by number and operands.
    /// </summary>
    private static Dictionary<uint, Dictionary<uint, uint[]>> Decorated(uint id, uint decoration, uint[] operands)
    {
        return new Dictionary<uint, Dictionary<uint, uint[]>> { [id] = new Dictionary<uint, uint[]> { [decoration] = operands } };
    }

    /// <summary>
    /// A module: the five-word header, then the instructions.
    /// </summary>
    private static uint[] Module(params uint[][] instructions)
    {
        uint[] header = [0x07230203, 0x00010000, 0, 100, 0];
        return [.. header, .. instructions.SelectMany(i => i)];
    }

    /// <summary>
    /// A module with a named debug string, a binding, a block struct with one member at offset 16, and a uniform
    /// variable of that block.
    /// </summary>
    private static uint[] BlockModule(uint storage) => Module(
        [Head(3, 5), 10u, Word("ab")],
        [Head(4, 71), 20u, 33u, 0u],
        [Head(4, 72), 30u, 0u, 35u, 16u],
        [Head(3, 71), 30u, 2u],
        [Head(4, 32), 40u, 2u, 30u],
        [Head(4, 59), 40u, 50u, storage]);

    [Fact]
    public void ScanReadsNamesDecorationsOffsetsPointersVariablesAndBlocks()
    {
        var decl = GameShader.SpirvDeclarations.Scan(BlockModule(2));
        Assert.Equal("ab", decl.Names[10]);
        Assert.Equal([0], decl.Decorations[20][33]);
        Assert.Equal(16u, decl.MemberOffsets[30][0]);
        Assert.Equal((2u, 30u), decl.Pointers[40]);
        Assert.Equal(new List<(uint, uint, uint)> { (50, 40, 2) }, decl.Variables);
        Assert.Contains(30u, decl.Blocks);
    }

    [Fact]
    public void BlockMembersAndTheirBlockGetNames()
    {
        var decl = GameShader.SpirvDeclarations.Scan(BlockModule(2));
        var prm = new GameShader.Params(new() { ["_Globals"] = new() { [16] = "_Color" } }, []);
        var add = new List<(uint Op, List<uint> Words)>();
        GameShader.AddBlockMemberNames(decl, prm, add);
        Assert.Equal([6, 5], add.Select(a => a.Op).ToArray());
        Assert.Equal([30, 0], add[0].Words.Take(2).ToArray());
        Assert.Equal("_Color", Text(add[0].Words.Skip(2)));
        Assert.Equal(50u, add[1].Words[0]);
        Assert.Equal("_Globals", Text(add[1].Words.Skip(1)));
    }

    [Fact]
    public void BlockMembersAreNotNamedForVariablesOutsideTheUniformStorage()
    {
        var decl = GameShader.SpirvDeclarations.Scan(BlockModule(0));
        var add = new List<(uint Op, List<uint> Words)>();
        GameShader.AddBlockMemberNames(decl, new GameShader.Params([], []), add);
        Assert.Empty(add);
    }

    [Fact]
    public void TexturesAreNamedByTheirBinding()
    {
        var decl = new GameShader.SpirvDeclarations([], Decorated(60, 33, [1]), [], [], [(60, 0, 0)], []);
        var add = new List<(uint Op, List<uint> Words)>();
        GameShader.AddVariableNames(decl, new GameShader.Params([], new() { [1] = "_MainTex" }), add);
        var (op, words) = Assert.Single(add);
        Assert.Equal(5u, op);
        Assert.Equal(60u, words[0]);
        Assert.Equal("_MainTex", Text(words.Skip(1)));
    }

    [Fact]
    public void InputsWithALocationAreNamedVsColor()
    {
        var decl = new GameShader.SpirvDeclarations([], [], [], [], [(61, 0, 1)], []);
        var add = new List<(uint Op, List<uint> Words)>();
        GameShader.AddVariableNames(decl, new GameShader.Params([], []), add);
        Assert.Equal("vs_COLOR0", Text(Assert.Single(add).Words.Skip(1)));
    }

    [Fact]
    public void OutputsAreNamedByTheirLocation()
    {
        var decl = new GameShader.SpirvDeclarations([], Decorated(62, 30, [2]), [], [], [(62, 0, 3)], []);
        var add = new List<(uint Op, List<uint> Words)>();
        GameShader.AddVariableNames(decl, new GameShader.Params([], []), add);
        Assert.Equal("SV_Target2", Text(Assert.Single(add).Words.Skip(1)));
    }

    [Fact]
    public void AnInputThatAlreadyHasADebugNameIsLeftAlone()
    {
        var decl = new GameShader.SpirvDeclarations(new() { [61] = "vs_Named" }, [], [], [], [(61, 0, 1)], []);
        var add = new List<(uint Op, List<uint> Words)>();
        GameShader.AddVariableNames(decl, new GameShader.Params([], []), add);
        Assert.Empty(add);
    }

    [Fact]
    public void NamesGoInBeforeTheFirstDecoration()
    {
        uint[] words = Module([Head(4, 71), 20u, 33u, 0u]);
        List<(uint Op, List<uint> Words)> add = [(5u, [20u, Word("A")])];
        uint[] output = GameShader.InsertNames(words, add);
        Assert.Equal(words.Length + 3, output.Length);
        Assert.Equal(Head(3, 5), output[5]);
        Assert.Equal(20u, output[6]);
        Assert.Equal(Head(4, 71), output[8]);
    }

    [Fact]
    public void ModuleWithoutDecorationsGetsNoNames()
    {
        uint[] words = Module([Head(3, 5), 10u, Word("ab")]);
        List<(uint Op, List<uint> Words)> add = [(5u, [20u, Word("A")])];
        Assert.Equal(words, GameShader.InsertNames(words, add));
    }
}
