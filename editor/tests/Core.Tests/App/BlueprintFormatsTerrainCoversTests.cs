using System.Numerics;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Whether a blueprint's terrain entry covers a ground point: a circle of its radius, or a square of its radius turned by
/// its rotation.
/// </summary>
public class BlueprintFormatsTerrainCoversTests
{
    /// <summary>Calls BlueprintFormats' covers test on an entry and a ground point.</summary>
    private static bool Covers(BlueprintFormats.TerrainMod entry, float x, float z) =>
        PrivateMember.Call<bool>(typeof(BlueprintFormats), "Covers", null, entry, x, z);

    [Fact]
    public void APointExactlyOnACircleOfItsRadiusIsCovered()
    {
        var circle = new BlueprintFormats.TerrainMod("circle", Vector3.Zero, 5f, 0, "");

        Assert.True(Covers(circle, 3f, 4f));
    }

    [Fact]
    public void APointJustOutsideACircleOfItsRadiusIsNotCovered()
    {
        var circle = new BlueprintFormats.TerrainMod("circle", Vector3.Zero, 5f, 0, "");

        Assert.False(Covers(circle, 3f, 4.1f));
    }

    [Fact]
    public void AnUnrotatedSquareCoversItsCornerButNotAPointPastItsEdge()
    {
        var square = new BlueprintFormats.TerrainMod("square", Vector3.Zero, 2f, 0, "");

        Assert.True(Covers(square, 2f, 2f));
        Assert.False(Covers(square, 2.1f, 0f));
    }

    [Fact]
    public void ASquareTurnedFortyFiveDegreesReachesFurtherOnItsAxes()
    {
        // Turned 45 degrees, the square's corners lie on the axes at 2 * sqrt(2), about 2.83 m out.
        var square = new BlueprintFormats.TerrainMod("square", Vector3.Zero, 2f, 45, "");

        Assert.True(Covers(square, 2.5f, 0f));
        Assert.False(Covers(square, 2.9f, 0f));
    }

    [Fact]
    public void ASquareTakesTheEntryPositionAsItsCentre()
    {
        var square = new BlueprintFormats.TerrainMod("square", new Vector3(10f, 0f, -4f), 1f, 0, "");

        Assert.True(Covers(square, 10.5f, -4.5f));
        Assert.False(Covers(square, 0f, 0f));
    }
}
