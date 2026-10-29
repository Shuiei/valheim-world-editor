using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A mesh's bounds and vertex count (GameBundles.BoundsOf and VertexCountOf) when its file cannot be read as a bundle:
/// none and unknown, each said in the log, nothing thrown.
/// </summary>
public class GameBundlesBoundsTests
{
    [Fact]
    public void AMeshInAFileThatIsNotABundleHasNoBoundsAndNoVertexCount()
    {
        using var game = new TestGame();
        File.WriteAllBytes(Path.Combine(game.BundlesDir, "broken"), [.. "UnityFS"u8, 0]);
        var bundles = game.Bundles();
        var mesh = new GameBundles.Where("broken", 1);
        using var log = Log.Listen();

        Assert.Null(bundles.BoundsOf(mesh));
        Assert.Equal(-1, bundles.VertexCountOf(mesh));

        Assert.Contains(log.Lines, l => l.Contains("reading a mesh's bounds", StringComparison.Ordinal));
        Assert.Contains(log.Lines, l => l.Contains("counting a mesh's vertices", StringComparison.Ordinal));
    }
}
