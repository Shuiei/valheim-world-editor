using TerrainEditor.App;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What the log says Expand World does to a Riverheim world where it differs from the game's own: nothing for a world
/// Riverheim did not make or for Riverheim alone; each of Data's world rules, Size's continent settings and biome
/// stretch, a radius that does not match Riverheim's edge, and Rivers' water, once each. The rules' few words name it.
/// </summary>
public class RiverheimNotesTests
{
    private static readonly RiverheimRules Amur = new("1.2.1", "builtin.amur", 2);

    [Fact]
    public void NothingIsSaidOfAPlainWorldOrRiverheimAlone()
    {
        Assert.Empty(Riverheim.Notes(new WorldRules { AltitudeDelta = 10f, Rivers = false }));
        Assert.Empty(Riverheim.Notes(new WorldRules { Riverheim = Amur }));
        // Stretched as Riverheim asks (radius and edge 10500 m times the stretch): nothing either.
        Assert.Empty(Riverheim.Notes(new WorldRules { Riverheim = Amur, SizeInstalled = true, Radius = 20500f, EdgeSize = 500f, Stretch = 2f }));
    }

    [Fact]
    public void EachSettingRiverheimTakesOtherwiseIsSaidOnce()
    {
        var rules = new WorldRules
        {
            Riverheim = Amur,
            SizeInstalled = true,
            DataInstalled = true,
            RiversInstalled = true,
            World = [],
            AltitudeMultiplier = 1.3f,
            OffsetX = 100f,
            BiomeStretch = 2f,
            Radius = 15000f,
        };
        var notes = Riverheim.Notes(rules);
        Assert.Equal(5, notes.Count);
        Assert.Contains(notes, n => n.StartsWith("Expand World Data's world rules choose the biomes", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("Expand World Size's altitude, offsets and height seed", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("Expand World Size's biome stretch", StringComparison.Ordinal));
        Assert.Contains("Riverheim's world ends at 10500 m (10500 m times the stretch) and Expand World Size's radius and edge at 15500 m (Riverheim asks for them to match)", notes);
        Assert.Contains(notes, n => n.StartsWith("Expand World Rivers' lakes, rivers and streams are not laid out", StringComparison.Ordinal));
        Assert.Single(Riverheim.Notes(new WorldRules { Riverheim = Amur, HeightSeed = 12345f }));
    }

    [Fact]
    public void TheRulesFewWordsNameRiverheim()
    {
        Assert.Contains("Riverheim 1.2.1 (builtin.amur v2)", ExpandWorld.Describe(new WorldRules { Riverheim = Amur }));
        Assert.DoesNotContain("Riverheim", ExpandWorld.Describe(new WorldRules { Radius = 20000f }));
    }
}
