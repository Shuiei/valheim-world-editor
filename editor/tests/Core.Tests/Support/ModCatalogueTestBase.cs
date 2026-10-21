using TerrainEditor.App;

namespace WorldEditor.Tests;

/// <summary>
/// The set-up and the reports the ModCatalogue tests share (ModCatalogue*Tests): a data folder of its own, the mods
/// forgotten before and after, the tests' copies of the game data put back after, and the reports and models the game
/// would send, made as text.
/// </summary>
public abstract class ModCatalogueTestBase : IDisposable
{
    private readonly string? _dataDir = AppSettings.DataDirOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-mods-" + Guid.NewGuid().ToString("N")[..8]);
    /// <summary>
    /// The game's own prefabs the reports name as vanilla, as far as these tests go.
    /// </summary>
    protected static readonly HashSet<string> Vanilla = ["wood_fence"];

    /// <summary>
    /// Sets the data folder to this test's own, and forgets any mods before the test.
    /// </summary>
    protected ModCatalogueTestBase()
    {
        AppSettings.DataDirOverride = _dir;
        ModCatalogue.Forget();
    }

    /// <summary>
    /// Forgets the mods, puts the data folder and the tests' copies back, and deletes the folder.
    /// </summary>
    public void Dispose()
    {
        ModCatalogue.Forget();
        GameData.UseCopies(Fixtures.Root);
        AppSettings.DataDirOverride = _dataDir;
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A report as the plugin writes it: a vanilla fence (as the game has it, changed by a mod), a modded wall with its
    /// piece, cost and mod, and a modded effect (not saved: no piece).
    /// </summary>
    protected static string Report(string wallCost = "[[\"BFP_Plank\",4,1]]") => $$"""
        {
          "version": 1,
          "prefabs": {
            "wood_fence": {"p":1,"d":0,"t":0},
            "bfp_finewall": {"p":1,"d":0,"t":2},
            "vfx_bfp_hit": {"p":0,"d":0,"t":0}
          },
          "pieces": {
            "wood_fence": {"cat":3,"comfort":0},
            "bfp_finewall": {"cat":2,"comfort":0,"box":[-1,1,-0.1,0.1,0,2],"snap":[[-1,0,0],[1,2,0]],"tool":"BFP_FineHammer"}
          },
          "cost": {
            "pieces": {
              "wood_fence": {"n":"Fence","r":[]},
              "bfp_finewall": {"n":"Fine wood wall","r":{{wallCost}},"st":"Workbench"}
            },
            "items": {"BFP_Plank": "Fine Plank", "Wood": "Wood"}
          },
          "mods": {"bfp_finewall": "FineWoodPieces", "wood_fence": "SomeMod"}
        }
        """;

    /// <summary>
    /// A model as the plugin reports it (POST /models): this root scale and these parts.
    /// </summary>
    protected static string ModelJson(string scale, params string[] parts) => $$"""{"s":[{{scale}}],"p":[{{string.Join(",", parts)}}]}""";

    /// <summary>
    /// A model's part: this mesh (name, vertices, submesh), material (null: none) and matrix (a move of (1, 2, 3)
    /// unless given).
    /// </summary>
    protected static string PartJson(string mesh, int vertices, int sub, string? material, string matrix = "1,0,0,1,0,1,0,2,0,0,1,3,0,0,0,1") =>
        $$"""{"mesh":"{{mesh}}","v":{{vertices}},"sub":{{sub}},{{(material == null ? "" : $"\"mat\":\"{material}\",")}}"m":[{{matrix}}]}""";
}
