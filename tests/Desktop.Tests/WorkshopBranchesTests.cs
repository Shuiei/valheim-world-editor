using TerrainEditor.Terrain;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Blueprints opened on the Workshop's plot in their rarer shapes: one of things the hoe places only (no building
/// piece: its lowest thing goes on the ground), and pieces without a scale (taken as 1), centred as files from other
/// mods are.
/// </summary>
public sealed class WorkshopBranchesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-ws-branches-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Makes the folder the files are written to.</summary>
    public WorkshopBranchesTests() => Directory.CreateDirectory(_dir);

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ABlueprintOfNoBuildingPieceStandsOnItsLowestThing()
    {
        string f = Path.Combine(_dir, "stones.blueprint");
        File.WriteAllText(f, "#Name:Stones\n#Pieces\n"
            + "Placeable_Stone;Misc;10;3;10;0;0;0;1;\"\";1;1;1\n"
            + "Placeable_Stone;Misc;14;5;10;0;0;0;1;\"\";1;1;1\n");
        var scene = Workshop.Create("W");
        var (placed, unknown, _, terrain) = Workshop.Open(scene.Session!, f);
        Assert.Equal(2, placed);
        Assert.Empty(unknown);
        // Centred across (not Homestead's), and nothing built: no ground contacts.
        var stones = scene.Things.Where(t => !t.Gone).OrderBy(t => t.Position.X).ToList();
        Assert.Equal(-2, stones[0].Position.X - Workshop.Anchor(scene).X, 3);
        Assert.Equal(2, stones[1].Position.X - Workshop.Anchor(scene).X, 3);
        Assert.True(stones[0].Position.Y < stones[1].Position.Y);
        Assert.Empty(terrain);
    }

    [Fact]
    public void PiecesWithoutAScaleAreTakenAsTheirOwnSize()
    {
        string f = Path.Combine(_dir, "walls.blueprint");
        File.WriteAllText(f, "#Name:Walls\n#Pieces\n"
            + "woodwall;Misc;0;1;0;0;0;0;1;\"\";0;0;0\n"
            + "woodwall;Misc;4;1;0;0;0;0;1;\"\";0;0;0\n");
        var scene = Workshop.Create("W");
        var (placed, _, lift, terrain) = Workshop.Open(scene.Session!, f);
        Assert.Equal(2, placed);
        // The walls' bottoms (1 m below their middles, at their own size) on the ground.
        Assert.All(scene.Things.Where(t => !t.Gone), t => Assert.Equal(Workshop.Ground + 1, t.Position.Y, 3));
        Assert.Equal(0, lift, 3);
        Assert.True(Workshop.Buildable(PrefabCatalog.NameOf(scene.Things[0].Prefab)!));
        Assert.NotEmpty(terrain);
    }
}
