using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The block cache's position (GameCatalogues.BlockCache.Seek): from the start, from the current place, or from the end
/// of the bundle's data, and the place it leaves the stream at.
/// </summary>
public class GameCataloguesBlockCacheSeekTests
{
    /// <summary>A block cache over a stream of that many zero bytes.</summary>
    private static Stream Cache(int length) =>
        (Stream)PrivateMember.New(PrivateMember.Nested(typeof(GameCatalogues), "BlockCache"), new MemoryStream(new byte[length]));

    [Fact]
    public void SeekFromTheBeginningGoesToThatOffset()
    {
        var cache = Cache(10);

        Assert.Equal(4, cache.Seek(4, SeekOrigin.Begin));
        Assert.Equal(4, cache.Position);
    }

    [Fact]
    public void SeekFromTheCurrentPlaceAddsTheOffset()
    {
        var cache = Cache(10);
        cache.Seek(4, SeekOrigin.Begin);

        Assert.Equal(7, cache.Seek(3, SeekOrigin.Current));
    }

    [Fact]
    public void SeekFromTheEndCountsBackFromTheLength()
    {
        var cache = Cache(10);

        Assert.Equal(8, cache.Seek(-2, SeekOrigin.End));
    }
}
