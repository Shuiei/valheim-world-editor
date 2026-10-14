using System.Globalization;
using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A pasted copy of linked objects made a group of its own, by the example vehicle mod (tests/Support/ExampleMod): new ids unique in the world, every reference following them (parents, swivels, masts, switches), its parts
/// stored at its root, what copies leave out gone (ropes, what was only aboard), parts whose parent was not copied let
/// go, a mirrored copy's parts mirrored in their frames. Runs alone (an extension running elsewhere would keep the ropes).
/// </summary>
[Collection("DataDir")]
public class ObjectLinksRemapTests
{
    private const int Root = 10, Swivel = 11, Mast = 12, AnchorA = 13, AnchorB = 14;

    private static readonly int Floor = StableHash.Of("wood_floor");

    // The example mod's data keys.
    private static readonly int IdKey = StableHash.Of(ExampleMod.IdKey), ParentKey = StableHash.Of(ExampleMod.VehicleKey), SwivelKey = StableHash.Of(ExampleMod.MountKey),
        OffsetKey = StableHash.Of(ExampleMod.OffsetKey), EulerKey = StableHash.Of(ExampleMod.TurnKey), SailParentKey = StableHash.Of(ExampleMod.TargetKey),
        SwivelTargetKey = StableHash.Of(ExampleMod.DrivesKey), RopesKey = StableHash.Of(ExampleMod.PackedKey), AboardKey = StableHash.Of(ExampleMod.AboardKey);

