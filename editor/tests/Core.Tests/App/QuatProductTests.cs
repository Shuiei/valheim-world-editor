using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// GameCatalogues.Quat.Mul: the product of two rotations, the right one applied first (Hamilton product).
/// </summary>
public class QuatProductTests
{
    [Fact]
    public void TheIdentityLeavesARotationUnchanged()
    {
        var q = new GameCatalogues.Quat(0.6, 0.0, 0.8, 0.0);

        var product = GameCatalogues.Quat.Root.Mul(q);

        Assert.Equal(q.X, product.X, 12);
        Assert.Equal(q.Y, product.Y, 12);
        Assert.Equal(q.Z, product.Z, 12);
        Assert.Equal(q.W, product.W, 12);
    }

    [Fact]
    public void TwoQuarterTurnsAboutTheSameAxisMakeAHalfTurn()
    {
        // A quarter turn about z is (0, 0, sin 45°, cos 45°); twice it is a half turn about z, (0, 0, 1, 0).
        double half = Math.Sqrt(0.5);
        var quarter = new GameCatalogues.Quat(0, 0, half, half);

        var product = quarter.Mul(quarter);

        Assert.Equal(0, product.X, 12);
        Assert.Equal(0, product.Y, 12);
        Assert.Equal(1, product.Z, 12);
        Assert.Equal(0, product.W, 12);
    }
}
