using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Tie groups in a session (a manifest's "ties": a train naming its wagons): selecting any member brings the whole
/// group, deleting the root takes its members, and moving or turning one moves the rest as one block. Runs alone (the
/// manifests in use are shared).
/// </summary>
[Collection("DataDir")]
public sealed class EditSessionTiesTests : IDisposable
{
    private const string Ties = """{ "version": 1, "ties": [ { "name": "train", "root": "Wagons", "member": "Self", "idBytes": 4 } ] }""";

    // A prefab the test world can make objects of (its data holding the ties).
    private static readonly int Car = StableHash.Of("Rock_4");

    public EditSessionTiesTests() => ModManifests.Base = [ExampleMod.Manifest, Ties];

    public void Dispose() => ModManifests.Base = [ExampleMod.Manifest];

    private static ObjectField Bytes(string key, params byte[] ids) => new("bytes", StableHash.Of(key), Convert.ToBase64String(ids));

    /// <summary>A train at (10, 30, 5) naming two wagons east of it, and a car of no train.</summary>
    private static (EditSession S, int Train, int First, int Second, int Alone) Railway()
    {
        var s = TestScenes.Flat(1);
        var made = s.Commit("Put", null, [],
        [
            (new NewObject(0, Car, new Vector3(10, 30, 5), Vector3.Zero, 0, Data: [Bytes("Wagons", 1, 0, 0, 0, 2, 0, 0, 0)]), true),
            (new NewObject(0, Car, new Vector3(12, 30, 5), Vector3.Zero, 0, Data: [Bytes("Self", 1, 0, 0, 0)]), true),
            (new NewObject(0, Car, new Vector3(14, 30, 5), Vector3.Zero, 0, Data: [Bytes("Self", 2, 0, 0, 0)]), true),
            (new NewObject(0, Car, new Vector3(30, 30, 5), Vector3.Zero, 0, Data: [Bytes("Self", 3, 0, 0, 0)]), true),
        ]);
        return (s, made[0], made[1], made[2], made[3]);
    }

    [Fact]
    public void AnyMemberBringsItsWholeGroup()
    {
        var (s, train, first, second, alone) = Railway();

        var (indices, parts, groups, noun) = s.WithLinked([second]);

        Assert.Equal([second, train, first], indices);
        Assert.Equal([train, first, second], parts.Order());
        Assert.Equal((1, "train"), (groups, noun));
        Assert.Equal([alone], s.WithLinked([alone]).Indices);
        // Only the root brings its members where that is asked (deleting).
        Assert.Equal([second], s.WithLinked([second], anyPart: false).Indices);
    }

    [Fact]
    public void DeletingTheRootTakesItsMembers()
    {
        var (s, train, first, second, alone) = Railway();

        s.Delete([train]);

        Assert.True(s.Scene.Things[train].Gone && s.Scene.Things[first].Gone && s.Scene.Things[second].Gone);
        Assert.False(s.Scene.Things[alone].Gone);
    }

    [Fact]
    public void MovingTheRootMovesItsMembersAsOneBlock()
    {
        var (s, train, _, _, _) = Railway();

        // A quarter turn about y (Unity's): east of the train becomes south of it.
        var moved = s.Move([(train, new Vector3(20, 30, 5), new Vector3(0, 90, 0))]);

        Assert.Equal(3, moved.Count);
        var places = moved.Select(i => s.Scene.Things[i].Position).ToList();
        Assert.Contains(places, p => Vector3.Distance(p, new Vector3(20, 30, 3)) < 1e-3f);
        Assert.Contains(places, p => Vector3.Distance(p, new Vector3(20, 30, 1)) < 1e-3f);
        Assert.All(moved.Skip(1), i => Assert.Equal(90, s.Scene.Things[i].Rotation.Y, 3));
    }
}
