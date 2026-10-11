using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The area's ground (Ground) with No limit: what is past the game's ±8 m goes into the lift, and the limit counts from
/// the lifted ground.
/// </summary>
public class GroundTests
{
    [Fact]
    public void NoLimitPutsWhatIsPastTheLimitInTheLift()
    {
        var g = new Ground(65, 65, 0, 0, 1);
        int p = 32 * 65 + 32;
        g.Base[p] = 30;
        g.NoLimit = true;
        Assert.False(g.SetHeight(p, 60));
        Assert.Equal(60, g.HeightOf(p), 0.001f);
        Assert.Equal(30, g.Lift[p], 0.001f);
        Assert.False(g.AtLimit(p));
        // With the limit back, the ±8 m count from the lifted ground.
        g.NoLimit = false;
        Assert.True(g.SetHeight(p, 80));
        Assert.Equal(68, g.HeightOf(p), 0.001f);
        Assert.True(g.AtLimit(p));
        var e = g.ZoneEdit(0, 0);
        Assert.Equal(30, e.Lift[p], 0.001f);
        Assert.True(e.HasLift);
    }
}
