using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The fields a new object's bytes are given (WorldSave.WithFields): a built piece takes its builder as its creator, a
/// served piece takes the piece mark, and the object's own data fields are set over the rest.
/// </summary>
public class WorldSaveNewObjectFieldsTests
{
    private static readonly int PieceKey = StableHash.Of("piece");

    private static ZdoData WithFields(NewObject n, long builder, bool built, bool served) =>
        ZdoData.Parse((byte[])PrivateCall.Static(typeof(WorldSave), "WithFields", MadeObjects.Zdo("wood_floor", Vector3.Zero), n, builder, built, served)!);

    private static NewObject Blank(IReadOnlyList<ObjectField>? data = null) =>
        new(1, StableHash.Of("wood_floor"), Vector3.Zero, Vector3.Zero, 1f, Data: data);

    [Fact]
    public void ABuiltPieceTakesItsBuilderAsItsCreator()
    {
        var z = WithFields(Blank(), 42, built: true, served: false);
        Assert.Contains((ObjectField.CreatorKey, 42L), z.LongList);
    }

    [Fact]
    public void AServedPieceIsMarkedAsAPiece()
    {
        var z = WithFields(Blank(), 0, built: false, served: true);
        Assert.Contains((PieceKey, 1), z.IntList);
    }

    [Fact]
    public void ANotBuiltPieceKeepsNoCreator()
    {
        var z = WithFields(Blank(), 42, built: false, served: false);
        Assert.DoesNotContain(z.LongList, e => e.Key == ObjectField.CreatorKey);
    }

    [Fact]
    public void TheObjectsOwnDataFieldsAreSetOnTheBytes()
    {
        var z = WithFields(Blank([new ObjectField("ints", 7, "5")]), 0, built: false, served: false);
        Assert.Contains((7, 5), z.IntList);
    }
}
