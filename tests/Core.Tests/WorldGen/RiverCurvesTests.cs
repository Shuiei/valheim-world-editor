using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A river's meander: its curve width and wavelength are fixed fractions of its length (WorldGenerator.Curves).
/// </summary>
public class RiverCurvesTests
{
    /// <summary>A river 150 metres long, along the x axis from the origin.</summary>
    private static WorldGenerator.River RiverOf150Metres() => new() { P0 = new Vector2(0, 0), P1 = new Vector2(150, 0) };

    [Fact]
    public void ACurveWidthIsFifteenthOfTheRiverLength()
    {
        var river = RiverOf150Metres();

        PrivateMember.Call(typeof(WorldGenerator), "Curves", null, river);

        Assert.Equal(10f, river.CurveWidth);
    }

    [Fact]
    public void ACurveWavelengthIsTwentiethOfTheRiverLength()
    {
        var river = RiverOf150Metres();

        PrivateMember.Call(typeof(WorldGenerator), "Curves", null, river);

        Assert.Equal(7.5f, river.CurveWavelength);
    }
}
