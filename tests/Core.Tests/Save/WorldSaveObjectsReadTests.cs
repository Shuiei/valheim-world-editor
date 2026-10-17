using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Reading a chunk's object count: the chunk's world version is a short, the object count a checked int, and the chunk's
/// length is where reading stopped. An empty chunk has no objects and is read to its end.
/// </summary>
public sealed class WorldSaveObjectsReadTests
{
    /// <summary>A chunk with no objects reads its version and count, and its length is the bytes read.</summary>
    [Fact]
    public void AnEmptyChunkReadsItsVersionAndItsLength()
    {
        var world = new WorldSave { Directory = "", SaveNumber = 0 };
        var file = new ChunkFile();
        // Version 7 as a short, then an object count of 0 as an int.
        using var pkg = new ValheimReader(new MemoryStream([0x07, 0x00, 0x00, 0x00, 0x00, 0x00]));

        PrivateMember.Call(typeof(WorldSave), "ReadObjects", world, pkg, file);

        Assert.Equal(7, file.WorldVersion);
        Assert.Equal(0, file.Count);
        Assert.Equal(6, file.Length);
        Assert.Equal(0, world.ObjectCount);
    }

    /// <summary>A chunk file on disk is read the same way: its version, and the length read.</summary>
    [Fact]
    public void ALoadedChunkFileIsReadTheSameWay()
    {
        var world = new WorldSave { Directory = "", SaveNumber = 0 };
        var file = new ChunkFile();
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, [0x07, 0x00, 0x00, 0x00, 0x00, 0x00]);

            PrivateMember.Call(typeof(WorldSave), "LoadChunk", world, path, file);
        }
        finally
        {
            File.Delete(path);
        }

        Assert.Equal(7, file.WorldVersion);
        Assert.Equal(6, file.Length);
    }
}
