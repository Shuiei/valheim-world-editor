using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>What world rules change, in a few words (WorldRulesText, the SDK's): for the log and the New world page.</summary>
public class WorldRulesTextTests
{
    [Fact]
    public void TheRulesAreDescribedInAFewWords()
    {
        Assert.Equal("the game's own values", WorldRulesText.Describe(WorldRules.Vanilla));
        string text = WorldRulesText.Describe(new WorldRules { Radius = 20000f, Stretch = 2f, AltitudeDelta = 5f, Rivers = false });
        Assert.Contains("radius 20000 + 500 m", text);
        Assert.Contains("stretch 2", text);
        Assert.Contains("+5 m", text);
        Assert.Contains("rivers off", text);
    }
}
