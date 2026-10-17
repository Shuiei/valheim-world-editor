using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The rounding and comparing of the game data's values (GameCatalogues.Round, R and Max): floats are rounded as Python
/// rounds them, whole numbers and nothing are left as they are, and of two equal values the first is kept.
/// </summary>
public class GameCataloguesFieldValuesTests
{
    [Fact]
    public void AFloatIsRoundedToTheDecimalsAsked() => Assert.Equal(2.46, GameCatalogues.Round(2.4567, 2));

    [Fact]
    public void AWholeNumberIsLeftAsItIs() => Assert.Equal(7L, GameCatalogues.Round(7L, 2));

    [Fact]
    public void NothingIsLeftAsNothing() => Assert.Null(GameCatalogues.Round(null, 2));

    [Fact]
    public void RoundsHalfwayToEven()
    {
        Assert.Equal(2.0, GameCatalogues.R(2.5, 0));
        Assert.Equal(4.0, GameCatalogues.R(3.5, 0));
    }

    [Fact]
    public void TheLargerOfTwoValuesIsTakenWhicheverComesFirst()
    {
        Assert.Equal(3.0, GameCatalogues.Max(1.0, 3.0));
        Assert.Equal(3.0, GameCatalogues.Max(3.0, 1.0));
    }

    [Fact]
    public void OfEqualValuesTheFirstIsKept()
    {
        Assert.True(double.IsNegative(GameCatalogues.Max(-0.0, 0.0)));
    }
}
