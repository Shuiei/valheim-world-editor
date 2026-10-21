using System.Numerics;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// the example vehicle mod's vehicles (editor/tests/Support/ExampleMod) in an edit session: parts drawn where the mod builds them (stored at the root), a part
/// moved taking its whole vehicle along (the parts stored at the root's new place), a root deleted taking its parts, a
/// root gone leaving its parts where they are stored, and a saved ship read back where it was drawn.
/// </summary>
[Collection("World files")]
public class EditSessionVehiclesTests
{
    /// <summary>Whether two places are within a millimetre.</summary>
    private static void Near(Vector3 want, Vector3 got) => Assert.True(Vector3.Distance(want, got) < 1e-3f, $"{want} != {got}");

    /// <summary>Whether things are drawn at these places, in any order.</summary>
    private static void DrawnAt(Vector3[] want, EditSession s, List<int> things)
    {
        Assert.Equal(want.Length, things.Count);
        Assert.All(want, p => Assert.Contains(things, i => Vector3.Distance(s.Scene.Things[i].Position, p) < 1e-3f));
    }

    /// <summary>Where the ship's things are drawn: root, east and west floors, swivel, floor on the swivel.</summary>
    private static Vector3[] Drawn(EditSession s, IEnumerable<int> ship) => [.. ship.Select(i => s.Scene.Things[i].Position)];

    /// <summary>The ship of AddShip drawn with its root at (x, 30, z) turned 90°.</summary>
    private static Vector3[] ShipAt(float x, float z) =>
        [new(x, 30, z), new(x, 30, z - 2), new(x, 30, z + 2), new(x + 4, 31, z), new(x + 4, 31, z - 1)];

    [Fact]
    public void PartsAreDrawnWhereTheModBuildsThemAndStoredAtTheRoot()
    {
        var s = TestScenes.Flat(2);
        var ship = TestScenes.AddShip(s);
        Assert.All(ShipAt(40, 40).Zip(Drawn(s, ship)), p => Near(p.First, p.Second));
        // Turned with the root; what is stored stays at the root, as the game saves it.
        Assert.Equal(90, s.Scene.Things[ship[4]].Rotation.Y, 2);
        Assert.All(s.Edits.Added, n => Assert.Equal(new Vector3(40, 30, 40), n.Position));
    }

    [Fact]
    public void MovingAPartMovesTheWholeVehicle()
    {
        var s = TestScenes.Flat(2);
        var ship = TestScenes.AddShip(s);
        var floor = s.Scene.Things[ship[1]];
        var copies = s.Move([(ship[1], floor.Position + new Vector3(5, 0, 0), floor.Rotation)]);
        Assert.Equal(5, copies.Count);
        Assert.All(ship, i => Assert.True(s.Scene.Things[i].Gone));
        // The floor moved first; then the root and the other parts, all stored at the root's new place.
        Assert.Equal(TestScenes.ShipRoot, s.Scene.Things[copies[1]].Prefab);
        Assert.All(ShipAt(45, 40).Zip(Drawn(s, [copies[1], copies[0], .. copies.Skip(2)])), p => Near(p.First, p.Second));
        Assert.All(s.Edits.Added, n => Near(new Vector3(45, 30, 40), n.Position));
        // The root turned a quarter more: the parts go round with it.
        int root = copies[1];
        var turned = s.Transform([(root, s.Scene.Things[root].Position, new Vector3(0, 180, 0), 0)]);
        DrawnAt([new(45, 30, 40), new(43, 30, 40), new(47, 30, 40), new(45, 31, 36), new(44, 31, 36)], s, turned);
        // Undone, twice: the ship as it was.
        s.Undo();
        s.Undo();
        Assert.All(ShipAt(40, 40).Zip(Drawn(s, ship)), p => Near(p.First, p.Second));
    }

    [Fact]
    public void DeletingTheRootTakesItsPartsAndAPartGoesAlone()
    {
        var s = TestScenes.Flat(2);
        var ship = TestScenes.AddShip(s);
        s.Delete([ship[0]]);
        Assert.All(ship, i => Assert.True(s.Scene.Things[i].Gone));
        s.Undo();
        s.Delete([ship[2]]);
        Assert.Equal([ship[2]], ship.Where(i => s.Scene.Things[i].Gone));
        // The Select tool's way: any part brings the whole vehicle.
        s.Undo();
        var (all, parts, groups, noun) = s.WithLinked([ship[4], 99]);
        Assert.Equal([ship[4], 99], all.Take(2));
        Assert.Equal(ship.Order(), all.Where(i => i != 99).Order());
        Assert.Equal(ship.Order(), parts.Order());
        Assert.Equal((1, "vehicle"), (groups, noun));
        Assert.Equal([ship[4]], s.WithLinked([ship[4]], anyPart: false).Indices);
    }

    [Fact]
    public void WithItsRootGoneAShipsPartsStandWhereTheyAreStored()
    {
        var s = TestScenes.Flat(2);
        var ship = TestScenes.AddShip(s);
        s.Commit("Root only", null, [ship[0]], []);
        Assert.All(ship.Skip(1), i => Near(new Vector3(40, 30, 40), s.Scene.Things[i].Position));
        s.Undo();
        Assert.All(ShipAt(40, 40).Zip(Drawn(s, ship)), p => Near(p.First, p.Second));
    }

    // Saved, and the world opened again: the parts are read from the save and drawn where they were.
    [Fact]
    public void ASavedShipIsReadBackWhereItWasDrawn()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var s = WorldScene.Load(dir, 0, 0, 1).Session!;
            TestScenes.AddShip(s, 0, 0);
            Assert.True(s.Save().Saved);
            var world = WorldSave.Load(dir);
            var things = WorldScene.ReadThings(world, 0, 0, 1, 1, new HashSet<int>());
            var floors = things.Where(t => t.Prefab == StableHash.Of("wood_floor") && world.Links.ContainsKey(t.Id)).Select(t => t.Position).ToList();
            Assert.Equal(4, floors.Count);
            foreach (var p in ShipAt(0, 0).Skip(1))
            {
                Assert.Contains(floors, f => Vector3.Distance(f, p) < 0.01f);
            }
            var root = Assert.Single(things, t => t.Prefab == TestScenes.ShipRoot && world.Links.ContainsKey(t.Id));
            Assert.Equal(new Vector3(0, 90, 0), root.Rotation);
            Assert.Equal(TestScenes.ShipId, world.Links[root.Id].Link.Id);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
