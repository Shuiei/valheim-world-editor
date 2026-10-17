using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The instruction decoder (SmolvDecoder) on a SMOL-V program made by SmolvWriter: the words written back are the ones
/// encoded, and a program that ends early is said.
/// </summary>
public class SmolvDecoderTests
{
    private static uint[] Module(params uint[] capability)
    {
        uint[] header = [0x07230203, 0x00010300, 7, 40, 0];
        return [.. header, (2u << 16) | 17u, .. capability];
    }

    [Fact]
    public void ADecodedInstructionIsWrittenBackAsSpirv()
    {
        uint[] spirv = Module(1);
        byte[] data = SmolvWriter.Encode(spirv);
        var output = new List<uint>(spirv[..5]);
        var decoder = new SmolvDecoder(data, 24, output, spirv.Length, 367);
        decoder.Run();
        Assert.Equal(spirv, output);
    }

    [Fact]
    public void TheEndOfTheReadDataIsTheEndOfTheProgram()
    {
        uint[] spirv = Module(1);
        byte[] data = SmolvWriter.Encode(spirv);
        var output = new List<uint>(spirv[..5]);
        var decoder = new SmolvDecoder(data, 24, output, spirv.Length, 367);
        decoder.Run();
        Assert.Equal(data.Length, decoder.End);
    }

    [Fact]
    public void AProgramThatEndsBeforeItsWordsIsRefused()
    {
        uint[] spirv = Module(1);
        byte[] data = SmolvWriter.Encode(spirv)[..24];
        var output = new List<uint>(spirv[..5]);
        var decoder = new SmolvDecoder(data, 24, output, spirv.Length, 367);
        Assert.Throws<InvalidDataException>(decoder.Run);
    }
}
