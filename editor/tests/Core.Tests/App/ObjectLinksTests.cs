using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Objects mods link into one, by the example vehicle mod (editor/tests/Support/ExampleMod): a link read from an
/// object's data (and set over by new values), a part drawn at its offset in its root's frame (on a swivel, in the
/// swivel's), turned and leaning as the mod builds it, a part whose parent is missing or whose chain loops left where it
/// is stored, and a group's parts found from any of them. Runs alone (the manifests' rules are shared).
/// </summary>
[Collection("DataDir")]
public class ObjectLinksTests
{
    private const int Root = 10, Swivel = 11;

    /// <summary>A link to a parent (and a swivel), with an offset and an Euler turn in its frame.</summary>
    private static ObjectLink Part(int parent, Vector3 offset, Vector3 euler, int swivel = 0, int id = 0) => new(ExampleMod.Rule, id, [swivel, parent], offset, euler, null);

    /// <summary>A root's link: its id alone.</summary>
    private static ObjectLink RootLink(int id) => new(ExampleMod.Rule, id, [0, 0], Vector3.Zero, null, null);

    /// <summary>
    /// A ship's root at (100, 30, 200) turned 90° (id 1, id 10); a part 2 m east and 1 m up of it in its frame
    /// (2); a swivel 3 m north of it (3, id 11); a part 1 m east on the swivel, turned 90° more (4); a part
    /// leaning 30° (5); a part turned by the old quaternion only (6); and more if given. The parts are stored at the root.
    /// </summary>
    private static LinkIndex Ship(params LinkIndex.Member[] more)
    {
        var at = new Vector3(100, 30, 200);
        var yaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        return LinkIndex.Of(
        [
            new(1, at, new Vector3(0, 90, 0), RootLink(Root)),
            new(2, at, Vector3.Zero, Part(Root, new Vector3(2, 1, 0), Vector3.Zero)),
            new(3, at, Vector3.Zero, Part(Root, new Vector3(0, 0, 3), Vector3.Zero, id: Swivel)),
            new(4, at, Vector3.Zero, Part(Root, new Vector3(1, 0, 0), new Vector3(0, 90, 0), swivel: Swivel)),
            new(5, at, Vector3.Zero, Part(Root, Vector3.Zero, new Vector3(30, 0, 0))),
            new(6, at, Vector3.Zero, new ObjectLink(ExampleMod.Rule, 0, [0, Root], Vector3.Zero, null, yaw)),
            .. more,
        ]);
    }

    /// <summary>Whether two places are within a millimetre.</summary>
    private static void Near(Vector3 want, Vector3 got) => Assert.True(Vector3.Distance(want, got) < 1e-3f, $"{want} != {got}");

    /// <summary>Whether two Euler turns are the same turn (each angle within a hundredth of a degree, 360 apart allowed).</summary>
    private static void SameTurn(Vector3 want, Vector3 got)
    {
        static float D(float a, float b) => MathF.Abs(((a - b) % 360 + 540) % 360 - 180);
        Assert.True(D(want.X, got.X) < 0.01f && D(want.Y, got.Y) < 0.01f && D(want.Z, got.Z) < 0.01f, $"{want} != {got}");
    }

    [Fact]
    public void ALinkIsReadFromAnObjectsDataTheEulerTurnFirst()
    {
        var z = ZdoData.Parse(MadeObjects.VehiclePart("wood_floor", Vector3.Zero, Root, new Vector3(1, 2, 3), new Vector3(0, 45, 0), Swivel, 12));
        z.QuatList.Add((StableHash.Of(ExampleMod.TurnQuatKey), Quaternion.Identity));
        var link = ObjectLinks.Read(z)!;
        Assert.Same(ExampleMod.Rule, link.Rule);
        Assert.Equal((12, new Vector3(1, 2, 3)), (link.Id, link.Offset));
        Assert.Equal([Swivel, Root], link.Parents);
        // On a swivel (its first parent key): it hangs on the swivel, turned by its Euler angles, not the quaternion.
        Assert.Equal(Swivel, link.Holder);
        SameTurn(new Vector3(0, 45, 0), BlueprintFormats.ToEuler(link.Turn));
        // Only aboard for now (characters, carts): no part, the mod keeps it where it is.
        var aboard = ZdoData.Parse(MadeObjects.Zdo("Cart", Vector3.Zero));
        aboard.IntList.Add((StableHash.Of(ExampleMod.AboardKey), Root));
        Assert.Null(ObjectLinks.Read(aboard));
        // No group's data: no link; a damaged quaternion turns nothing.
        Assert.Null(ObjectLinks.Read(ZdoData.Parse(MadeObjects.Zdo("wood_floor", Vector3.Zero))));
        Assert.Equal(Quaternion.Identity, new ObjectLink(ExampleMod.Rule, 0, [0, Root], Vector3.Zero, null, default(Quaternion)).Turn);
    }

