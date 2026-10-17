using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A picture pixel's colour (BuildingPicture.SetColour): red, green and blue are each clamped to a byte and truncated, and
/// the pixel's alpha is left as it is.
/// </summary>
public class BuildingPictureColourTests
{
    [Fact]
    public void AColourOutsideTheByteRangeIsClampedToIt()
    {
        byte[] rgba = [0, 0, 0, 77];

        BuildingPicture.SetColour(rgba, 0, new Vector3(300f, -5f, 128.7f));

        Assert.Equal(new byte[] { 255, 0, 128, 77 }, rgba);
    }

    [Fact]
    public void AColourInsideTheByteRangeIsSetAtItsPixelOnly()
    {
        byte[] rgba = new byte[8];

        BuildingPicture.SetColour(rgba, 1, new Vector3(10f, 20f, 30f));

        Assert.Equal(new byte[] { 0, 0, 0, 0, 10, 20, 30, 0 }, rgba);
    }
}
