using System.Numerics;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.Terrain;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Where a move "put on the ground" lands: on the top of what is under the object where it ends up (a floor), on the
/// ground when nothing is under it or only something taller than it, and a building as one block that keeps its
/// shape, onto a platform and back onto the ground.
/// </summary>
[Collection("World files")]
public class SelectToolLandTests
{
    [AvaloniaFact]
    public void ATreeMovedOntoAFloorLandsOnItsTop()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false), T(2, Floor, 50, 31, 50));
        v.Tool.OnGround = true;
        v.Click(40, 40);
        v.Drag(KeyModifiers.None, (40, 40), (45, 45), (50, 50));
        Assert.Equal(50, v.Last.Position.X, 1);
        Assert.Equal(50, v.Last.Position.Z, 1);
        Assert.Equal(31 + PieceCatalog.Get(Floor)!.MaxY, v.Last.Position.Y, 2);
    }

    // A tree floating 6 m up, moved where nothing is under it, lands on the ground; and so does one moved under a
    // tower taller than itself (it goes under it, not on top).
    [AvaloniaFact]
    public void ATreeMovedWhereNothingIsUnderItLandsOnTheGround()
    {
        var v = Open(T(1, Beech, 40, 36, 40, false), T(2, Beech, 60, 30, 60, false));
        var c = v.S.Scene;
        // The second tree drawn as a 2 × 8 × 2 m tower, its top above the first tree's 5 m box.
        v.W.View.SetBoxes([(1, new Vector3(59 - c.Cx, 30, -(61 - c.Cz)), new Vector3(61 - c.Cx, 38, -(59 - c.Cz)))]);
        v.Tool.OnGround = true;
        v.Click(40, 40);
        v.Drag(KeyModifiers.None, (40, 40), (45, 45), (50, 50));
        Assert.Equal(30, v.Last.Position.Y, 2);
        // The copy is drawn (given its box) so it can be dragged on.
        v.W.View.SetBoxes([Box(c, c.Things.Count - 1)]);
        v.Drag(KeyModifiers.None, (50, 50), (55, 55), (60, 60));
        Assert.Equal(60, v.Last.Position.X, 1);
        Assert.Equal(30, v.Last.Position.Y, 2);
    }

    // Two walls and a floor between them, sunk half a metre, moved onto a 30 cm platform: the whole block goes up so
    // its lowest bottom sits on the platform and the pieces keep their heights apart; moved back onto the grass, the
    // block comes down until its bottom layer sits on the ground, as before.
    [AvaloniaFact]
    public void ABuildingMovedOntoAPlatformGoesUpAsOneBlock()
    {
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Wall, 42, 30, 40), T(3, Floor, 41, 29.5f, 41.2f), T(4, Beech, 62, 30, 62, false));
        var c = v.S.Scene;
        // The tree drawn as a wide, low platform around (62, 62), its top at 30.3 m.
        v.W.View.SetBoxes([(3, new Vector3(55 - c.Cx, 29.9f, -(70 - c.Cz)), new Vector3(70 - c.Cx, 30.3f, -(55 - c.Cz)))]);
        v.Tool.OnGround = true;
        v.Tool.SnapToPieces = false;
        v.W.View.Select([0, 1, 2]);
        v.Drag(KeyModifiers.None, (40, 40), (50, 50), (60, 60));
        var moved = v.S.Scene.Things.Where(t => !t.Gone && t.Prefab != Beech).OrderBy(t => t.Position.X).ToList();
        Assert.Equal(3, moved.Count);
        Assert.Equal([30.8f, 30.3f, 30.8f], moved.Select(t => MathF.Round(t.Position.Y, 2)));
        Assert.Equal([60f, 61f, 62f], moved.Select(t => MathF.Round(t.Position.X, 2)));
        // The copies are drawn (given their boxes) so they can be dragged on: the floor, wide enough for the pointer's
        // ray to meet it where it stands raised.
        v.W.View.SetBoxes(v.Selected.Select(i => Box(c, i)));
        v.Drag(KeyModifiers.None, (61, 61.2f), (51, 51.2f), (41, 31.2f));
        moved = [.. v.S.Scene.Things.Where(t => !t.Gone && t.Prefab != Beech).OrderBy(t => t.Position.X)];
        Assert.Equal([30.5f, 30f, 30.5f], moved.Select(t => MathF.Round(t.Position.Y, 2)));
    }

    // A building with a piece hidden 10 km under it (a mod's crafting station), moved along the grass: the walls and
    // the floor sink half a metre into the ground no more, the floor's bottom on it, and the hidden piece goes along
    // with them instead of lifting them all 10 km to put it on the ground.
    [AvaloniaFact]
    public void APieceBuriedDeepUnderABuildingGoesAlongWithoutLiftingIt()
    {
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Wall, 42, 30, 40), T(3, Floor, 41, 29.5f, 41.2f), T(4, Wall, 41, -9962, 40));
        v.Tool.OnGround = true;
        v.Tool.SnapToPieces = false;
        v.W.View.Select([0, 1, 2, 3]);
        v.Drag(KeyModifiers.None, (40, 40), (45, 40), (50, 40));
        var moved = v.S.Scene.Things.Where(t => !t.Gone).OrderBy(t => t.Position.Y).ToList();
        Assert.Equal([-9961.5f, 30f, 30.5f, 30.5f], moved.Select(t => MathF.Round(t.Position.Y, 2)));
    }
}
