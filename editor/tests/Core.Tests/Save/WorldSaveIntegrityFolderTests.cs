using TerrainEditor.Save;
using Xunit;

using static WorldEditor.Tests.SaveGzipHelpers;

namespace WorldEditor.Tests;

/// <summary>
/// Worlds that cannot be read are refused with a reason: a folder with no save, a missing chunk file, and a snapshot that is not
/// one or is a newer one.
/// </summary>
public class WorldSaveIntegrityFolderTests
{
    [Fact]
    public void AFolderWithoutASaveIsRefused()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-empty-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => WorldSave.Load(dir));
            Assert.Contains("No _main.<n>.chunks file", ex.Message);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void AMissingChunkFileIsReported()
    {
        using var w = new TempWorld();
        string chunk = Directory.GetFiles(w.Dir, "*.chunk").First();
        File.Delete(chunk);
        var ex = Assert.Throws<FileNotFoundException>(() => WorldSave.Load(w.Dir));
        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public void ASnapshotThatIsNotOneIsRefused()
    {
        var ex = Assert.Throws<InvalidDataException>(() => WorldSave.LoadLive(Gzip([1, 2, 3, 4, 5, 6, 7, 8]), "x"));
        Assert.Equal("not a WorldEditorBridge snapshot", ex.Message);
        // Not even gzip.
        Assert.ThrowsAny<Exception>(() => WorldSave.LoadLive([0, 1, 2], "x"));
    }

    [Fact]
    public void ANewerSnapshotAsksForANewerEditor()
    {
        byte[] raw = [.. BitConverter.GetBytes(0x42455756), .. BitConverter.GetBytes(2)];
        var ex = Assert.Throws<InvalidDataException>(() => WorldSave.LoadLive(Gzip(raw), "x"));
        Assert.Contains("update the editor", ex.Message);
    }
}
