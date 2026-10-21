using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>Expand World Data's water colours: none given, and a territory's laid over a biome's.</summary>
public class WaterColoursTests
{
    private static readonly Rgba Red = new(1f, 0f, 0f, 1f), Blue = new(0f, 0f, 1f, 1f);

    [Fact]
    public void NoneHasNoColour()
    {
        Assert.True(WaterColours.None.IsNone);
        Assert.False(new WaterColours(Surface: Red).IsNone);
    }

    [Fact]
    public void TheColoursLaidOverReplaceOnlyThoseTheyGive()
    {
        var biome = new WaterColours(Top: Red, Bottom: Red);
        Assert.Equal(new WaterColours(Red, Blue, Blue), biome.Under(new WaterColours(Bottom: Blue, Shallow: Blue)));
        Assert.Same(biome, biome.Under(null));
    }
}
