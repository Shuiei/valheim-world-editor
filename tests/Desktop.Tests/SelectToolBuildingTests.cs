using System.Numerics;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool's double clicks: a whole building, a whole ship (the example vehicle mod's, its parts moved and
/// deleted together), and a chest on a building's floor.
/// </summary>
[Collection("World files")]
public class SelectToolBuildingTests
{
    [AvaloniaFact]
    public void ADoubleClickSelectsTheWholeBuilding()
    {
        // Two walls end to end and a floor against them: one building; a wall further off is another.
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Wall, 42, 30, 40), T(3, Floor, 41, 29.5f, 41.2f), T(4, Wall, 55, 30, 55), T(5, Beech, 43.5f, 30, 40, false));
        v.Click(40, 40, clicks: 2);
        Assert.Equal([0, 1, 2], v.Selected);
        // From the button too, adding to what is selected.
        v.Click(55, 55);
        v.Tool.WholeBuilding();
        Assert.Equal([3], v.Selected);
        // A tree is not a building.
        string? said = null;
        v.Tool.Message += (t, _) => said = t.Shown;
        v.W.View.Select([4]);
        v.Tool.WholeBuilding();
        Assert.StartsWith("Select a building piece first", said);
    }

    // A ship (the example vehicle mod's) (TestScenes.AddShip): a double click on a part selects the whole vehicle; a part dragged
    // takes it all along, kept afloat (its height as it was), every part stored at the root's new place; deleted, the
    // whole ship goes.
    [AvaloniaFact]
    public void AShipsPartBringsTheWholeShip()
    {
        var v = Open(T(1, Beech, 10, 30, 10, false));
        var ship = TestScenes.AddShip(v.S);
        v.W.View.SetBoxes(ship.Select(i => Box(v.S.Scene, i)));
        string? said = null;
        v.Tool.Message += (t, _) => said = t.Shown;
        v.Click(40, 38, clicks: 2);
        Assert.Equal(ship.Order(), v.Selected.Order());
        Assert.Equal("Selected the whole vehicle: 5 part(s).", said);
        v.W.View.Select([]);
        v.Click(40, 38);
        Assert.Equal([ship[1]], v.Selected);
        // Exact place shows the whole ship's middle (as a move takes it); End drops nothing (a ship keeps its height).
        var (x, y, z, _, count) = v.Tool.Where()!.Value;
        Assert.Equal((5, 41.6f, 30f, 39.8f), (count, MathF.Round(x, 3), MathF.Round(y, 3), MathF.Round(z, 3)));
        v.Tool.Drop();
        Assert.Equal("Nothing to drop: a vehicle keeps its height.", said);
        v.Tool.Cancel();
        Assert.All(ship, i => Assert.False(v.S.Scene.Things[i].Gone));
        v.Drag(KeyModifiers.None, (40, 38), (43, 38), (45, 38));
        Assert.All(ship, i => Assert.True(v.S.Scene.Things[i].Gone));
        Assert.StartsWith("Moved 5 object(s) (a whole vehicle", said);
        var copies = Enumerable.Range(ship[^1] + 1, 5).ToList();
        Assert.Equal(copies, v.Selected.Order());
        var root = v.S.Scene.Things[Assert.Single(copies, i => v.S.Scene.Things[i].Prefab == TestScenes.ShipRoot)];
        Assert.Equal(45, root.Position.X, 1);
        Assert.Equal(40, root.Position.Z, 1);
        Assert.Equal(30, root.Position.Y, 3);
        Assert.All(copies, i => Assert.Equal(root.Position, v.S.Edits.FindAdded(v.S.Scene.Things[i].Id)!.Position));
        Assert.Contains(copies, i => Vector3.Distance(v.S.Scene.Things[i].Position, root.Position + new Vector3(4, 1, -1)) < 1e-3f);
        // One part deleted: the whole ship goes.
        v.W.View.Select([copies[0]]);
        v.Tool.Delete();
        Assert.All(copies, i => Assert.True(v.S.Scene.Things[i].Gone));
        Assert.StartsWith("Deleted 5 object(s) (a whole vehicle: a vehicle's parts never go alone)", said);
    }

    [AvaloniaFact]
    public void ADoubleClickOnAChestInABuildingSelectsTheChestAlone()
    {
        // The chest stands on the building's floor; its contents are what a double click is for.
        var v = Open(T(1, Wall, 40, 30, 40), T(2, Floor, 41, 29.5f, 41.2f), T(3, StableHash.Of("piece_chest_wood"), 41, 29.6f, 41.6f));
        v.Click(41, 41.6f, clicks: 2);
        Assert.Equal([2], v.Selected);
        v.Click(40, 40, clicks: 2);
        Assert.Equal([0, 1, 2], v.Selected);
    }
}
