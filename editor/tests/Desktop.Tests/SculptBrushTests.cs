using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The brush shapes and falloffs, the noise brush's reach, and the noise values the web editor gives.
/// </summary>
[Collection("World files")]
public class SculptBrushTests
{
    [Fact]
    public void BrushShapesAndFalloffs()
    {
        var b = new Brush { Radius = 10 };
        Assert.Equal(1, b.Weight(0, 0), 4);
        Assert.Equal(0, b.Weight(10, 0), 4);
        Assert.Equal(0.5f, b.Weight(5, 0), 4);
        b.Falloff = Falloff.Sharp;
        Assert.Equal(1, b.Weight(9.9f, 0), 4);
        b.Shape = BrushShape.Square;
        Assert.Equal(1, b.Weight(9, 9), 4);
        b.Turn = 45;
        Assert.Equal(0, b.Weight(9, 9), 4);
        Assert.Equal(1, b.Weight(13, 0), 4);
        Assert.True(b.Reach > 14);
        b.Shape = BrushShape.Ring;
        Assert.Equal(0, b.Weight(0, 0), 4);
        Assert.Equal(1, b.Weight(7, 0), 4);
        Assert.NotNull(b.InnerOutline(16));
    }

    [Fact]
    public void TheNoiseShapeChangesNothingPastItsReach()
    {
        var b = new Brush { Radius = 10, Shape = BrushShape.Noise, Noise = new Noise(7) };
        for (int c = 0; c < 60; c++)
        {
            float cx = c * 37.3f, cz = c * 11.9f;
            for (int dz = -20; dz <= 20; dz++)
            {
                for (int dx = -20; dx <= 20; dx++)
                {
                    if (MathF.Sqrt(dx * dx + dz * dz) > b.Reach)
                    {
                        Assert.Equal(0, b.Weight(dx, dz, cx + dx, cz + dz));
                    }
                }
            }
        }
    }

    [Fact]
    public void TheNoiseIsTheWebEditors()
    {
        // Values from the web editor's reseed(12345) and fbm (editor.html).
        var n = new Noise(12345);
        Assert.Equal(0.04665f, n.Fbm(3.3f, 7.7f), 4);
        Assert.Equal(0.29457f, n.Fbm(-12.25f, 40.5f), 4);
        Assert.Equal(-0.07274f, n.Fbm(100.1f, -3.9f), 4);
    }
}
