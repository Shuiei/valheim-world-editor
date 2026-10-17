using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// SMOL-V decoded back to the SPIR-V it was made from (Smolv), for both encoding versions and every way an
/// instruction can be written (SmolvWriter makes the SMOL-V); and broken SMOL-V said rather than decoded wrong.
/// </summary>
public class SmolvTests
{
    /// <summary>
    /// A SPIR-V header then these instructions (each its words without the length and op word: added here).
    /// </summary>
    private static uint[] Module(params (ushort Op, uint[] Args)[] instructions)
    {
        var w = new List<uint> { 0x07230203, 0x00010300, 7, 40, 0 };
        foreach (var (op, args) in instructions)
        {
            w.Add(((uint)(args.Length + 1) << 16) | op);
            w.AddRange(args);
        }
        return [.. w];
    }

    /// <summary>
    /// Instructions SMOL-V writes in its special ways: loads and access chains (shorter lengths), vector shuffles
    /// compact and not, member decorations merged (offsets as deltas, decorations with no, one or more literals), and
    /// an op later than the encoding knows (plain words).
    /// </summary>
    private static readonly uint[] Special = Module(
        (61, [6, 21, 19]),
        (61, [6, 22, 19, 2]),
        (65, [12, 23, 13, 8]),
        (79, [6, 24, 21, 22, 0, 3]),
        (79, [6, 25, 21, 22, 1, 2, 3, 0]),
        (79, [6, 26, 21, 22, 0xFFFFFFFF, 1]),
        (72, [11, 0, 35, 0]),
        (72, [11, 1, 35, 16]),
        (72, [11, 1, 0]),
        (72, [11, 2, 11, 5]),
        (72, [11, 3, 30, 2]),
        (71, [11, 2]),
        (5632, [11, 1, 2]));

    [Fact]
    public void BothEncodingsDecodeToTheSameProgram()
    {
        foreach (int version in new[] { 0, 1 })
        {
            foreach (uint[] spirv in new[] { TestSpirv.Program(), Special })
            {
                byte[] smolv = SmolvWriter.Encode(spirv, version);
                Assert.Equal(spirv.Length * 4, Smolv.DecodedSize(smolv));
                Assert.Equal(spirv, Smolv.Decode(smolv));
            }
        }
        // An op the first version does not know (later than its table): plain words there.
        uint[] late = Module((17, [1]), (340, [4, 5]));
        Assert.Equal(late, Smolv.Decode(SmolvWriter.Encode(late, 0)));
    }

    [Fact]
    public void EveryProgramOfABlobIsFound()
    {
        byte[] a = SmolvWriter.Encode(TestSpirv.Program()), b = SmolvWriter.Encode(Special);
        // The magic alone, too short to be a program, between them: passed over.
        byte[] blob = [1, 2, 3, .. a, .. "LOMS"u8, 9, .. b];
        var found = Smolv.Programs(blob).ToList();
        Assert.Equal(2, found.Count);
        Assert.Equal(3, found[0].At);
        Assert.Equal(TestSpirv.Program(), found[0].Words);
        Assert.Equal(Special, found[1].Words);
        Assert.Equal(0, Smolv.DecodedSize("LOMS"u8.ToArray()));
    }

    [Fact]
    public void TheNextProgramIsLookedForAfterTheOneDecoded()
    {
        // A name whose words read as a whole SMOL-V header (magic, version 1, size 20): taken for a program before.
        uint[] named = Module((5, [1, 0x534D4F4C, 0x01000000, 0, 0, 0, 20]));
        byte[] a = SmolvWriter.Encode(named), b = SmolvWriter.Encode(Special);

        var found = Smolv.Programs([.. a, .. b]).ToList();

        Assert.Equal([0, a.Length], found.Select(f => f.At));
        Assert.Equal(named, found[0].Words);
        Assert.Equal(Special, found[1].Words);
    }

    [Fact]
    public void ASizeTheDataCannotHoldIsNoProgram()
    {
        byte[] b = SmolvWriter.Encode(Special);
        // A chance "LOMS" with a size past int's range, then one too large for the bytes after it, before a program.
        byte[] huge = [.. "LOMS"u8, .. new byte[16], 0, 0, 0, 0x80], large = [.. "LOMS"u8, .. new byte[16], 0, 0, 1, 0];

        Assert.Equal(0, Smolv.DecodedSize(huge));
        Assert.Contains("ends early", Assert.Throws<InvalidDataException>(() => Smolv.Decode(large)).Message);
        Assert.Equal(Special, Assert.Single(Smolv.Programs([.. huge, .. large, .. b])).Words);
    }

    [Fact]
    public void BrokenSmolvIsSaid()
    {
        byte[] good = SmolvWriter.Encode(Special);
        // An encoding version not known.
        byte[] version = (byte[])good.Clone();
        version[7] = 2;
        Assert.Contains("version 2", Assert.Throws<InvalidDataException>(() => Smolv.Decode(version)).Message);
        // Cut short, in a varint and in a word.
        Assert.Contains("ends early", Assert.Throws<InvalidDataException>(() => Smolv.Decode(good[..30])).Message);
        Assert.Contains("ends early", Assert.Throws<InvalidDataException>(() => Smolv.Decode(good[..^2])).Message);
        // Ending where a member decoration's count or a compact shuffle's components would be.
        byte[] header = [.. "LOMS"u8, .. new uint[] { 0x01010000, 0, 20, 0, 40 }.SelectMany(BitConverter.GetBytes)];
        Assert.Throws<InvalidDataException>(() => Smolv.Decode([.. header, 0x47, 0x16]));
        Assert.Throws<InvalidDataException>(() => Smolv.Decode([.. header, 0x2D, 6, 48, 6, 4]));
        // A size that ends inside an instruction.
        byte[] size = (byte[])good.Clone();
        BitConverter.GetBytes((uint)(Special.Length * 4 - 4)).CopyTo(size, 20);
        Assert.Contains("wrong size", Assert.Throws<InvalidDataException>(() => Smolv.Decode(size)).Message);
    }
}
