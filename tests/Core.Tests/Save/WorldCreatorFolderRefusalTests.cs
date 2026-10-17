using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Refusing to write a new world over a folder that already holds one: an empty or missing folder is fine, a folder with
/// a world's main file or a zone file is refused, and other files do not count.
/// </summary>
public sealed class WorldCreatorFolderRefusalTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-refusal-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>
    /// Makes this test's temporary folder.
    /// </summary>
    public WorldCreatorFolderRefusalTests() => Directory.CreateDirectory(_dir);

    /// <summary>
    /// Deletes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void AMissingFolderIsAllowed()
    {
        string folder = Path.Combine(_dir, "new-world");

        PrivateCall.Static(typeof(WorldCreator), "RefuseExistingWorld", folder);
    }

    [Fact]
    public void AnEmptyFolderIsAllowed()
    {
        PrivateCall.Static(typeof(WorldCreator), "RefuseExistingWorld", _dir);
    }

    [Fact]
    public void AFolderWithOnlyOtherFilesIsAllowed()
    {
        File.WriteAllText(Path.Combine(_dir, "notes.txt"), "not a world");

        PrivateCall.Static(typeof(WorldCreator), "RefuseExistingWorld", _dir);
    }

    [Fact]
    public void AFolderWithAWorldMainFileIsRefused()
    {
        File.WriteAllText(Path.Combine(_dir, "_main.fwl"), "world");

        var e = Assert.Throws<InvalidOperationException>(() => PrivateCall.Static(typeof(WorldCreator), "RefuseExistingWorld", _dir));
        Assert.Contains("already holds a world", e.Message);
    }

    [Fact]
    public void AFolderWithAZoneFileIsRefused()
    {
        File.WriteAllText(Path.Combine(_dir, "0_0.chunk"), "zone");

        Assert.Throws<InvalidOperationException>(() => PrivateCall.Static(typeof(WorldCreator), "RefuseExistingWorld", _dir));
    }
}
