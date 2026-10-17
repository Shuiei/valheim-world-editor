using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Where a Valheim install keeps its bundles: the StreamingAssets/SoftRef/Bundles folder inside its data folder, which
/// is found under either spelling of the data folder's name; an install without one has no bundles folder.
/// </summary>
public sealed class GameLookBundlesDirTests : IDisposable
{
    private readonly string _valheim = Path.Combine(Path.GetTempPath(), "vwe-valheim-" + Guid.NewGuid().ToString("N")[..8]);

    public GameLookBundlesDirTests()
    {
        Directory.CreateDirectory(_valheim);
    }

    public void Dispose()
    {
        Directory.Delete(_valheim, recursive: true);
    }

    [Fact]
    public void TheBundlesFolderIsInsideTheDataFolder()
    {
        string bundles = Path.Combine(_valheim, "valheim_Data", "StreamingAssets", "SoftRef", "Bundles");
        Directory.CreateDirectory(bundles);
        Assert.Equal(bundles, GameLook.BundlesDir(_valheim));
    }

    [Fact]
    public void TheDataFolderMayBeSpelledWithACapital()
    {
        string bundles = Path.Combine(_valheim, "Valheim_Data", "StreamingAssets", "SoftRef", "Bundles");
        Directory.CreateDirectory(bundles);
        Assert.Equal(bundles, GameLook.BundlesDir(_valheim));
    }

    [Fact]
    public void AnInstallWithoutABundlesFolderHasNone()
    {
        Assert.Null(GameLook.BundlesDir(_valheim));
    }
}
