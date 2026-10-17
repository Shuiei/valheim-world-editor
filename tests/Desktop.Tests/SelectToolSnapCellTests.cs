using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The metre cell a snap point is in: each of its x and z rounded down, so a point just short of zero is in the cell below.
/// </summary>
public class SelectToolSnapCellTests
{
    [Theory]
    [InlineData(1.5f, 2.9f, 1, 2)]
    [InlineData(0.9f, -0.1f, 0, -1)]
    [InlineData(-0.5f, 0f, -1, 0)]
    public void APointsCellIsItsXAndZRoundedDown(float x, float z, int cellX, int cellZ)
    {
        Assert.Equal((cellX, cellZ), SelectTool.SnapCell(new Vector3(x, 1f, z)));
    }
}
