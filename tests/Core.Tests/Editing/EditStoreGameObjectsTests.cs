using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Live: an added object takes the place, turn, size and data the game has for it, under its own id, and nothing
/// else moves.
/// </summary>
public class EditStoreGameObjectsTests
{
    [Fact]
    public void AnAddedObjectTakesWhatTheGameHasForItUnderItsOwnId()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
        var edits = new EditStore(world);
        var at = new Vector3(640, 30, 640);
        var placed = new NewObject(-1, StableHash.Of("Deer"), at, Vector3.Zero, 0f, Data: [new ObjectField("longs", ObjectField.CreatorKey, "7")]);
        edits.AddObjects([placed]);
        int version = edits.ObjectsVersion;
        // The game moved it, turned it, sized it and gave it data.
        byte[] game = MadeObjects.Zdo("Deer", at + new Vector3(0.5f, -0.2f, 0), z =>
        {
            z.Rotation = new Vector3(0, 90, 0);
            z.Vec3List.Add((ZdoData.ScaleKey, new Vector3(1.5f, 1.5f, 1.5f)));
            z.FloatList.Add((StableHash.Of("max_health"), 10f));
        });

        Assert.True(edits.TakeFromGame(-1, game));

        NewObject n = edits.FindAdded(-1)!;
        Assert.Equal((at + new Vector3(0.5f, -0.2f, 0), new Vector3(0, 90, 0), 1.5f), (n.Position, n.Rotation, n.Scale));
        Assert.Equal(game, n.Raw);
        Assert.False(n.Fresh);
        Assert.Null(n.Data);
        Assert.Equal(StableHash.Of("Deer"), n.Prefab);
        Assert.Equal([n], edits.Added);
        Assert.NotEqual(version, edits.ObjectsVersion);
        // Written as the game has it from now on (its data, the place and turn it gave).
        Assert.Equal(game, world.NewObjectBytes(n, world.SourceReader()));
        // The same again changes nothing; nor an object not added now.
        version = edits.ObjectsVersion;
        Assert.False(edits.TakeFromGame(-1, game));
        Assert.False(edits.TakeFromGame(-2, game));
        Assert.Equal(version, edits.ObjectsVersion);
        edits.SetDeleted([-1], true);
        Assert.False(edits.TakeFromGame(-1, MadeObjects.Zdo("Deer", at)));
        Assert.Equal(game, edits.FindAdded(-1)!.Raw);
    }

    [Fact]
    public void AScalarScaleIsTheSizeAndNoScaleIsNone()
    {
        var edits = new EditStore(WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test"));
        var at = new Vector3(640, 30, 640);
        edits.AddObjects([new NewObject(-1, StableHash.Of("Deer"), at, Vector3.Zero, 2f), new NewObject(-2, StableHash.Of("Deer"), at, Vector3.Zero, 2f)]);

        edits.TakeFromGame(-1, MadeObjects.Zdo("Deer", at, z => z.FloatList.Add((ZdoData.ScaleScalarKey, 0.5f))));
        edits.TakeFromGame(-2, MadeObjects.Zdo("Deer", at));

        Assert.Equal(0.5f, edits.FindAdded(-1)!.Scale);
        Assert.Equal(0f, edits.FindAdded(-2)!.Scale);
    }
}
