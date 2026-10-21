using System.Numerics;
using System.Reflection;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The clumping test: with no clumping set every point is kept, whatever its draw.
/// </summary>
public class PlaceToolClumpTests
{
    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.9f)]
    public void WithNoClumpingEveryDrawIsKept(float draw)
    {
        var tool = new PlaceTool { Clump = 0 };
        Assert.True((bool)tool.GetType().GetMethod("ClumpKeeps", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(tool, [null, Vector2.Zero, draw])!);
    }
}