    [Fact]
    public void ValuesSetOverALinkChangeItAndDamagedOnesAreLeftOut()
    {
        var start = Part(Root, new Vector3(1, 0, 0), Vector3.Zero);
        Assert.Same(start, ObjectLinks.With(start, [new ObjectField("ints", StableHash.Of("tamed"), "1")]));
        var moved = ObjectLinks.With(start,
        [
            new ObjectField("vec3", StableHash.Of(ExampleMod.OffsetKey), "4 5 6"),
            new ObjectField("ints", StableHash.Of(ExampleMod.IdKey), "77"),
            new ObjectField("ints", StableHash.Of(ExampleMod.MountKey), "not a number"),
        ])!;
        Assert.Equal((77, Root, new Vector3(4, 5, 6)), (moved.Id, moved.Holder, moved.Offset));
        Assert.Equal([0, Root], moved.Parents);
        // Its parent taken away: nothing of a group left.
        Assert.Null(ObjectLinks.With(start, [new ObjectField("ints", StableHash.Of(ExampleMod.VehicleKey), null)]));
    }

    // An object added in this session: moved, it keeps its saved link; edited, its bytes say; a fresh copy has only
    // what its data gives.
    [Fact]
    public void AnAddedObjectsLinkComesFromItsSourceItsBytesOrItsData()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        var at = new Vector3(640, 30, 640);
        var came = world.MergeLive([(10, 10)], [(4242, 1, MadeObjects.VehiclePart("wood_floor", at, Root, new Vector3(1, 0, 0), Vector3.Zero))], _ => false, _ => false)!;
        int saved = Assert.Single(came.Added);
        int floor = StableHash.Of("wood_floor");
        Assert.Equal(Root, ObjectLinks.LinkOf(world, new NewObject(-1, floor, at, Vector3.Zero, 0, saved, Fresh: false))!.Holder);
        Assert.Null(ObjectLinks.LinkOf(world, new NewObject(-2, floor, at, Vector3.Zero, 0, saved)));
        byte[] raw = MadeObjects.VehiclePart("wood_floor", at, 99, Vector3.Zero, Vector3.Zero);
        Assert.Equal(99, ObjectLinks.LinkOf(world, new NewObject(-3, floor, at, Vector3.Zero, 0, Raw: raw))!.Holder);
        Assert.Equal(5, ObjectLinks.LinkOf(world, new NewObject(-4, floor, at, Vector3.Zero, 0, Data: [new ObjectField("ints", StableHash.Of(ExampleMod.VehicleKey), "5")]))!.Holder);
    }

    [Fact]
    public void PartsAreDrawnAtTheirOffsetsInTheirParentsFrames()
    {
        var ship = Ship();
        // The root where it is stored, leaning as stored.
        Assert.Equal((new Vector3(100, 30, 200), new Vector3(0, 90, 0)), ship.Pose(1));
        // Turned with the root: 2 m east in its frame is 2 m south in the world (Unity's yaw turns east to south).
        var (p, r) = ship.Pose(2)!.Value;
        Near(new Vector3(100, 31, 198), p);
        SameTurn(new Vector3(0, 90, 0), r);
        (p, _) = ship.Pose(3)!.Value;
        Near(new Vector3(103, 30, 200), p);
        // On the swivel: in the swivel's frame, turned with it.
        (p, r) = ship.Pose(4)!.Value;
        Near(new Vector3(103, 30, 199), p);
        SameTurn(new Vector3(0, 180, 0), r);
        // Leaning in the root's frame: the lean stays, the root's turn added.
        (_, r) = ship.Pose(5)!.Value;
        SameTurn(new Vector3(30, 90, 0), r);
        (_, r) = ship.Pose(6)!.Value;
        SameTurn(new Vector3(0, 180, 0), r);
        Assert.Null(ship.Pose(99));
    }

    [Fact]
    public void APartWhoseParentIsMissingOrWhoseChainLoopsStaysWhereItIsStored()
    {
        var at = new Vector3(5, 30, 5);
        var ix = LinkIndex.Of(
        [
            new(1, at, new Vector3(0, 10, 0), Part(404, new Vector3(9, 9, 9), Vector3.Zero)),
            // Two holding each other.
            new(2, at, Vector3.Zero, Part(21, new Vector3(1, 0, 0), Vector3.Zero, id: 20)),
            new(3, at + Vector3.UnitX, Vector3.Zero, Part(20, new Vector3(1, 0, 0), Vector3.Zero, id: 21)),
        ]);
        Assert.Equal((at, new Vector3(0, 10, 0)), ix.Pose(1));
        Assert.Equal((at, Vector3.Zero), ix.Pose(2));
        Assert.Equal([2, 3], ix.Group(2).Order());
        // A chain deeper than a mod's groups go (damaged data) is taken for one that loops.
        var deep = LinkIndex.Of(Enumerable.Range(1, ObjectLinks.MaxDepth + 3)
            .Select(i => new LinkIndex.Member(i, at, Vector3.Zero, Part(i == 1 ? 0 : i - 1, Vector3.UnitX, Vector3.Zero, id: i))));
        Assert.Equal(at, deep.Pose(ObjectLinks.MaxDepth + 3)!.Value.Position);
        Assert.Equal(at + new Vector3(3, 0, 0), deep.Pose(4)!.Value.Position);
    }

    [Fact]
    public void AGroupsPartsAreFoundFromAnyOfThem()
    {
        var ship = Ship();
        var group = ship.Group(4);
        Assert.Equal(1, group[0]);
        Assert.Equal([1, 2, 3, 4, 5, 6], group.Order());
        Assert.Equal(1, ship.RootOf(4));
        // Called as the mod's manifest says; an object with no link is a group of its own.
        Assert.Equal("vehicle", ship.NameOf(4));
        Assert.Null(ship.NameOf(99));
        Assert.Equal([99], ship.Group(99));
        Assert.Equal(99, ship.RootOf(99));
        Assert.Equal([Root, Swivel], ship.Ids.Order());
    }

    [Fact]
    public void AnIdUnderAnotherRulesKeyIsNotWhatAPartHangsOn()
    {
        // Another mod's rule whose objects happen to hold the ship's id under their own key.
        var other = ExampleMod.Rule with { Name = "cart", IdKey = StableHash.Of("OtherMod_Id") };
        var ship = Ship(new LinkIndex.Member(7, Vector3.Zero, Vector3.Zero, new ObjectLink(other, Root, [0, 0], Vector3.Zero, null, null)),
            new LinkIndex.Member(8, Vector3.Zero, Vector3.Zero, new ObjectLink(other, 0, [0, Swivel], Vector3.One, null, null)));

        Assert.Equal(1, ship.RootOf(2));
        Assert.Equal(1, ship.Holder(ExampleMod.Rule.IdKey, Root)!.Id);
        Assert.Equal(7, ship.Holder(other.IdKey, Root)!.Id);
        // The cart's part names an id only the ship's rule holds: it hangs on nothing, and stays where it is stored.
        Assert.Equal(8, ship.RootOf(8));
        Assert.Equal((Vector3.Zero, Vector3.Zero), ship.Pose(8));
        Assert.Equal([1, 2, 3, 4, 5, 6], ship.Group(1).Order());
    }

    // Over a world: the saved parts (not the deleted ones, whose ids stay taken) and the session's, a moved root
    // drawing its parts where it went.
    [Fact]
    public void AWorldsGroupsAreItsObjectsAndTheSessionsDeletedOnesLeftOut()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        var at = new Vector3(640, 30, 640);
        var came = world.MergeLive([(10, 10)],
        [
            (4242, 1, MadeObjects.VehicleRoot("ValheimVehicles_WaterVehicleShip", at, new Vector3(0, 90, 0), Root)),
            (4242, 2, MadeObjects.VehiclePart("wood_floor", at, Root, new Vector3(2, 0, 0), Vector3.Zero)),
        ], _ => false, _ => false)!;
        int root = came.Added[0], part = came.Added[1];
        Assert.Equal(new Vector3(0, 90, 0), world.Links[root].Rotation);
        var ix = LinkIndex.Of(world, [], []);
        Near(at + new Vector3(0, 0, -2), ix.Pose(part)!.Value.Position);
        // The root moved 10 m east (a copy keeping its data in its place).
        var moved = new NewObject(-1, StableHash.Of(ExampleMod.Hull), at + new Vector3(10, 0, 0), new Vector3(0, 90, 0), 0, root, Fresh: false);
        ix = LinkIndex.Of(world, [root], [moved]);
        Near(at + new Vector3(10, 0, -2), ix.Pose(part)!.Value.Position);
        Assert.Equal([-1, part], ix.Group(part));
        Assert.Contains(Root, LinkIndex.Of(world, [root, part], []).Ids);
        Assert.Null(LinkIndex.Of(world, [root, part], []).Pose(part));
    }
}
