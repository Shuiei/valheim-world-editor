using TerrainEditor.App;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.WorldSessionRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Opening a live world (WorldSession.OpenLive) on a game whose bridge reports its mods: the mods' prefabs are learnt
/// from its catalogue and their models asked for, and the plugins registered with the bridge are listed. The mods are
/// forgotten again afterwards, in a data folder of the test's own.
/// </summary>
[Collection("DataDir")]
public sealed class WorldSessionOpenLiveModsTests : IDisposable
{
    private readonly string? _dataDir = AppSettings.DataDirOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-livemods-" + Guid.NewGuid().ToString("N")[..8]);

    public WorldSessionOpenLiveModsTests()
    {
        AppSettings.DataDirOverride = _dir;
        ModCatalogue.Forget();
        GameData.UseCopies(Fixtures.Root);
    }

    /// <summary>Forgets the mods, puts the data folder and the tests' copies back, and deletes the folder.</summary>
    public void Dispose()
    {
        ModCatalogue.Forget();
        GameData.UseCopies(Fixtures.Root);
        AppSettings.DataDirOverride = _dataDir;
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    /// <summary>A report as the plugin writes it: one modded wall, with its piece, cost and mod.</summary>
    private const string Report = """
        {
          "version": 1,
          "prefabs": {"bfp_finewall": {"p":1,"d":0,"t":2}},
          "pieces": {"bfp_finewall": {"cat":2,"comfort":0}},
          "cost": {"pieces": {"bfp_finewall": {"n":"Fine wood wall","r":[]}}, "items": {}},
          "mods": {"bfp_finewall": "FineWoodPieces"}
        }
        """;

    [Fact]
    public async Task TheModsPrefabsAreLearntFromTheBridgesCatalogue()
    {
        using var game = new FakeGame { Catalogue = Report, Mods = ["FineWoodPieces"] };
        using var world = await OpenLive(game);

        Assert.True(world.IsLive);
        Assert.Contains("bfp_finewall", ModCatalogue.Names);
    }
}
