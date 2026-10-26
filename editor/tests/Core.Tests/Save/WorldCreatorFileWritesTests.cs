using System.Text;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The two files WorldCreator writes by hand: a chunk file (version, object count, then the objects back to back) and the
/// world's metadata (a length, then the package the game reads: version, name, seed name, seed and unique id).
/// </summary>
public class WorldCreatorFileWritesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-creator-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Makes the test's folder.</summary>
    public WorldCreatorFileWritesTests() => Directory.CreateDirectory(_dir);

    /// <summary>Deletes the test's folder.</summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Directory.Delete(_dir, true);
    }

    [Fact]
    public void AChunkFileHoldsItsVersionCountAndObjectsBackToBack()
    {
        string path = Path.Combine(_dir, "chunk.bin");

        PrivateMember.Call(typeof(WorldCreator), "WriteChunk", null, path, new List<byte[]> { new byte[] { 1, 2 }, new byte[] { 3 } });

        byte[] expected = [.. BitConverter.GetBytes((short)WorldCreator.SaveFileVersion), .. BitConverter.GetBytes(2), 1, 2, 3];
        Assert.Equal(expected, File.ReadAllBytes(path));
    }

    [Fact]
    public void AChunkFileWithNoObjectsHasOnlyItsVersionAndZeroCount()
    {
        string path = Path.Combine(_dir, "empty.bin");

        PrivateMember.Call(typeof(WorldCreator), "WriteChunk", null, path, new List<byte[]>());

        byte[] expected = [.. BitConverter.GetBytes((short)WorldCreator.SaveFileVersion), .. BitConverter.GetBytes(0)];
        Assert.Equal(expected, File.ReadAllBytes(path));
    }

    [Fact]
    public void TheMetadataPackageHoldsTheNamesSeedAndUniqueId()
    {
        string path = Path.Combine(_dir, "world.fwl");

        PrivateMember.Call(typeof(WorldCreator), "WriteMetadata", null, path, "My world", "Seedy", 12345, 987654321012L, null);

        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(bytes.Length - 4, BitConverter.ToInt32(bytes, 0));
        using var reader = new BinaryReader(new MemoryStream(bytes, 4, bytes.Length - 4), Encoding.UTF8);
        Assert.Equal(WorldCreator.SaveFileVersion, reader.ReadInt32());
        Assert.Equal("My world", reader.ReadString());
        Assert.Equal("Seedy", reader.ReadString());
        Assert.Equal(12345, reader.ReadInt32());
        Assert.Equal(987654321012L, reader.ReadInt64());
    }
}
