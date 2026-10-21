using System.Globalization;
using ValheimWorldEditor.Sdk;
using WorldEditor.Tests;
using Xunit;

namespace Riverheim.WorldEditor.Tests;

/// <summary>
/// Riverheim's world generator with the real Riverheim (downloaded for the tests, its BepInEx beside it and a stand-in
/// for Unity's classes): the game's own values to the bit; its beta's worlds, a missing Riverheim, a preset it does not
/// know, a Riverheim that cannot lay the world out and a live game on another release refused with why; new worlds
/// given its own preset.
/// </summary>
public sealed class RiverheimGeneratorTests : IDisposable
{
    /// <summary>The reference world's seed: the game's hash of "5DCcdIcuYJ".</summary>
    private const int Seed = 509094289;

    private static readonly GeneratorSettings Amur = RiverheimGenerator.Settings(new RiverheimMod.Tag("builtin.amur", 2));

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-riverheim-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly List<string> _log = [];

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    /// <summary>The editor with Riverheim installed in a BepInEx folder (with its core unless left out) and a game folder.</summary>
    private FakeHost Host(string name, bool core = true)
    {
        string bep = Path.Combine(_dir, name, "BepInEx");
        string dll = TestMods.Install(bep, TestMods.Riverheim);
        if (core)
        {
            TestMods.InstallCore(bep);
        }
        string game = UnityStandIn.Game(Path.Combine(_dir, name, "Valheim"));
        return new FakeHost { Riverheim = new PluginFile(dll, bep), GameManagedFolder = Path.Combine(game, "valheim_Data", "Managed") };
    }

    private RiverheimGenerator Generator(IEditorHost host, string id = RiverheimMod.PluginId) => new(host, id, _log.Add);

    private static float Bits(string hex) => BitConverter.Int32BitsToSingle(int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    private static string Hex(float v) => BitConverter.SingleToInt32Bits(v).ToString("x8", CultureInfo.InvariantCulture);

    [Fact]
    public void TheReferenceWorldIsTheGamesToTheBit()
    {
        var layout = Generator(Host("bep")).Open(Amur, null);
        Assert.Equal(("1.2.1", true), (layout.Version, layout.Slow));
        var ground = layout.LayOut(Seed);
        var lines = File.ReadLines(Path.Combine(AppContext.BaseDirectory, "fixtures", "riverheim-reference.txt")).Where(l => l.StartsWith("H ", StringComparison.Ordinal)).ToList();
        Assert.True(lines.Count > 100);
        Assert.All(lines, line =>
        {
            string[] t = line.Split(' ');
            float x = Bits(t[1]), z = Bits(t[2]);
            float h = ground.Height(x, z, out var mask);
            Assert.Equal(int.Parse(t[3], CultureInfo.InvariantCulture), ground.Biome(x, z));
            // The game's GetHeight is its GetBiomeHeight: the same ground (no other mod's patch in the way).
            Assert.Equal([t[4], t[5], t[6], t[7], t[8], t[9], t[10]], new[] { h, mask.X, mask.Y, mask.Z, mask.W, h, ground.Forest(x, z) }.Select(Hex));
        });
    }

    [Fact]
    public void ItsBetasWorldsAndAMissingRiverheimAreRefused()
    {
        var beta = Generator(new FakeHost(), RiverheimMod.BetaPluginId);
        Assert.Equal(Texts.Beta, Assert.Throws<InvalidDataException>(() => beta.ReadHeader(WorldFile.Reader([]), out _)).Message);
        Assert.Null(beta.NewWorldSettings());
        var none = Generator(new FakeHost());
        Assert.Equal(Texts.Missing, none.Problem(Amur));
        Assert.Equal(Texts.Missing, Assert.Throws<InvalidOperationException>(() => none.Open(Amur, null)).Message);
        Assert.Null(none.NewWorldSettings());
    }

    [Theory]
    [InlineData("builtin.amur", 3, "Unsupported preset version '3' for preset 'builtin.amur'")]
    [InlineData("made.up", 1, "Unknown preset 'made.up'")]
    public void APresetItDoesNotKnowIsRefusedInItsWords(string preset, int version, string words)
    {
        var e = Assert.Throws<InvalidOperationException>(() => Generator(Host("bep")).Open(RiverheimGenerator.Settings(new RiverheimMod.Tag(preset, version)), null));
        Assert.Equal(Texts.Unsupported(preset, version, "1.2.1", words), e.Message);
    }

    [Fact]
    public void ARiverheimThatCannotLayTheWorldOutIsSaid()
    {
        // Without BepInEx's assemblies its own scan fails as the world is laid out.
        var layout = Generator(Host("nocore", core: false)).Open(Amur, null);
        Assert.StartsWith(Texts.LayOutFailed("1.2.1", ""), Assert.Throws<InvalidOperationException>(() => layout.LayOut(Seed)).Message);
    }

    [Fact]
    public void ALiveGameOnAnotherReleaseIsRefused()
    {
        var generator = Generator(Host("bep"));
        Assert.Equal(Texts.OtherVersion("1.1.0", "1.2.1"), Assert.Throws<InvalidOperationException>(() => generator.Open(Amur, "1.1.0")).Message);
        // Another patch of the same release lays worlds out alike.
        Assert.Equal("1.2.1", generator.Open(Amur, "1.2.0").Version);
    }

    [Fact]
    public void NewWorldsGetItsOwnPreset()
    {
        var made = Generator(Host("bep")).NewWorldSettings();
        Assert.Equal(("builtin.amur v2", new RiverheimMod.Tag("builtin.amur", 2)), (made?.Description, made?.Value));
    }
}
