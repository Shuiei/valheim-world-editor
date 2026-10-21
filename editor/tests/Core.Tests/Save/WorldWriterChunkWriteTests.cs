using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A chunk file written anew (WorldWriter.WriteChunk): it holds the chunk's objects less the removed ones, plus the
/// additions, and the count it reports and writes is that total.
/// </summary>
public class WorldWriterChunkWriteTests
{
    /// <summary>A chunk of the test world with more than one object.</summary>
    private static ChunkFile MultiObjectChunk(TempWorld w) => WorldSave.Load(w.Dir).Chunks.First(c => c.Count > 1);

    /// <summary>Writes a chunk of the test world anew, and reads back the object count the file holds.</summary>
    private static (int Reported, int Written) Write(TempWorld w, ChunkFile file, List<byte[]>? additions, HashSet<long>? removals)
    {
        string source = Path.Combine(w.Dir, file.FileName), target = Path.Combine(w.Dir, "written.chunk");
        int reported = (int)PrivateCall.Static(typeof(WorldWriter), "WriteChunk", source, target, file, null, additions, removals)!;
        using var r = new BinaryReader(File.OpenRead(target));
        r.ReadInt16();
        return (reported, r.ReadInt32());
    }

    [Fact]
    public void AddedObjectsAreCountedWithTheChunksOwn()
    {
        using var w = new TempWorld();
        ChunkFile file = MultiObjectChunk(w);

        var (reported, written) = Write(w, file, [[1], [2]], null);

        Assert.Equal(file.Count + 2, reported);
        Assert.Equal(reported, written);
    }

    [Fact]
    public void ARemovedObjectIsLeftOutOfTheCount()
    {
        using var w = new TempWorld();
        ChunkFile file = MultiObjectChunk(w);

        var (reported, written) = Write(w, file, null, [file.Objects[0].Start]);

        Assert.Equal(file.Count - 1, reported);
        Assert.Equal(reported, written);
    }
}
