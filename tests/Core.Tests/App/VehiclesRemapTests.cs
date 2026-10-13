using System.Globalization;
using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A pasted copy of a ValheimRAFT vehicle made a vehicle of its own: new PersistentIDs unique in the world, every
/// reference following them (parents, swivels, masts, switches, rope anchors' ropes), its parts stored at its root,
/// what was aboard left out, parts whose parent was not copied let go, a mirrored copy's parts mirrored in their frames.
/// </summary>
public class VehiclesRemapTests
{
    private const int Root = 10, Swivel = 11, Mast = 12, AnchorA = 13, AnchorB = 14;

    private static readonly int Floor = StableHash.Of("wood_floor");

    /// <summary>An int value as data text.</summary>
    private static ObjectField Int(int key, int value) => new("ints", key, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>A new object at a place holding this data, as the clipboard pastes it.</summary>
    private static NewObject Made(Vector3 at, params ObjectField[] data) => new(0, Floor, at, Vector3.Zero, 0, Data: data);

    /// <summary>A part of the ship: its parent, its offset (x east in the root's frame) and more data.</summary>
    private static NewObject Part(Vector3 at, float offsetX, params ObjectField[] more) =>
        Made(at, [Int(Vehicles.ParentKey, Root), new ObjectField("vec3", Vehicles.OffsetKey, $"{offsetX.ToString(CultureInfo.InvariantCulture)} 0 0"),
            new ObjectField("vec3", Vehicles.EulerKey, "0 30 10"), .. more]);

    /// <summary>
    /// A pasted ship (its root at 50, 30, 50): a swivel, a part on it, a mast, a sail on the mast, a switch driving the
    /// swivel, two rope anchors (A's ropes go to B and to an anchor not copied), a cart aboard, a tree (no vehicle's),
    /// each where the copy shows it.
    /// </summary>
    private static List<NewObject> Ship() =>
    [
        Made(new Vector3(50, 30, 50), Int(Vehicles.PersistentIdKey, Root)),
        Part(new Vector3(51, 30, 50), 1, Int(Vehicles.PersistentIdKey, Swivel)),
        Part(new Vector3(52, 30, 50), 2, Int(Vehicles.SwivelKey, Swivel)),
        Part(new Vector3(53, 30, 50), 3, Int(Vehicles.PersistentIdKey, Mast)),
        Part(new Vector3(54, 30, 50), 4, Int(Vehicles.SailParentKey, Mast)),
        Part(new Vector3(55, 30, 50), 5, Int(Vehicles.SwivelTargetKey, Swivel)),
        Part(new Vector3(56, 30, 50), 6, Int(Vehicles.PersistentIdKey, AnchorA),
            new ObjectField("bytes", Vehicles.RopesKey, Convert.ToBase64String(Vehicles.WriteRopes([(AnchorB, 0), (999, 1)])))),
        Part(new Vector3(57, 30, 50), 7, Int(Vehicles.PersistentIdKey, AnchorB)),
        Made(new Vector3(58, 30, 50), Int(Vehicles.AboardKey, Root)),
        new(0, StableHash.Of("Beech1"), new Vector3(60, 30, 60), Vector3.Zero, 0),
    ];

    /// <summary>An int value of an object's data (null when it has none).</summary>
    private static int? IntOf(NewObject o, int key) =>
        o.Data?.FirstOrDefault(f => f.Key == key && f.Section == "ints")?.Value is { } v ? int.Parse(v, CultureInfo.InvariantCulture) : null;

    [Fact]
    public void EachCopyGetsIdsOfItsOwnAndEveryReferenceFollows()
    {
        var taken = new HashSet<int> { Root, Swivel, Mast, AnchorA, AnchorB, 999 };
        var first = Vehicles.Remap(Ship(), taken, random: new Random(1));
        var second = Vehicles.Remap(Ship(), taken, random: new Random(1));
        foreach (var copy in new[] { first, second })
        {
            int root = IntOf(copy[0]!, Vehicles.PersistentIdKey)!.Value, swivel = IntOf(copy[1]!, Vehicles.PersistentIdKey)!.Value;
            int mast = IntOf(copy[3]!, Vehicles.PersistentIdKey)!.Value, b = IntOf(copy[7]!, Vehicles.PersistentIdKey)!.Value;
            Assert.DoesNotContain(root, new[] { Root, Swivel, Mast, AnchorA, AnchorB, 999 });
            Assert.All(copy.Skip(1).Take(7), o => Assert.Equal(root, IntOf(o!, Vehicles.ParentKey)));
            Assert.Equal(swivel, IntOf(copy[2]!, Vehicles.SwivelKey));
            Assert.Equal(mast, IntOf(copy[4]!, Vehicles.SailParentKey));
            Assert.Equal(swivel, IntOf(copy[5]!, Vehicles.SwivelTargetKey));
            // The rope to B goes to the copy's B; the one to an anchor not copied goes.
            string ropes = copy[6]!.Data!.Single(f => f.Key == Vehicles.RopesKey).Value!;
            Assert.Equal([(b, 0)], Vehicles.ReadRopes(Convert.FromBase64String(ropes))!);
            // The parts are stored at the root, as the game saves them; their offsets stay.
            Assert.All(copy.Skip(1).Take(7), o => Assert.Equal(new Vector3(50, 30, 50), o!.Position));
            Assert.Equal("3 0 0", copy[3]!.Data!.Single(f => f.Key == Vehicles.OffsetKey).Value);
            // What was aboard stays with the original; the tree comes as it was.
            Assert.Null(copy[8]);
            Assert.Equal(Ship()[9], copy[9]);
        }
        // Every id once: each copy is a vehicle of its own.
        var ids = first.Concat(second).Select(o => o == null ? null : IntOf(o, Vehicles.PersistentIdKey)).OfType<int>().ToList();
        Assert.Equal(10, ids.Distinct().Count());
        Assert.Subset(taken, ids.ToHashSet());
    }

    // Copied without its root: the parts are no vehicle's any more and stay where they were pasted; a sail whose
    // mast was not copied loses it.
    [Fact]
    public void PartsWhoseParentWasNotCopiedAreLetGo()
    {
        var copy = Vehicles.Remap([.. Ship().Skip(3).Take(2)], new HashSet<int>());
        var mast = copy[0]!;
        Assert.Null(IntOf(mast, Vehicles.ParentKey));
        Assert.All([Vehicles.ParentKey, Vehicles.OffsetKey, Vehicles.EulerKey], k => Assert.Null(mast.Data!.Single(f => f.Key == k).Value));
        Assert.Equal(new Vector3(53, 30, 50), mast.Position);
        Assert.NotEqual(Mast, IntOf(mast, Vehicles.PersistentIdKey));
        // The sail hangs on the root, which was not copied: let go too, its mast's new id kept.
        var sail = copy[1]!;
        Assert.Null(IntOf(sail, Vehicles.ParentKey));
        Assert.Equal(IntOf(mast, Vehicles.PersistentIdKey), IntOf(sail, Vehicles.SailParentKey));
        Assert.Equal(new Vector3(54, 30, 50), sail.Position);
        // A sail alone: its mast reference goes.
        Assert.Null(Vehicles.Remap([Ship()[4]], new HashSet<int>())[0]!.Data!.Single(f => f.Key == Vehicles.SailParentKey).Value);
    }

    // A mast standing on its own (no vehicle) and its sail, which only names it: the sail follows the mast's new id.
    [Fact]
    public void WhatOnlyNamesAnotherObjectFollowsItsNewId()
    {
        var copy = Vehicles.Remap([Made(new Vector3(1, 30, 1), Int(Vehicles.PersistentIdKey, Mast)), Made(new Vector3(1, 32, 1), Int(Vehicles.SailParentKey, Mast))], new HashSet<int>());
        int mast = IntOf(copy[0]!, Vehicles.PersistentIdKey)!.Value;
        Assert.NotEqual(Mast, mast);
        Assert.Equal(mast, IntOf(copy[1]!, Vehicles.SailParentKey));
        Assert.Equal(new Vector3(1, 32, 1), copy[1]!.Position);
    }

    [Fact]
    public void AMirroredCopysPartsAreMirroredInTheirParentsFrames()
    {
        var copy = Vehicles.Remap([Ship()[0], Ship()[3]], new HashSet<int>(), mirror: true);
        var part = copy[1]!.Data!;
        Assert.Equal("-3 0 0", part.Single(f => f.Key == Vehicles.OffsetKey).Value);
        Assert.Equal("0 -30 -10", part.Single(f => f.Key == Vehicles.EulerKey).Value);
    }

    [Fact]
    public void ACopyOfNoVehicleComesBackAsItWas()
    {
        var copy = new List<NewObject> { new(0, Floor, Vector3.One, Vector3.Zero, 0), Made(Vector3.Zero, Int(StableHash.Of("tamed"), 1)) };
        Assert.Equal(copy, Vehicles.Remap(copy, new HashSet<int>()));
    }

    [Fact]
    public void RopesAreReadAndWrittenAsTheModKeepsThem()
    {
        byte[] bytes = Vehicles.WriteRopes([(AnchorA, 2), (-5, 0)]);
        Assert.Equal(11, bytes.Length);
        Assert.Equal(2, bytes[0]);
        Assert.Equal([(AnchorA, 2), (-5, 0)], Vehicles.ReadRopes(bytes)!);
        // An older list (anchors by ZDOID) or damaged bytes are not read.
        Assert.Null(Vehicles.ReadRopes([1, 0, 0]));
        Assert.Null(Vehicles.ReadRopes([2, 0, 0]));
        Assert.Null(Vehicles.ReadRopes([]));
        Assert.Empty(Vehicles.ReadRopes([2])!);
    }
}
