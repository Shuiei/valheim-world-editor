using System.Globalization;
using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A world made with Riverheim, drawn with the real Riverheim (downloaded for the tests, its BepInEx beside it and a
/// stand-in for Unity's classes): the game's own values to the bit, a preset it does not know refused with its words,
/// a Riverheim that cannot lay the world out said as such, a live game on another release refused, and new worlds laid
/// out with its own preset.
/// </summary>
[Collection("DataDir")]
public class RiverheimRulesTests : AppSettingsTestBase
{
    private static readonly Riverheim.Tag Amur = new("builtin.amur", 2);

    /// <summary>Settings with Riverheim on, installed in a BepInEx folder (with its core unless left out), and a game folder.</summary>
    private AppSettings Settings(string name, bool core = true)
    {
        string bep = Path.Combine(Data, name, "BepInEx");
        TestMods.Install(bep, TestMods.Riverheim);
        if (core)
        {
            TestMods.InstallCore(bep);
        }
        return new AppSettings { Riverheim = true, ExpandWorldFolder = bep, ValheimPath = UnityStandIn.Game(Path.Combine(Data, name, "Valheim")) };
    }

    /// <summary>A world's generator with these Riverheim rules.</summary>
    private static WorldGenerator Generator(string seedName, RiverheimRules? riverheim) =>
        WorldGenerator.Create(new World { Seed = StableHash.Of(seedName), SeedName = seedName, Rules = new WorldRules { Riverheim = riverheim } });

    private static float Bits(string hex) => BitConverter.Int32BitsToSingle(int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    [Fact]
    public void TheReferenceWorldIsTheGamesToTheBit()
    {
        var rules = Riverheim.Rules(Amur, Settings("bep"));
        Assert.NotNull(rules);
        Assert.Equal(("1.2.1", "builtin.amur", 2), (rules.Version, rules.Preset, rules.PresetVersion));
        var gen = Generator("5DCcdIcuYJ", rules);
        // Riverheim lays its own world out: no lakes, rivers or streams of the game's.
        Assert.Empty(gen.GetLakes());
        Assert.Empty(gen.GetRivers());
        var lines = File.ReadLines(Path.Combine(Fixtures.Root, "riverheim-reference.txt")).Where(l => l.StartsWith("H ", StringComparison.Ordinal)).ToList();
        Assert.True(lines.Count > 100);
        Assert.All(lines, line =>
        {
            string[] t = line.Split(' ');
            float x = Bits(t[1]), z = Bits(t[2]);
            var biome = gen.GetBiome(x, z);
            float h = gen.GetBiomeHeight(biome, x, z, out var mask);
            Assert.Equal(int.Parse(t[3], CultureInfo.InvariantCulture), (int)biome);
            Assert.Equal([t[4], t[5], t[6], t[7], t[8], t[9], t[10]], new[] { h, mask.R, mask.G, mask.B, mask.A, gen.GetHeight(x, z), gen.GetForestFactor(new Vector3(x, 0f, z)) }.Select(v => BitConverter.SingleToInt32Bits(v).ToString("x8", CultureInfo.InvariantCulture)));
        });
    }

    [Theory]
    [InlineData("builtin.amur", 3, "Unsupported preset version '3' for preset 'builtin.amur'")]
    [InlineData("made.up", 1, "Unknown preset 'made.up'")]
    public void APresetItDoesNotKnowIsRefusedInItsWords(string preset, int version, string words)
    {
        var e = Assert.Throws<InvalidOperationException>(() => Riverheim.Rules(new Riverheim.Tag(preset, version), Settings("bep")));
        Assert.Equal(Lang.T("riverheim.unsupported", preset, version, "1.2.1", words), e.Message);
    }

    [Fact]
    public void ARiverheimThatCannotLayTheWorldOutIsSaid()
    {
        // Without BepInEx's assemblies its own scan fails as the world is laid out.
        var rules = Riverheim.Rules(Amur, Settings("nocore", core: false));
        var e = Assert.Throws<InvalidOperationException>(() => Generator("5DCcdIcuYJ", rules));
        Assert.StartsWith(Lang.T("riverheim.layOutFailed", "1.2.1", ""), e.Message);
    }

    [Fact]
    public void NewWorldsAreLaidOutByRiverheimWhileItsSwitchIsOn()
    {
        var settings = Settings("bep");
        var riverheim = Riverheim.ForNewWorld(settings).Riverheim;
        Assert.NotNull(riverheim);
        Assert.Equal(("1.2.1", "builtin.amur", 2), (riverheim.Version, riverheim.Preset, riverheim.PresetVersion));
        settings.Riverheim = false;
        Assert.True(Riverheim.ForNewWorld(settings).IsVanilla);
    }

    [Fact]
    public void ALiveGameOnAnotherReleaseIsRefused()
    {
        var settings = Settings("bep");
        Assert.Equal(Lang.T("riverheim.otherVersion", "1.1.0", "1.2.1"), Assert.Throws<InvalidOperationException>(() => Riverheim.Rules(Amur, settings, "1.1.0")).Message);
        // Another patch of the same release lays worlds out alike.
        Assert.NotNull(Riverheim.Rules(Amur, settings, "1.2.0"));
    }

    [Theory]
    [InlineData("1.2.1", "1.2.0", true)]
    [InlineData("1.2.1", "1.2.1", true)]
    [InlineData("1.1.1", "1.2.1", false)]
    [InlineData("2.2.1", "1.2.1", false)]
    [InlineData("beta", "1.2.1", false)]
    public void ARiverheimReleaseIsItsMajorAndMinorVersion(string a, string b, bool same)
    {
        Assert.Equal(same, Riverheim.SameRelease(a, b));
    }

    [Fact]
    public void AWorldItDidNotMakeHasNoRiverheimRules()
    {
        Assert.Null(Riverheim.Rules(null, new AppSettings()));
    }
}
