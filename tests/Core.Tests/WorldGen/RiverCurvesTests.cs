using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A river's meander: its curve width and wavelength are shares of its length (WorldGenerator.Curves), the game's
/// fifteenth and twentieth, or the rules'.
/// </summary>
public class RiverCurvesTests
{
    /// <summary>A river 150 metres long, along the x axis from the origin.</summary>
    private static WorldGenerator.River RiverOf150Metres() => new() { P0 = new Vector2(0, 0), P1 = new Vector2(150, 0) };

    /// <summary>The game's shares: a fifteenth of the length wide, a twentieth long.</summary>
    private static void Curves(WorldGenerator.River river, double widthShare = 15.0, double lengthShare = 20.0) =>
        PrivateMember.Call(typeof(WorldGenerator), "Curves", null, river, widthShare, lengthShare);

    [Fact]
    public void ACurveWidthIsFifteenthOfTheRiverLength()
    {
        var river = RiverOf150Metres();

        Curves(river);

        Assert.Equal(10f, river.CurveWidth);
    }

    [Fact]
    public void ACurveWavelengthIsTwentiethOfTheRiverLength()
    {
        var river = RiverOf150Metres();

        Curves(river);

        Assert.Equal(7.5f, river.CurveWavelength);
    }

    [Fact]
    public void TheRulesSharesAreTakenInstead()
    {
        var river = RiverOf150Metres();

        Curves(river, widthShare: 10.0, lengthShare: 30.0);

        Assert.Equal(15f, river.CurveWidth);
        Assert.Equal(5f, river.CurveWavelength);
    }
}
