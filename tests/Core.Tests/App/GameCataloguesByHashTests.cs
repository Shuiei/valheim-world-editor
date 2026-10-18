using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The names a ZDO key list is keyed by: each name and its "_u" and "_i" forms (the two halves of a ZDOID value), each
/// under its stable hash.
/// </summary>
public class GameCataloguesByHashTests
{
    private static readonly string[] Expected1 = ["Foo", "Foo_i", "Foo_u"];
    [Fact]
    public void ANameIsKeptWithItsTwoZdoidSuffixes()
    {
        Dictionary<int, string> table = GameCatalogues.ByHash(["Foo"]);

        Assert.Equal(Expected1, table.Values.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void EachEntryIsKeyedByTheStableHashOfItsName()
    {
        Dictionary<int, string> table = GameCatalogues.ByHash(["Foo"]);

        Assert.Equal("Foo_u", table[StableHash.Of("Foo_u")]);
    }
}
