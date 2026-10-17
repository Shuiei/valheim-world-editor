using System.Numerics;
using Xunit;

using static TerrainEditor.Desktop.Tests.SharedPlaceTool;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The brush's random layout (PlaceTool.NewLayout, the R key): a new layout puts the scattered objects somewhere else,
/// while the same settings and cursor keep the layout they have.
/// </summary>
public class PlaceToolLayoutTests
{
    [Fact]
    public void ANewLayoutScattersTheObjectsSomewhereElse()
    {
        var (t, _) = Tool();
        t.Brush.Radius = 15;
        t.Density = 5;
        t.Spacing = 3;
        var before = t.Preview(new Vector2(64, 64));
        Assert.Equal(before[0].G, t.Preview(new Vector2(64, 64))[0].G);
        t.NewLayout();
        var after = t.Preview(new Vector2(64, 64));
        Assert.NotEqual(before[0].G, after[0].G);
    }
}
