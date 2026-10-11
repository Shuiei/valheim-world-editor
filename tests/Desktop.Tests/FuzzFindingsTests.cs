using System.IO.Compression;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

// Inputs the fuzz targets (tests/Fuzz) found that broke a reader, minimized: each must now be refused
// as damaged (InvalidDataException, EndOfStreamException...), not fail with an unrelated error.
public class FuzzFindingsTests
{
    private static void Refused(Action read)
    {
        Exception? e = Record.Exception(read);
        Assert.NotNull(e);
        Assert.True(e is InvalidDataException or EndOfStreamException or FormatException, $"{e.GetType().Name}: {e.Message}");
    }

    private static byte[] Gzip(byte[] data)
    {
        using MemoryStream packed = new();
        using (GZipStream gz = new(packed, CompressionLevel.Fastest, leaveOpen: true))
        {
            gz.Write(data);
        }
        return packed.ToArray();
    }

    // A byte array of an object with a negative length.
    [Fact]
    public void ZdoNegativeByteArrayLength() =>
        Refused(() => ZdoData.Parse(Convert.FromHexString("e0e03fe0f60100000000000004e0000000000000e0")));

    // The same in a chunk file.
    [Fact]
    public void ChunkNegativeByteArrayLength() =>
        Refused(() => WorldSave.ReadChunk(new MemoryStream(Convert.FromHexString("290001000000803f0183348b8b8b8b8b8b8b8b8b8b8b008b8b8b8b8b"))));

    // A live snapshot whose zone package has a negative length.
    [Fact]
    public void SnapshotNegativeZonePackageLength() =>
        Refused(() => WorldSave.LoadLive(Gzip(Convert.FromHexString("56574542010000000000000073740a47e5c22722c3e2f2d8f9ffffffff88")), "fuzz"));
}
