using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A mesh's vertex channels read as floats: full-size floats, normalised bytes, a channel that is not there, and a
/// vertex cut short by the end of the data (VertexLayout.Read).
/// </summary>
public class VertexLayoutReadTests
{
    [Fact]
    public void FloatChannelReadsEachVertexsComponents()
    {
        var layout = new GameBundles.VertexLayout([(0, 0, 0, 3)], 2, [0], [12]);
        byte[] data = [.. new float[] { 1, 2, 3, 4, 5, 6 }.SelectMany(BitConverter.GetBytes)];

        Assert.Equal(new float[] { 1, 2, 3, 4, 5, 6 }, layout.Read(data, 0, 3));
    }

    [Fact]
    public void ByteChannelIsScaledToZeroToOne()
    {
        var layout = new GameBundles.VertexLayout([(0, 0, 2, 4)], 1, [0], [4]);

        Assert.Equal(new float[] { 1, 0, 127 / 255f, 51 / 255f }, layout.Read([255, 0, 127, 51], 0, 4));
    }

    [Fact]
    public void AChannelThatIsNotThereReadsAsNothing()
    {
        var layout = new GameBundles.VertexLayout([(0, 0, 0, 3)], 1, [0], [12]);

        Assert.Null(layout.Read(new byte[12], 1, 3));
    }

    [Fact]
    public void AChannelWithNoComponentsReadsAsNothing()
    {
        var layout = new GameBundles.VertexLayout([(0, 0, 0, 0)], 1, [0], [12]);

        Assert.Null(layout.Read(new byte[12], 0, 3));
    }

    [Fact]
    public void AVertexPastTheEndOfTheDataReadsAsZeros()
    {
        var layout = new GameBundles.VertexLayout([(0, 0, 0, 3)], 2, [0], [12]);
        byte[] data = [.. new float[] { 1, 2, 3 }.SelectMany(BitConverter.GetBytes)];

        Assert.Equal(new float[] { 1, 2, 3, 0, 0, 0 }, layout.Read(data, 0, 3));
    }
}
