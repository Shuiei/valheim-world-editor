using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The fractal noise of the brushes and natural paths.
/// </summary>
public class NoiseTests
{
    [Fact]
    public void FractalNoiseStaysWithinItsBound()
    {
        var noise = new Noise(12345);
        for (int i = 0; i < 200_000; i++)
        {
            float v = noise.Fbm(i * 0.0173f, i * 0.0291f % 300);
            Assert.InRange(v, -Noise.FbmBound, Noise.FbmBound);
        }
    }
}
