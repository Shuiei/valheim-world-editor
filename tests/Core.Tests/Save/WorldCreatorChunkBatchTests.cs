using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A new world's objects grouped by chunk file: an object is counted and grouped by its zone's base chunk, an object in
/// the portals' chunk is never added, and a placed object without counting is only grouped.
/// </summary>
public class WorldCreatorChunkBatchTests
{
    [Fact]
    public void AnObjectInAZoneIsGroupedByItsBaseChunkAndCounted()
    {
        var batch = new WorldCreator.ChunkBatch();

        batch.Add(0f, 0f, [1]);

        Assert.Equal(1, batch.Objects);
        var (chunk, _) = Assert.Single(batch.Chunks);
        Assert.Equal(ChunkMath.BaseChunk(0, 0), chunk);
    }

    [Fact]
    public void AnObjectInThePortalsChunkIsNeverAdded()
    {
        var batch = new WorldCreator.ChunkBatch();

        batch.Add(-16384f, -16384f, [1]);

        Assert.Equal(0, batch.Objects);
        Assert.Empty(batch.Chunks);
    }

    [Fact]
    public void APutObjectIsGroupedButNotCounted()
    {
        var batch = new WorldCreator.ChunkBatch();

        batch.Put(ChunkMath.BaseChunk(0, 0), [2]);

        Assert.Equal(0, batch.Objects);
        Assert.Single(batch.Chunks);
    }
}