    /// <summary>An int value as data text.</summary>
    private static ObjectField Int(int key, int value) => new("ints", key, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>A new object at a place holding this data, as the clipboard pastes it.</summary>
    private static NewObject Made(Vector3 at, params ObjectField[] data) => new(0, Floor, at, Vector3.Zero, 0, Data: data);

    /// <summary>A part of the ship: its parent, its offset (x east in the root's frame) and more data.</summary>
    private static NewObject Part(Vector3 at, float offsetX, params ObjectField[] more) =>
        Made(at, [Int(ParentKey, Root), new ObjectField("vec3", OffsetKey, $"{offsetX.ToString(CultureInfo.InvariantCulture)} 0 0"),
            new ObjectField("vec3", EulerKey, "0 30 10"), .. more]);

    /// <summary>
    /// A pasted ship (its root at 50, 30, 50): a swivel, a part on it, a mast, a sail on the mast, a switch driving the
    /// swivel, two rope anchors (A's ropes, packed in bytes, go to B), a cart aboard, a tree (no group's), each where the
    /// copy shows it.
    /// </summary>
    private static List<NewObject> Ship() =>
    [
        Made(new Vector3(50, 30, 50), Int(IdKey, Root)),
        Part(new Vector3(51, 30, 50), 1, Int(IdKey, Swivel)),
        Part(new Vector3(52, 30, 50), 2, Int(SwivelKey, Swivel)),
        Part(new Vector3(53, 30, 50), 3, Int(IdKey, Mast)),
        Part(new Vector3(54, 30, 50), 4, Int(SailParentKey, Mast)),
        Part(new Vector3(55, 30, 50), 5, Int(SwivelTargetKey, Swivel)),
        Part(new Vector3(56, 30, 50), 6, Int(IdKey, AnchorA),
            new ObjectField("bytes", RopesKey, Convert.ToBase64String([2, AnchorB, 0, 0, 0, 0]))),
        Part(new Vector3(57, 30, 50), 7, Int(IdKey, AnchorB)),
        Made(new Vector3(58, 30, 50), Int(AboardKey, Root)),
        new(0, StableHash.Of("Beech1"), new Vector3(60, 30, 60), Vector3.Zero, 0),
    ];

    /// <summary>An int value of an object's data (null when it has none).</summary>
    private static int? IntOf(NewObject o, int key) =>
        o.Data?.FirstOrDefault(f => f.Key == key && f.Section == "ints")?.Value is { } v ? int.Parse(v, CultureInfo.InvariantCulture) : null;

    [Fact]
    public void EachCopyGetsIdsOfItsOwnAndEveryReferenceFollows()
    {
        var taken = new HashSet<int> { Root, Swivel, Mast, AnchorA, AnchorB, 999 };
        var first = ObjectLinks.Remap(Ship(), taken, random: new Random(1));
        var second = ObjectLinks.Remap(Ship(), taken, random: new Random(1));
        foreach (var copy in new[] { first, second })
        {
            int root = IntOf(copy[0]!, IdKey)!.Value, swivel = IntOf(copy[1]!, IdKey)!.Value;
            int mast = IntOf(copy[3]!, IdKey)!.Value;
            Assert.DoesNotContain(root, new[] { Root, Swivel, Mast, AnchorA, AnchorB, 999 });
            Assert.All(copy.Skip(1).Take(7), o => Assert.Equal(root, IntOf(o!, ParentKey)));
            Assert.Equal(swivel, IntOf(copy[2]!, SwivelKey));
            Assert.Equal(mast, IntOf(copy[4]!, SailParentKey));
            Assert.Equal(swivel, IntOf(copy[5]!, SwivelTargetKey));
            // The ropes name the anchors in bytes the editor cannot rewrite: the copy has none.
            Assert.Null(copy[6]!.Data!.Single(f => f.Key == RopesKey).Value);
            // The parts are stored at the root, as the game saves them; their offsets stay.
            Assert.All(copy.Skip(1).Take(7), o => Assert.Equal(new Vector3(50, 30, 50), o!.Position));
            Assert.Equal("3 0 0", copy[3]!.Data!.Single(f => f.Key == OffsetKey).Value);
            // What was aboard is pasted standing on its own; the tree comes as it was.
            Assert.Null(copy[8]!.Data!.Single(f => f.Key == AboardKey).Value);
            Assert.Equal(new Vector3(58, 30, 50), copy[8]!.Position);
            Assert.Equal(Ship()[9], copy[9]);
        }
        // Every id once: each copy is a vehicle of its own.
        var ids = first.Concat(second).Select(o => IntOf(o, IdKey)).OfType<int>().ToList();
        Assert.Equal(10, ids.Distinct().Count());
        Assert.Subset(taken, ids.ToHashSet());
    }

    // Copied without its root: the parts are no vehicle's any more and stay where they were pasted; a sail whose
    // mast was not copied loses it.
    [Fact]
    public void PartsWhoseParentWasNotCopiedAreLetGo()
    {
        var copy = ObjectLinks.Remap([.. Ship().Skip(3).Take(2)], new HashSet<int>());
        var mast = copy[0]!;
        Assert.Null(IntOf(mast, ParentKey));
        Assert.All([ParentKey, OffsetKey, EulerKey], k => Assert.Null(mast.Data!.Single(f => f.Key == k).Value));
        Assert.Equal(new Vector3(53, 30, 50), mast.Position);
        Assert.NotEqual(Mast, IntOf(mast, IdKey));
        // The sail hangs on the root, which was not copied: let go too, its mast's new id kept.
        var sail = copy[1]!;
        Assert.Null(IntOf(sail, ParentKey));
        Assert.Equal(IntOf(mast, IdKey), IntOf(sail, SailParentKey));
        Assert.Equal(new Vector3(54, 30, 50), sail.Position);
        // A sail alone: its mast reference goes.
        Assert.Null(ObjectLinks.Remap([Ship()[4]], new HashSet<int>())[0]!.Data!.Single(f => f.Key == SailParentKey).Value);
    }

    // A mast standing on its own (no vehicle) and its sail, which only names it: the sail follows the mast's new id.
    [Fact]
    public void WhatOnlyNamesAnotherObjectFollowsItsNewId()
    {
        var copy = ObjectLinks.Remap([Made(new Vector3(1, 30, 1), Int(IdKey, Mast)), Made(new Vector3(1, 32, 1), Int(SailParentKey, Mast))], new HashSet<int>());
        int mast = IntOf(copy[0]!, IdKey)!.Value;
        Assert.NotEqual(Mast, mast);
        Assert.Equal(mast, IntOf(copy[1]!, SailParentKey));
        Assert.Equal(new Vector3(1, 32, 1), copy[1]!.Position);
    }

    [Fact]
    public void AMirroredCopysPartsAreMirroredInTheirParentsFrames()
    {
        var copy = ObjectLinks.Remap([Ship()[0], Ship()[3]], new HashSet<int>(), mirror: true);
        var part = copy[1]!.Data!;
        Assert.Equal("-3 0 0", part.Single(f => f.Key == OffsetKey).Value);
        Assert.Equal("0 -30 -10", part.Single(f => f.Key == EulerKey).Value);
    }

    [Fact]
    public void ACopyOfNoGroupComesBackAsItWas()
    {
        var copy = new List<NewObject> { new(0, Floor, Vector3.One, Vector3.Zero, 0), Made(Vector3.Zero, Int(StableHash.Of("tamed"), 1)) };
        Assert.Equal(copy, ObjectLinks.Remap(copy, new HashSet<int>()));
    }
}
