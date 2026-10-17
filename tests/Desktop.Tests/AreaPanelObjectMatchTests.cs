using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Matching a backup's objects with the current ones: two objects are the same when prefab, place (within a centimetre)
/// and turn (within 0.6°) agree, and an object's metre cell is its prefab with its floored position.
/// </summary>
public class AreaPanelObjectMatchTests
{
    /// <summary>
    /// An object of a prefab at a place and turn, with an id.
    /// </summary>
    private static WorldScene.Thing Obj(int id, int prefab, Vector3 position, Vector3 rotation) =>
        new(id, prefab, position, rotation, 1, false);

    [Fact]
    public void ObjectsMatchOnPrefabPlaceAndTurnNotOnId()
    {
        var at = new Vector3(1, 2, 3);
        Assert.True(AreaPanel.SameObject(Obj(1, 7, at, Vector3.Zero), Obj(2, 7, at, Vector3.Zero)));
    }

    [Fact]
    public void ADifferentPrefabPlaceOrTurnIsNotTheSameObject()
    {
        var at = new Vector3(1, 2, 3);
        var o = Obj(1, 7, at, Vector3.Zero);

        Assert.False(AreaPanel.SameObject(o, Obj(2, 8, at, Vector3.Zero)));
        Assert.False(AreaPanel.SameObject(o, Obj(2, 7, at + new Vector3(0.5f, 0, 0), Vector3.Zero)));
        Assert.False(AreaPanel.SameObject(o, Obj(2, 7, at, new Vector3(0, 90, 0))));
    }

    [Fact]
    public void ATurnOfAFullCircleIsTheSameTurn()
    {
        var at = new Vector3(1, 2, 3);
        Assert.True(AreaPanel.SameObject(Obj(1, 7, at, Vector3.Zero), Obj(2, 7, at, new Vector3(0, 360, 0))));
    }

    [Fact]
    public void ACellIsThePrefabAndTheFlooredMetre()
    {
        var t = Obj(1, 9, new Vector3(1.7f, 0, -0.2f), Vector3.Zero);

        Assert.Equal((9, 1, -1), AreaPanel.CellOf(t));
    }
}
