using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game's bundles as the catalogue scan opens them (GameCatalogues.Game): a file in the bundles folder that is not a
/// bundle is passed over when the internal files are listed, and opens as nothing.
/// </summary>
public class GameCataloguesGameTests
{
    [Fact]
    public void AFileThatIsNotABundleIsPassedOverAndOpensAsNothing()
    {
        using var game = new TestGame();
        var b = game.Bundle("real");
        new TestMesh { Name = "plank", Positions = [0, 0, 0, 1, 0, 0, 0, 1, 0], Submeshes = [[0, 1, 2]] }.Add(b);
        game.Save();
        File.WriteAllText(Path.Combine(game.BundlesDir, "readme.txt"), "not a bundle");
        using var g = new GameCatalogues.Game(game.BundlesDir);

        g.IndexCabs();

        Assert.Equal(["readme.txt", "real"], g.Bundles.Order(StringComparer.Ordinal).ToList());
        Assert.Equal(["real"], g.CabBundle.Values.Distinct().ToList());
        Assert.Null(g.Open("readme.txt"));
        using var real = g.Open("real");
        Assert.NotNull(real);
    }
}
