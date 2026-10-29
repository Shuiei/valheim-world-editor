using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What place_pieces looks at before it puts pieces down (ClaudeTools): the placed objects a new piece could snap to,
/// which are those within its snap reach and no others.
/// </summary>
[Collection("World files")]
public class ClaudeToolsPlacingTests
{
    // A floor put down at 0, 0 is one a wall at 1 m could snap to; a wall 500 m away could not snap to it.
    [AvaloniaFact]
    public async Task AFloorNextToAWallIsOneItCouldSnapTo()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        await r.T.PlacePieces([new ClaudeTools.PieceSpec { Prefab = "wood_floor", X = 0, Z = 0 }]);
        var scene = r.W.Session!.Scene;
        var near = r.T.PlacedNear(scene, [new ClaudeTools.PieceSpec { Prefab = "woodwall", X = 1, Z = 0 }]);
        Assert.Contains(near, p => p.Prefab == "wood_floor");
        var far = r.T.PlacedNear(scene, [new ClaudeTools.PieceSpec { Prefab = "woodwall", X = 500, Z = 0 }]);
        Assert.DoesNotContain(far, p => p.Prefab == "wood_floor");
    }
}
