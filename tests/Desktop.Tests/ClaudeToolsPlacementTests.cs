using Avalonia.Headless.XUnit;
using Xunit;
using static TerrainEditor.Desktop.Tests.ClaudeRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Claude tools' placements: the blueprint tools look for the homestead again, a lone coordinate is refused, and pieces stand on the ground between grid points.
/// </summary>
[Collection("World files")]
public class ClaudeToolsPlacementTests
{
    // Homestead moved (or installed) after the Blueprints panel last looked: the blueprint tools look again.
    [AvaloniaFact]
    public async Task TheBlueprintToolsLookForHomesteadAgain()
    {
        using var r = new ClaudeRun();
        r.Hut("Hut", "A floor");
        Assert.Equal("Hut", J(await r.T.ListBlueprints())[0].GetProperty("name").GetString());
        string elsewhere = Path.Combine(r.Homestead, "moved");
        Directory.CreateDirectory(elsewhere);
        r.W.Blueprints.FindHomestead = () => new TerrainEditor.App.Homestead.Status(true, "1.3.2", ["test"], elsewhere);
        Assert.Equal(0, J(await r.T.ListBlueprints()).GetArrayLength());
        Assert.Equal("No blueprint \"Hut\" in the library (list_blueprints).", await r.T.OpenWorkshop("Hut"));
    }

    // A point's coordinates come both or neither: a lone one was dropped (list_objects' near) or mixed with a default
    // (move_objects' pivot with the objects' middle z).
    [Fact]
    public void ALoneCoordinateIsRefused()
    {
        Assert.Null(ClaudeTools.LoneCoordinate(null, null));
        Assert.Null(ClaudeTools.LoneCoordinate(1, 2));
        Assert.Equal("Give x and z together, or neither: only x was given.", ClaudeTools.LoneCoordinate(1, null));
        Assert.Equal("Give pivotX and pivotZ together, or neither: only pivotZ was given.", ClaudeTools.LoneCoordinate(null, 2, "pivotX", "pivotZ"));
    }

    // On a slope a piece stands on the ground between the grid points, as ground_at gives it: it took the nearest grid
    // point's height, up to half a metre's slope off.
    [AvaloniaFact]
    public async Task PiecesStandOnTheGroundBetweenTheGridPoints()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var s = r.W.Session!;
        s.Shape((s.Scene.W - 1) / 2f, (s.Scene.H - 1) / 2f, Formula.Compile("x * 0.3 + z * 0.2", ["x", "z"]), 20, 0, "slope");
        float cx = s.Scene.Cx + 3, cz = s.Scene.Cz;
        var placed = J(await r.T.PlacePieces(
        [
            new ClaudeTools.PieceSpec { Prefab = "Birch1", X = cx, Z = cz, Snap = false },
            new ClaudeTools.PieceSpec { Prefab = "Birch1", X = cx + 0.4f, Z = cz + 0.3f, Snap = false },
        ])).GetProperty("placed");
        var ground = J(await r.T.GroundAtPoints([[cx, cz], [cx + 0.4f, cz + 0.3f]]));
        float rise = ground[1].GetProperty("height").GetSingle() - ground[0].GetProperty("height").GetSingle();
        // On a slope either way (the area's own ground under the shaped one): both points share a nearest grid point, so
        // they stood at one height before.
        Assert.True(MathF.Abs(rise) > 0.1f, $"{rise}");
        Assert.Equal(rise, placed[1].GetProperty("piece").GetProperty("y").GetSingle() - placed[0].GetProperty("piece").GetProperty("y").GetSingle(), 2);
    }
}
