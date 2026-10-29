using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The chunks of a PNG picture read in order: the header, the text entries and the joined picture data, stopping at
/// the end chunk, and a chunk that runs past the picture is refused.
/// </summary>
public class PngChunkScanTests
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>
    /// A chunk with its length, type and data (the checksum is not read, so it is left zero).
    /// </summary>
    private static byte[] Chunk(string type, params byte[] data)
    {
        var chunk = new List<byte> { (byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length };
        chunk.AddRange(System.Text.Encoding.ASCII.GetBytes(type));
        chunk.AddRange(data);
        chunk.AddRange([0, 0, 0, 0]);
        return [.. chunk];
    }

    /// <summary>
    /// The picture's signature followed by these chunks.
    /// </summary>
    private static byte[] Picture(params byte[][] chunks) => [.. Signature, .. chunks.SelectMany(c => c)];

    [Fact]
    public void TheHeaderChunkGivesTheHeaderFields()
    {
        byte[] header = [0, 0, 0, 3, 0, 0, 0, 2, 8, 6, 0, 0, 0];

        var (found, _, _) = Png.ScanChunks(Picture(Chunk("IHDR", header)));

        Assert.Equal(new Png.PngHeader(3, 2, 8, 6), found);
    }

    [Fact]
    public void APictureWithoutAHeaderChunkHasNoHeader()
    {
        var (found, _, _) = Png.ScanChunks(Picture(Chunk("IEND")));

        Assert.Null(found);
    }

    [Fact]
    public void TheTextChunksBecomeTheTextEntries()
    {
        var (_, text, _) = Png.ScanChunks(Picture(Chunk("tEXt", "Title\0Hello"u8.ToArray())));

        Assert.Equal("Hello", text["Title"]);
    }

    [Fact]
    public void TheDataChunksAreJoinedInTheirOrder()
    {
        var (_, _, idat) = Png.ScanChunks(Picture(Chunk("IDAT", 1, 2), Chunk("IDAT", 3), Chunk("IEND")));

        Assert.Equal([1, 2, 3], idat);
    }

    [Fact]
    public void ChunksAfterTheEndChunkAreNotRead()
    {
        var (_, _, idat) = Png.ScanChunks(Picture(Chunk("IEND"), Chunk("IDAT", [9])));

        Assert.Empty(idat);
    }

    [Fact]
    public void AChunkLongerThanThePictureIsRefused()
    {
        byte[] cut = Picture(Chunk("IDAT", 1, 2, 3));
        byte[] shortened = cut[..^6];

        Assert.Throws<InvalidDataException>(() => Png.ScanChunks(shortened));
    }
}
