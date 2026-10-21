using System.Numerics;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The lines of a blueprint's pieces, terrain and VBuild entries, and the coordinates in them
/// (BlueprintFormats.ParseBlueprintPiece, ParseTerrain, ParseVBuild and Coord).
/// </summary>
public class BlueprintFormatsEntryParsingTests
{
    [Fact]
    public void APieceLineGivesItsPrefabNameWithoutTheCloneSuffix()
    {
        var piece = PrivateMember.Call<BlueprintFormats.Piece>(typeof(BlueprintFormats), "ParseBlueprintPiece", null, "piece_chest(Clone);0;1;2;3;0;0;0;1");

        Assert.Equal("piece_chest", piece.Name);
        Assert.Equal(new Vector3(1, 2, 3), piece.Position);
        Assert.Equal(Vector3.Zero, piece.Euler);
        Assert.Equal(1f, piece.Scale);
    }

    [Fact]
    public void APieceLineWithACommaDecimalSeparatorReadsItsNumbers()
    {
        var piece = PrivateMember.Call<BlueprintFormats.Piece>(typeof(BlueprintFormats), "ParseBlueprintPiece", null, "piece_wood;0;1,5;2,25;-3;0;0;0;1");

        Assert.Equal(new Vector3(1.5f, 2.25f, -3f), piece.Position);
    }

    [Fact]
    public void APieceLineWithThirteenFieldsTakesItsScaleFromTheEleventhField()
    {
        var piece = PrivateMember.Call<BlueprintFormats.Piece>(typeof(BlueprintFormats), "ParseBlueprintPiece", null, "piece_wood;0;0;0;0;0;0;0;1;0;2.5;0;0");

        Assert.Equal(2.5f, piece.Scale);
    }

    [Fact]
    public void ATerrainLineGivesItsShapeInLowerCaseWithItsRadiusAndRotation()
    {
        var mod = PrivateMember.Call<BlueprintFormats.TerrainMod>(typeof(BlueprintFormats), "ParseTerrain", null, "Raise;1;2;3;4;90");

        Assert.Equal("raise", mod.Shape);
        Assert.Equal(new Vector3(1, 2, 3), mod.Position);
        Assert.Equal(4f, mod.Radius);
        Assert.Equal(90, mod.Rotation);
        Assert.Equal("", mod.Paint);
    }

    [Fact]
    public void ATerrainLineTakesItsPaintFromTheEighthField()
    {
        var mod = PrivateMember.Call<BlueprintFormats.TerrainMod>(typeof(BlueprintFormats), "ParseTerrain", null, "Level;0;0;0;4;0;0;Grass");

        Assert.Equal("Grass", mod.Paint);
    }

    [Fact]
    public void ATerrainLineWithANegativeRadiusIsAFormatError()
    {
        Assert.Throws<FormatException>(() => PrivateMember.Call(typeof(BlueprintFormats), "ParseTerrain", null, "Raise;0;0;0;-1;0"));
    }

    [Fact]
    public void AVBuildLineGivesItsPrefabNameAndItsPositionAfterTheRotation()
    {
        var piece = PrivateMember.Call<BlueprintFormats.Piece>(typeof(BlueprintFormats), "ParseVBuild", null, "piece_wood(Clone) 0 0 0 1 4 5 6");

        Assert.Equal("piece_wood", piece.Name);
        Assert.Equal(new Vector3(4, 5, 6), piece.Position);
        Assert.Equal(1f, piece.Scale);
    }

    [Fact]
    public void AVBuildLineWithACommaDecimalSeparatorReadsItsNumbers()
    {
        var piece = PrivateMember.Call<BlueprintFormats.Piece>(typeof(BlueprintFormats), "ParseVBuild", null, "piece_wood 0 0 0 1 4,5 6 7");

        Assert.Equal(new Vector3(4.5f, 6f, 7f), piece.Position);
    }

    [Fact]
    public void ACoordinateAtTheWorldLimitIsRead()
    {
        Assert.Equal(100000f, PrivateMember.Call<float>(typeof(BlueprintFormats), "Coord", null, "100000"));
    }

    [Fact]
    public void ACoordinateBeyondTheWorldLimitIsAFormatError()
    {
        Assert.Throws<FormatException>(() => PrivateMember.Call(typeof(BlueprintFormats), "Coord", null, "-100001"));
    }

    [Fact]
    public void ABlankCoordinateIsZero()
    {
        Assert.Equal(0f, PrivateMember.Call<float>(typeof(BlueprintFormats), "Coord", null, "   "));
    }
}
