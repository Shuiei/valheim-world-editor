using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The blend of two colours of the base terrain, at its ends and past them (BaseTerrain.Lerp).
/// </summary>
public class BaseTerrainLerpTests
{
    private static readonly Color Black = new(0f, 0f, 0f, 0f);
    private static readonly Color White = new(1f, 1f, 1f, 1f);

    [Fact]
    public void AHalfwayBlendIsTheMiddleColour()
    {
        Color mid = PrivateMember.Call<Color>(typeof(BaseTerrain), "Lerp", null, Black, White, 0.5f);

        Assert.Equal(0.5f, mid.r);
        Assert.Equal(0.5f, mid.g);
        Assert.Equal(0.5f, mid.b);
        Assert.Equal(0.5f, mid.a);
    }

    [Fact]
    public void ABlendBelowZeroIsTheFirstColour()
    {
        Assert.Equal(Black, PrivateMember.Call<Color>(typeof(BaseTerrain), "Lerp", null, Black, White, -1f));
    }

    [Fact]
    public void ABlendAboveOneIsTheSecondColour()
    {
        Assert.Equal(White, PrivateMember.Call<Color>(typeof(BaseTerrain), "Lerp", null, Black, White, 2f));
    }
}
