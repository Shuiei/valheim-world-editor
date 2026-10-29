using ValheimWorldEditor.Sdk;
using Xunit;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>The game's terrain operations read from its TerrainOp components, by the hash of their object's name.</summary>
public class TerrainOpsTests
{
    [Fact]
    public void TheGamesHoeStrokesAreFoundByTheirNamesHash()
    {
        var ops = Fixtures.TerrainOps();
        var paved = ops[Hash.Of("paved_road_v2")];
        Assert.Equal((true, 3f, 1f, true, 2, 2.2f), (paved.Smooth, paved.SmoothRadius, paved.SmoothPower, paved.PaintCleared, paved.PaintType, paved.PaintRadius));
        Assert.False(paved.Level);
        Assert.Equal(0.1f, ops[Hash.Of("raise_v2")].RaisePower);
    }

    [Fact]
    public void ComponentsWithoutSettingsAndRepeatedNamesAreLeftOut()
    {
        var first = new Dictionary<string, object?> { ["m_raise"] = 1L, ["m_raisePower"] = 0.5 };
        var again = new Dictionary<string, object?> { ["m_raise"] = 0L };
        var ops = TerrainOps.From(
        [
            new GameComponent("raise", "raise", new Dictionary<string, object?> { ["m_settings"] = first }),
            new GameComponent("raise", "raise", new Dictionary<string, object?> { ["m_settings"] = again }),
            new GameComponent("other", "other", new Dictionary<string, object?>()),
        ]);
        var raise = Assert.Single(ops).Value;
        Assert.True(raise.Raise);
        Assert.Equal(0.5f, raise.RaisePower);
        Assert.Equal(0f, raise.SmoothRadius);
    }
}
