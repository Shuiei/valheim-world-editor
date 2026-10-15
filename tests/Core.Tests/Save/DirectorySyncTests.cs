using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Flushing a directory's names to disk: a folder flushes, and one that is not there is an error (off Windows, where a
/// folder is flushed at all).
/// </summary>
public class DirectorySyncTests
{
    [Fact]
    public void AFolderFlushes()
    {
        string dir = Directory.CreateTempSubdirectory("vwe-sync-").FullName;
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "a"), [1]);
            DirectorySync.Flush(dir);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void AMissingFolderIsAnError()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-sync-missing-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows())
        {
            DirectorySync.Flush(dir);
        }
        else
        {
            Assert.Throws<IOException>(() => DirectorySync.Flush(dir));
        }
    }
}
