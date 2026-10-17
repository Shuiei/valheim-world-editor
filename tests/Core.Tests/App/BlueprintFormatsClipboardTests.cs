using System.Numerics;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A blueprint turned into the clipboard's objects and ground (BlueprintFormats.PieceObjects and TerrainGrid): positions
/// relative to the copy's centre and lowest piece, the scale and data of each object, and the ground grid of the terrain
/// marks, with later marks over earlier ones.
/// </summary>
public class BlueprintFormatsClipboardTests
{
    /// <summary>A parsed blueprint with the given terrain marks and no pieces.</summary>
    private static BlueprintFormats.Parsed Terrain(params BlueprintFormats.TerrainMod[] marks) =>
        new("t", null, null, [], [.. marks], 0);

    /// <summary>A round terrain mark of the given radius, at (0, height, 0), painted with the given paint.</summary>
    private static BlueprintFormats.TerrainMod Mark(float height, string paint = "", float radius = 0.5f) =>
        new("circle", new Vector3(0, height, 0), radius, 0, paint);

    [Fact]
    public void PositionsAreRelativeToTheCentreAndTheLowestPiece()
    {
        List<BlueprintFormats.Piece> pieces = [new("wood", new Vector3(3, 1, 5), Vector3.Zero, 1f)];
        var obj = BlueprintFormats.PieceObjects(pieces, 1f, 2f, 0.5f)[0]!.AsObject();
        Assert.Equal(2f, obj["dx"]!.GetValue<float>());
        Assert.Equal(3f, obj["dz"]!.GetValue<float>());
        Assert.Equal(0.5f, obj["dy"]!.GetValue<float>());
    }

    [Fact]
    public void AScaleOfOneIsWrittenAsZeroAndAnyOtherAsItIs()
    {
        List<BlueprintFormats.Piece> pieces = [new("a", Vector3.Zero, Vector3.Zero, 1f), new("b", Vector3.Zero, Vector3.Zero, 2f)];
        var objects = BlueprintFormats.PieceObjects(pieces, 0f, 0f, 0f);
        Assert.Equal(0f, objects[0]!["scale"]!.GetValue<float>());
        Assert.Equal(2f, objects[1]!["scale"]!.GetValue<float>());
    }

    [Fact]
    public void FieldsOfAPieceAreWrittenAsTheirTextList()
    {
        List<BlueprintFormats.Piece> pieces = [new("a", Vector3.Zero, Vector3.Zero, 1f, [new("floats", 7, "2")])];
        var obj = BlueprintFormats.PieceObjects(pieces, 0f, 0f, 0f)[0]!.AsObject();
        Assert.Equal("floats;7;Mg==", obj["data"]!.AsArray()[0]!.GetValue<string>());
    }

    [Fact]
    public void APieceWithoutFieldsHasNoDataKey()
    {
        List<BlueprintFormats.Piece> pieces = [new("a", Vector3.Zero, Vector3.Zero, 1f)];
        Assert.Null(BlueprintFormats.PieceObjects(pieces, 0f, 0f, 0f)[0]!.AsObject()["data"]);
    }

    [Fact]
    public void TheGridHasOnePointMoreThanTheSpanHasMetres()
    {
        var (w, h, rel, _, pnt) = BlueprintFormats.TerrainGrid(Terrain(Mark(2f)), 0f, 0f, 0f, 2f, 2f);
        Assert.Equal((3, 3), (w, h));
        Assert.Equal(9, rel.Length);
        Assert.Equal(36, pnt.Length);
    }

    [Fact]
    public void ACoveredPointTakesTheHeightAboveTheLowestPieceAndFullWeight()
    {
        var (_, _, rel, wt, _) = BlueprintFormats.TerrainGrid(Terrain(Mark(2f)), 0f, 0f, 0f, 2f, 2f);
        // The middle point of the 3 x 3 grid is the mark's centre; the other points are not covered.
        Assert.Equal((200, 255), (rel[4], wt[4]));
        Assert.Equal((-32768, 0), (rel[0], wt[0]));
    }

    [Fact]
    public void APaintedPointTakesTheMarksPaintAsFourWeights()
    {
        var (_, _, _, _, pnt) = BlueprintFormats.TerrainGrid(Terrain(Mark(2f, "dirt")), 0f, 0f, 0f, 2f, 2f);
        Assert.Equal(new[] { 255, 0, 0, 255 }, pnt.AsSpan(16, 4).ToArray());
    }

    [Fact]
    public void AMarkWithoutPaintLeavesThePaintUnset()
    {
        var (_, _, _, _, pnt) = BlueprintFormats.TerrainGrid(Terrain(Mark(2f)), 0f, 0f, 0f, 2f, 2f);
        Assert.Equal(-255, pnt[16]);
    }

    [Fact]
    public void ALaterMarkIsAppliedOverAnEarlierOne()
    {
        var (_, _, rel, _, _) = BlueprintFormats.TerrainGrid(Terrain(Mark(1f), Mark(3f)), 0f, 0f, 0f, 2f, 2f);
        Assert.Equal(300, rel[4]);
    }

    [Fact]
    public void AGridLargerThanTheEditorAllowsIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => BlueprintFormats.TerrainGrid(Terrain(Mark(2f)), 0f, 0f, 0f, BlueprintFormats.MaxGround + 1f, 2f));
    }
}
