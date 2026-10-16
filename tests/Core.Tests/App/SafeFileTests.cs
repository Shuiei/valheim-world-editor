using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// SafeFile: a file written through its .tmp and moved over the old one; not over a file there already when asked
/// not to, with nothing left behind.
/// </summary>
public class SafeFileTests
{
    [Fact]
    public void AFileThereAlreadyIsLeftWhenNotOverwriting()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-safefile-" + Guid.NewGuid().ToString("N")[..8]);
        string path = Path.Combine(dir, "kept.txt");
        try
        {
            SafeFile.WriteAllText(path, "first", overwrite: false);
            Assert.Equal("first", File.ReadAllText(path));
            Assert.Throws<IOException>(() => SafeFile.WriteAllText(path, "second", overwrite: false));
            Assert.Equal("first", File.ReadAllText(path));
            Assert.False(File.Exists(path + ".tmp"));
            SafeFile.WriteAllText(path, "third");
            Assert.Equal("third", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
