using ValheimWorldEditor.Sdk;
using Xunit;

namespace Riverheim.WorldEditor.Tests;

/// <summary>
/// What the log says Expand World does to a Riverheim world where it differs from the game's own: nothing for Riverheim
/// alone or stretched as it asks; each of Data's world rules, Size's continent settings and biome stretch, a radius that
/// does not match Riverheim's edge, and Rivers' water, once each.
/// </summary>
public class RiverheimNotesTests
{
    [Fact]
    public void NothingIsSaidOfRiverheimAloneOrStretchedAsItAsks()
    {
        Assert.Empty(RiverheimMod.Notes(new ExpandWorldSettings()));
        // Stretched as Riverheim asks (radius and edge 10500 m times the stretch): nothing either.
        Assert.Empty(RiverheimMod.Notes(new ExpandWorldSettings { Radius = 21000f, Stretch = 2f }));
    }

    [Fact]
    public void EachSettingRiverheimTakesOtherwiseIsSaidOnce()
    {
        var notes = RiverheimMod.Notes(new ExpandWorldSettings { WorldData = true, ShapesGround = true, BiomeStretch = 2f, Radius = 15500f, Rivers = true });
        Assert.Equal(5, notes.Count);
        Assert.Contains(notes, n => n.StartsWith("Expand World Data's world rules choose the biomes", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("Expand World Size's altitude, offsets and height seed", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("Expand World Size's biome stretch", StringComparison.Ordinal));
        Assert.Contains("Riverheim's world ends at 10500 m (10500 m times the stretch) and Expand World Size's radius and edge at 15500 m (Riverheim asks for them to match)", notes);
        Assert.Contains(notes, n => n.StartsWith("Expand World Rivers' lakes, rivers and streams are not laid out", StringComparison.Ordinal));
    }
}
