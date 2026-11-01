using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A manifest's build pieces (a prefab's "piece"): its name, cost and station read with the mod it is of; a piece not
/// in that form is left out, the rest of the prefab's notes kept.
/// </summary>
[Collection("DataDir")]
public sealed class ModManifestsPieceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-manifest-pieces-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Forgets the manifests and deletes the folder.</summary>
    public void Dispose()
    {
        ModManifests.Forget();
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    [Fact]
    public void APiecesNameCostAndStationAreRead()
    {
        string mod = Path.Combine(_dir, "BepInEx", "plugins", "Author-Pieces");
        Directory.CreateDirectory(mod);
        File.WriteAllText(Path.Combine(mod, ModManifests.FileName), """
            { "version": 1, "prefabs": {
                "pm_wall": { "group": "Walls", "piece": { "name": "Wall", "cost": { "Wood": 2, "Resin": 1 }, "station": "Workbench" } },
                "pm_free": { "piece": { "name": "Free" } },
                "pm_noname": { "group": "Kept", "piece": { "cost": { "Wood": 2 } } },
                "pm_zero": { "piece": { "name": "Zero", "cost": { "Wood": 0 } } },
                "pm_list": { "piece": { "name": "List", "cost": [ "Wood" ] } },
                "pm_long": { "piece": { "name": "This name is far too long to be a piece's name in the Workshop dock at all" } } } }
            """);

        ModManifests.Refresh(new AppSettings { BepInExFolders = [Path.Combine(_dir, "BepInEx")] });

        var pieces = ModManifests.Pieces.ToDictionary(p => p.Prefab, p => p.Note);
        Assert.Equal(["pm_free", "pm_wall"], pieces.Keys.Order(StringComparer.Ordinal));
        var wall = pieces["pm_wall"];
        Assert.Equal(new ModManifests.PieceNote("Wall", wall.Piece!.Cost, "Workbench"), wall.Piece);
        Assert.Equal([("Wood", 2), ("Resin", 1)], wall.Piece.Cost);
        Assert.Equal("Author-Pieces", wall.Mod);
        Assert.Equal("Walls", wall.Group);
        Assert.Empty(pieces["pm_free"].Piece!.Cost);
        Assert.Null(pieces["pm_free"].Piece!.Station);
        // Not a piece, but the prefab's other notes stay.
        Assert.Equal("Kept", ModManifests.PrefabOf("pm_noname")?.Group);
        Assert.Null(ModManifests.PrefabOf("pm_noname")?.Piece);
    }
}
