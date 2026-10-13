using TerrainEditor.App;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Build panel's pieces with a modded game's: learned from a live game, a mod's pieces are listed at once, on the
/// Mods tab whatever their tool (a mod's own hammer too) and category, and the Workshop counts them as built; a
/// mod's hoe pieces are ground edits, not listed. Runs alone (the catalogues are shared), in a data folder of its own;
/// the tests' copies are in use again afterwards.
/// </summary>
[Collection("DataDir")]
public sealed class BuildPanelModsTests : IDisposable
{
    private readonly string? _dataDir = AppSettings.DataDirOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-buildmods-" + Guid.NewGuid().ToString("N")[..8]);

    public BuildPanelModsTests()
    {
        AppSettings.DataDirOverride = _dir;
        ModCatalogue.Forget();
        GameData.UseCopies(Fixtures.Root);
    }

    /// <summary>
    /// Forgets the mods, puts the data folder and the tests' copies back, and deletes the folder.
    /// </summary>
    public void Dispose()
    {
        ModCatalogue.Forget();
        GameData.UseCopies(Fixtures.Root);
        AppSettings.DataDirOverride = _dataDir;
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    private const string Report = """
        {
          "prefabs": {
            "bfp_finewall": {"p":1,"d":0,"t":2},
            "odin_path": {"p":1,"d":0,"t":2}
          },
          "pieces": {
            "bfp_finewall": {"cat":7,"comfort":0,"tool":"BFP_FineHammer"},
            "odin_path": {"cat":0,"comfort":0,"tool":"hoe"}
          },
          "cost": {"pieces": {"bfp_finewall": {"n":"Fine wood wall","r":[["FineWood",4,1]]}}, "items": {}}
        }
        """;

    [Fact]
    public void AModsPiecesAreListedOnTheModsTabOnceLearned()
    {
        Assert.DoesNotContain(BuildPanel.Pieces, p => p.Category == BuildPanel.Mods);

        GameData.AddMods(Report);

        var pieces = BuildPanel.Pieces;
        Assert.Contains(pieces, p => p.Prefab == "bfp_finewall" && p.Name == "Fine wood wall" && p.Category == BuildPanel.Mods);
        Assert.DoesNotContain(pieces, p => p.Prefab == "odin_path");
        Assert.Contains(BuildPanel.Tabs, t => t.Category == BuildPanel.Mods);
        Assert.True(Workshop.Buildable("bfp_finewall"));
        Assert.False(Workshop.Buildable("odin_path"));
    }
}
