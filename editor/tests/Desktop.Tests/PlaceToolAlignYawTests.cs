using System.Reflection;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Turning a piece to line up with a reference yaw: the reference plus the quarter turns nearest the placement's own
/// rotation.
/// </summary>
public class PlaceToolAlignYawTests
{
    [Fact]
    public void WithNoRotationTheReferenceYawIsKept()
    {
        var tool = new PlaceTool { Rotation = 0 };
        Assert.Equal(30f, (float)tool.GetType().GetMethod("AlignYaw", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(tool, [30f])!, 3);
    }

    [Fact]
    public void ARotationOfAbout100DegreesAddsOneQuarterTurn()
    {
        var tool = new PlaceTool { Rotation = 100 };
        Assert.Equal(120f, (float)tool.GetType().GetMethod("AlignYaw", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(tool, [30f])!, 3);
    }

    [Fact]
    public void ANegativeRotationOfAbout100DegreesTakesOneQuarterTurnBack()
    {
        var tool = new PlaceTool { Rotation = -100 };
        Assert.Equal(-60f, (float)tool.GetType().GetMethod("AlignYaw", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(tool, [30f])!, 3);
    }
}
