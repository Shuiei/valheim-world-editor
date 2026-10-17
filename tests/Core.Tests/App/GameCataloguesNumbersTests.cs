using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game data's values read as numbers and as truth (GameCatalogues.Num, Int and Truthy), as Python reads them: a bool is
/// a number, a float is cut toward zero as an integer, and an empty value is not true.
/// </summary>
public class GameCataloguesNumbersTests
{
    [Fact]
    public void ANumberIsReadAsADouble() => Assert.Equal(5.0, GameCatalogues.Num(5L));

    [Fact]
    public void ABoolIsReadAsZeroOrOne()
    {
        Assert.Equal(1.0, GameCatalogues.Num(true));
        Assert.Equal(0.0, GameCatalogues.Num(false));
    }

    [Fact]
    public void ATextIsNotANumber() => Assert.Throws<InvalidDataException>(() => GameCatalogues.Num("x"));

    [Fact]
    public void AFloatIsCutTowardZeroAsAnInteger()
    {
        Assert.Equal(2L, GameCatalogues.Int(2.9));
        Assert.Equal(-2L, GameCatalogues.Int(-2.9));
    }

    [Fact]
    public void ABoolIsReadAsAnIntegerZeroOrOne()
    {
        Assert.Equal(1L, GameCatalogues.Int(true));
        Assert.Equal(0L, GameCatalogues.Int(false));
    }

    [Fact]
    public void ATextIsNotAnInteger() => Assert.Throws<InvalidDataException>(() => GameCatalogues.Int("x"));

    [Fact]
    public void NothingIsNotTrue() => Assert.False(GameCatalogues.Truthy(null));

    [Fact]
    public void ZeroAndEmptyValuesAreNotTrue()
    {
        Assert.False(GameCatalogues.Truthy(0L));
        Assert.False(GameCatalogues.Truthy(0.0));
        Assert.False(GameCatalogues.Truthy(""));
        Assert.False(GameCatalogues.Truthy(new List<object?>()));
        Assert.False(GameCatalogues.Truthy(new CatalogueJson.Dict()));
    }

    [Fact]
    public void ANonEmptyValueIsTrue()
    {
        Assert.True(GameCatalogues.Truthy(3L));
        Assert.True(GameCatalogues.Truthy("a"));
        Assert.True(GameCatalogues.Truthy(new List<object?> { 1L }));
        var dict = new CatalogueJson.Dict { ["a"] = 1L };
        Assert.True(GameCatalogues.Truthy(dict));
    }
}
