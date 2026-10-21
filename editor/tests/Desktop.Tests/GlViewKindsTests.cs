using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The 3D view's kinds: a ruin's piece joined to a building is shown with Your buildings, the same piece in a ruin
/// apart with Ruins &amp; structures.
/// </summary>
public class GlViewKindsTests
{
    [AvaloniaFact]
    public void ARuinsPieceJoinedToABuildingIsShownWithIt()
    {
        int stone = StableHash.Of("stone_wall_2x1");
        var s = TestScenes.Flat(2,
            new WorldScene.Thing(1, stone, new Vector3(30, 30, 30), Vector3.Zero, 0, false) { Joined = true },
            new WorldScene.Thing(2, stone, new Vector3(40, 30, 40), Vector3.Zero, 0, false));
        var v = new GlView();
        v.Show(s.Scene, null);
        v.SetBoxes([(0, Vector3.Zero, Vector3.One), (1, Vector3.Zero, Vector3.One)]);
        Assert.Equal(ObjectKind.Buildings, v.KindOf(0));
        Assert.Equal(ObjectKind.Ruins, v.KindOf(1));
    }
}
