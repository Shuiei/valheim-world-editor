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

    // A container's crafter name with a negative length (BinaryReader threw a plain IOException).
    [Fact]
    public void InventoryNegativeStringLength() =>
        Refused(() => InventoryData.Read(Convert.FromHexString("266d000000d9ffffd9ff01006f27040affffffff0a27040affffffff0a")));

    // A .db2 file whose zone package has a negative length.
    [Fact]
    public void ZoneDbNegativePackageLength() =>
        Refused(() => ZoneDb.Load(new MemoryStream(Convert.FromHexString("00000000ff00ff0000000000ff0000ff"))));

    // A PNG whose header chunk is empty.
    [Fact]
    public void PngShortHeader() =>
        Refused(() => Png.Read(Convert.FromHexString("89504e470d0a1a0a000000004948445200006082")));

    // Terrain data that unpacks to far more than any zone's (17 MiB of zeros in 17 KB).
    [Fact]
    public void TerrainDataTooLarge() =>
        Refused(() => TerrainZone.Decode(default, 0, Gzip(new byte[17 << 20])));

    // Picture data whose compression is damaged (the decompressor threw ZLibException).
    [Fact]
    public void PngDamagedData() =>
        Refused(() => Png.Read(Convert.FromHexString("89504e470d0a1a0a0000000d494844520000000d000000030804000000d94a22e80000002649444154783f63604944415460e1e5e317106411121611151397ffffff746d6170067b15070000000000d958744982")));

    // Gzip data whose compressed part is damaged.
    [Fact]
    public void GzipDamagedData()
    {
        byte[] damaged = Gzip([.. Enumerable.Range(0, 4096).Select(i => (byte)(i * 7))]);
        for (int i = 12; i < damaged.Length - 8; i += 5)
        {
            damaged[i] ^= 0xA5;
        }
        Refused(() => CheckedReads.Gunzip(damaged, 1 << 20));
    }
}
