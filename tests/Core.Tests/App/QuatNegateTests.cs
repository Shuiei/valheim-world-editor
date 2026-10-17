using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// GameCatalogues.Quat's negation of a part (used when a rotation is turned): a whole rotation's zero stays a plain zero,
/// where any other turns it into a negative zero that would show in the files.
/// </summary>
public class QuatNegateTests
{
    [Fact]
    public void NegatingAPartFlipsItsSign()
    {
        var q = new GameCatalogues.Quat(2, 0, 0, 1);

        Assert.Equal(-2.0, PrivateMember.Call<double>(typeof(GameCatalogues.Quat), "Neg", q, 2.0));
    }

    [Fact]
    public void NegatingAZeroOfAWholeRotationGivesAPlainZero()
    {
        var root = GameCatalogues.Quat.Root;

        double negated = PrivateMember.Call<double>(typeof(GameCatalogues.Quat), "Neg", root, 0.0);

        Assert.False(double.IsNegative(negated));
    }

    [Fact]
    public void NegatingAZeroOfAnOrdinaryRotationGivesANegativeZero()
    {
        var q = new GameCatalogues.Quat(0, 0, 0, 1);

        double negated = PrivateMember.Call<double>(typeof(GameCatalogues.Quat), "Neg", q, 0.0);

        Assert.True(double.IsNegative(negated));
    }
}
