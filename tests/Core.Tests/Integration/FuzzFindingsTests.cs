using System.IO.Compression;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Inputs the fuzz targets (tests/Fuzz) found that broke a reader, minimized: each must now be refused as damaged
/// (InvalidDataException, EndOfStreamException...), not fail with an unrelated error.
/// </summary>
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

    // A chunk length near int.MaxValue: p + 12 + length overflowed and passed the bounds check.
    [Fact]
    public void PngHugeChunkLength() =>
        Refused(() => Png.Read(Convert.FromHexString("89504e470d0a1a0a0000000d4948445200000004000000041000000000dc0a1d217ffffff28bbaa774767765006865696768746d6170067b15070000002c49444154789c63646064626661656367e2e0e4e2494441546714101412161195930700179a8b673a3e0000f20000000000000082")));

    /// <summary>
    /// A 2 by 1 grayscale 8-bit PNG whose picture data is <paramref name="zlib"/> (the reader does not check chunk
    /// CRCs: they are left zero).
    /// </summary>
    private static byte[] GrayPng(byte[] zlib)
    {
        using MemoryStream png = new();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        void Chunk(string type, byte[] data)
        {
            png.Write([(byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length]);
            png.Write(System.Text.Encoding.ASCII.GetBytes(type));
            png.Write(data);
            png.Write([0, 0, 0, 0]);
        }
        Chunk("IHDR", [0, 0, 0, 2, 0, 0, 0, 1, 8, 0, 0, 0, 0]);
        Chunk("IDAT", zlib);
        Chunk("IEND", []);
        return png.ToArray();
    }

    private static byte[] Zlib(byte[] data)
    {
        using MemoryStream packed = new();
        using (ZLibStream z = new(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(data);
        }
        return packed.ToArray();
    }

    // Picture data that unpacks to more than its rows: refused, not cut to size.
    [Fact]
    public void PngMoreDataThanItsSize()
    {
        Assert.Equal(2, Png.Read(GrayPng(Zlib([0, 10, 20]))).Values.Length);
        var e = Assert.Throws<InvalidDataException>(() => Png.Read(GrayPng(Zlib([0, 10, 20, 1, 2, 3, 4]))));
        Assert.Contains("more data than its size", e.Message);
    }

    // Picture data whose zlib checksum is wrong: refused as damaged (zlib throws InvalidDataException there, not
    // the IOException a damaged block gives).
    [Fact]
    public void PngWrongChecksum()
    {
        byte[] zlib = Zlib([0, 10, 20]);
        zlib[^1] ^= 0xFF;
        var e = Assert.Throws<InvalidDataException>(() => Png.Read(GrayPng(zlib)));
        Assert.Contains("damaged", e.Message);
    }

    // A zone database from a stream that cannot seek (it cannot tell how much is left): read in full, as from
    // its file.
    [Fact]
    public void ZoneDbFromAStreamThatCannotSeek()
    {
        string file = Path.Combine(Fixtures.Root, "CITest", "_main.2.db2");
        using GZipStream unseekable = new(new MemoryStream(Gzip(File.ReadAllBytes(file))), CompressionMode.Decompress);
        Assert.False(unseekable.CanSeek);
        var fromStream = ZoneDb.Load(unseekable);
        var fromFile = ZoneDb.Load(file);
        string dir = Directory.CreateTempSubdirectory("vwe-zonedb-").FullName;
        try
        {
            fromStream.Save(Path.Combine(dir, "stream.db2"));
            fromFile.Save(Path.Combine(dir, "file.db2"));
            Assert.Equal(File.ReadAllBytes(Path.Combine(dir, "file.db2")), File.ReadAllBytes(Path.Combine(dir, "stream.db2")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
