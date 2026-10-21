using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Reading through the block cache (GameCatalogues.BlockCache): a read across two blocks, a block read again from the
/// cache (the last one used, or an earlier one brought to the front), the block used longest ago let go once the cache is
/// full, and the stream's shape (read only, seekable, nothing to flush, no writing).
/// </summary>
public class GameCataloguesBlockCacheReadTests
{
    /// <summary>A block's size (as the cache has it): 128 KiB.</summary>
    private const int Block = 1 << 17;

    /// <summary>How many blocks the cache keeps at most.</summary>
    private const int Most = 512;

    /// <summary>A block cache over this stream.</summary>
    private static Stream Cache(Stream inner) => (Stream)PrivateMember.New(PrivateMember.Nested(typeof(GameCatalogues), "BlockCache"), inner);

    /// <summary>A stream of that many bytes, each byte the low byte of its block's index.</summary>
    private static MemoryStream Numbered(long length)
    {
        byte[] data = new byte[length];
        for (long i = 0; i < length; i += Block)
        {
            data.AsSpan((int)i, (int)Math.Min(Block, length - i)).Fill((byte)(i / Block));
        }
        return new MemoryStream(data);
    }

    [Fact]
    public void AReadAcrossTwoBlocksTakesFromBoth()
    {
        var cache = Cache(Numbered(Block * 2 + 10));
        cache.Position = Block - 3;
        byte[] buffer = new byte[6];

        Assert.Equal(6, cache.Read(buffer, 0, 6));
        Assert.Equal([0, 0, 0, 1, 1, 1], buffer);
        Assert.Equal(Block + 3, cache.Position);
        // Past the end: what is left, then nothing.
        cache.Position = Block * 2 + 8;
        Assert.Equal(2, cache.Read(buffer, 0, 6));
        Assert.Equal(0, cache.Read(buffer, 0, 6));
    }

    [Fact]
    public void ABlockReadAgainComesFromTheCache()
    {
        var inner = Numbered(Block * 3);
        var cache = Cache(inner);
        byte[] one = new byte[1];
        // Blocks 0, 1 and 2 read in turn, then 0 again (brought back to the front) and 2 (the front already).
        foreach (int block in new[] { 0, 1, 2, 0, 2, 1 })
        {
            cache.Position = (long)block * Block + 5;
            Assert.Equal(1, cache.Read(one, 0, 1));
            Assert.Equal((byte)block, one[0]);
        }
        // Each block was read from the stream once: the stream stands where the last new block ended.
        Assert.Equal(Block * 3, inner.Position);
    }

    [Fact]
    public void TheBlockUsedLongestAgoGoesOnceTheCacheIsFull()
    {
        var inner = Numbered((long)Block * (Most + 1));
        var cache = Cache(inner);
        byte[] one = new byte[1];
        for (int block = 0; block <= Most; block++)
        {
            cache.Position = (long)block * Block;
            Assert.Equal(1, cache.Read(one, 0, 1));
            Assert.Equal((byte)block, one[0]);
        }
        Assert.Equal((long)Block * (Most + 1), inner.Position);
        // Block 0 went when block 512 came in: read from the stream again (and block 1 goes in its turn). Block 2
        // stayed: not read again, the stream stands where block 0 ended.
        cache.Position = 0;
        Assert.Equal(1, cache.Read(one, 0, 1));
        Assert.Equal(0, one[0]);
        Assert.Equal(Block, inner.Position);
        cache.Position = 2L * Block;
        Assert.Equal(1, cache.Read(one, 0, 1));
        Assert.Equal(2, one[0]);
        Assert.Equal(Block, inner.Position);
    }

    [Fact]
    public void TheStreamIsReadOnlyAndSeekable()
    {
        var cache = Cache(new MemoryStream(new byte[10]));

        Assert.True(cache.CanRead);
        Assert.True(cache.CanSeek);
        Assert.False(cache.CanWrite);
        Assert.Equal(10, cache.Length);
        cache.Flush();
        Assert.Throws<NotSupportedException>(() => cache.SetLength(5));
        Assert.Throws<NotSupportedException>(() => cache.Write(new byte[1], 0, 1));
    }
}
