using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The procedural stamps' noise and rendering: smooth value noise and its four-octave sum, and the fill of a stamp
/// from a function (clamped to 0..1, north up).
/// </summary>
public class StampsProceduralTests
{
    /// <summary>Calls the stamps' value noise at a point.</summary>
    private static double VNoise(double x, double y) => PrivateMember.Call<double>(typeof(Stamps), "VNoise", null, x, y);

    /// <summary>Calls the stamps' fractal noise at a point.</summary>
    private static double Fractal(double x, double y) => PrivateMember.Call<double>(typeof(Stamps), "Fractal", null, x, y);

    /// <summary>Calls the stamps' renderer with a function of (u, v).</summary>
    private static float[] Render(Func<double, double, double> f) => PrivateMember.Call<float[]>(typeof(Stamps), "Render", null, f);

    [Fact]
    public void ValueNoiseAtTheOriginIsZero()
    {
        Assert.Equal(0.0, VNoise(0, 0));
    }

    [Fact]
    public void ValueNoiseStaysBetweenZeroAndOne()
    {
        foreach ((double x, double y) in new[] { (0.3, 0.7), (5.25, -2.5), (-13.9, 40.1) })
        {
            double v = VNoise(x, y);

            Assert.InRange(v, 0.0, 1.0);
        }
    }

    [Fact]
    public void FractalNoiseAtTheOriginIsZero()
    {
        Assert.Equal(0.0, Fractal(0, 0));
    }

    [Fact]
    public void ARenderedStampTakesTheFunctionsValueEverywhere()
    {
        float[] data = Render((u, v) => 0.5);

        Assert.All(data, value => Assert.Equal(0.5f, value));
    }

    [Fact]
    public void ARenderedStampClampsValuesToZeroAndOne()
    {
        Assert.All(Render((u, v) => 2.0), value => Assert.Equal(1f, value));
        Assert.All(Render((u, v) => -1.0), value => Assert.Equal(0f, value));
    }

    [Fact]
    public void ARenderedStampHasItsNorthEdgeAtTheTopRow()
    {
        float[] data = Render((u, v) => v);
        int size = Stamps.Size;

        Assert.All(data.Take(size), value => Assert.Equal(1f, value));
        Assert.All(data.Skip(size * (size - 1)), value => Assert.Equal(0f, value));
    }
}
