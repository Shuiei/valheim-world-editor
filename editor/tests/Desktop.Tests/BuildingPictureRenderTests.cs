using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// BuildingPicture's render: a picture is drawn at twice the requested size and resized to the requested size as a PNG, even
/// when there is nothing to draw.
/// </summary>
public class BuildingPictureRenderTests
{
    [Fact]
    public void AnEmptyBuildingIsAPngOfTheRequestedSize()
    {
        byte[] png = PrivateInvoke.Call<byte[]>(typeof(BuildingPicture), "Render", null, new List<BuildingPicture.Tri>(), 4);

        var image = Png.Read(png);
        Assert.Equal(4, image.Width);
        Assert.Equal(4, image.Height);
    }
}
