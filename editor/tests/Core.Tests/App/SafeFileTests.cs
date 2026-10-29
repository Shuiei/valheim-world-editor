using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// SafeFile: a file written through a temporary file of its own and moved over the old one; not over a file there
/// already when asked not to, with nothing left behind; writers of the same file at once each write it whole.
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
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
            SafeFile.WriteAllText(path, "third");
            Assert.Equal("third", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void WritersOfTheSameFileAtOnceEachWriteItWhole()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-safefile-" + Guid.NewGuid().ToString("N")[..8]);
        string path = Path.Combine(dir, "shared.txt");
        string[] texts = [.. Enumerable.Range(0, 32).Select(i => new string((char)('a' + (i % 26)), 4096 + i))];
        try
        {
            // With one fixed temporary name, a writer deleted or moved another's half-written file.
            Parallel.ForEach(texts, text => SafeFile.WriteAllText(path, text));

            Assert.Contains(File.ReadAllText(path), texts);
            Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
