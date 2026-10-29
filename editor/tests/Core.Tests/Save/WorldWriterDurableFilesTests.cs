using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The files a save writes and flushes to disk (WorldWriter.Files): bytes are written whole and replace a file there, an
/// index holds its format, object total and per-chunk fields, a copy replaces the file it copies over, and a removal
/// skips a file that is already gone.
/// </summary>
public class WorldWriterDurableFilesTests
{
    [Fact]
    public void WrittenBytesAreWholeOnDisk()
    {
        using var w = new TempWorld();
        string path = Path.Combine(w.Dir, "new.bin");

        PrivateCall.Static(typeof(WorldWriter), "WriteAllBytesDurable", path, new byte[] { 1, 2, 3 });

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
    }

    [Fact]
    public void WrittenBytesReplaceAFileThere()
    {
        using var w = new TempWorld();
        string path = Path.Combine(w.Dir, "new.bin");
        File.WriteAllBytes(path, [9, 9, 9, 9, 9]);

        PrivateCall.Static(typeof(WorldWriter), "WriteAllBytesDurable", path, new byte[] { 1, 2 });

        Assert.Equal(new byte[] { 1, 2 }, File.ReadAllBytes(path));
    }

    [Fact]
    public void AnIndexHoldsItsFormatTotalAndEachChunksFields()
    {
        using var w = new TempWorld();
        string path = Path.Combine(w.Dir, "index.chunks");

        WorldWriter.WriteIndex(path, 41, [(1, 2, 3u, 4), (5, 6, 7u, 8)]);

        using var r = new BinaryReader(File.OpenRead(path));
        Assert.Equal(41, r.ReadInt16());
        Assert.Equal(12, r.ReadInt32());
        Assert.Equal(2, r.ReadInt32());
        Assert.Equal(1, r.ReadUInt16());
        Assert.Equal(2, r.ReadByte());
        Assert.Equal(3u, r.ReadUInt32());
        Assert.Equal(4, r.ReadInt32());
        Assert.Equal(5, r.ReadUInt16());
        Assert.Equal(6, r.ReadByte());
        Assert.Equal(7u, r.ReadUInt32());
        Assert.Equal(8, r.ReadInt32());
    }

    [Fact]
    public void ACopyReplacesTheFileItCopiesOver()
    {
        using var w = new TempWorld();
        string source = Path.Combine(w.Dir, "source.bin"), target = Path.Combine(w.Dir, "target.bin");
        File.WriteAllBytes(source, [4, 5, 6]);
        File.WriteAllBytes(target, [0, 0, 0, 0, 0, 0, 0]);

        PrivateCall.Static(typeof(WorldWriter), "CopyDurable", source, target);

        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(target));
    }

    [Fact]
    public void RemovingFilesDeletesThemAndSkipsOneAlreadyGone()
    {
        using var w = new TempWorld();
        string present = Path.Combine(w.Dir, "present.ok"), gone = Path.Combine(w.Dir, "gone.ok");
        File.WriteAllBytes(present, [1]);

        PrivateCall.Static(typeof(WorldWriter), "RemoveAll", (object)new[] { present, gone });

        Assert.False(File.Exists(present));
    }
}
