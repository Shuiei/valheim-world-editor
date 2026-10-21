using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>When a copy has nothing a paste would put down (CopyData.Empty): no objects, no ground, nothing mods keep.</summary>
public class CopyDataEmptyTests
{
    private static CopyData Bare() => new() { W = 1, H = 1, Rel = [float.NaN], Wt = [0f], Pnt = [-1f, -1, -1, -1], Objects = [], Poly = [] };

    [Fact]
    public void OnlyACopyWithNothingIsEmpty()
    {
        Assert.True(Bare().Empty);
        var ground = new CopyData { W = 1, H = 1, Rel = [0f], Wt = [1f], Pnt = [-1f, -1, -1, -1], Objects = [], Poly = [] };
        Assert.False(ground.Empty);
        var tree = new CopyData { W = 1, H = 1, Rel = [float.NaN], Wt = [0f], Pnt = [-1f, -1, -1, -1], Objects = [new(1, "Beech1", 0, 0, 0, Vector3.Zero, 1, null)], Poly = [] };
        Assert.False(tree.Empty);
        var kept = Bare();
        kept.Kept = new Dictionary<string, byte[]> { ["Mod/x"] = [1] };
        Assert.False(kept.Empty);
    }
}
