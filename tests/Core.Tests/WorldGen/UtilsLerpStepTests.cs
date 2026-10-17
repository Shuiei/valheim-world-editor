using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The step from one value to another: 0 at the low end, 1 at the high end, clamped beyond both (Utils.LerpStep).
/// </summary>
public class UtilsLerpStepTests
{
    [Fact]
    public void AValueHalfwayBetweenTheEndsIsHalf()
    {
        Assert.Equal(0.5f, Utils.LerpStep(0f, 10f, 5f));
    }

    [Fact]
    public void AValueBelowTheLowEndIsZero()
    {
        Assert.Equal(0f, Utils.LerpStep(0f, 10f, -5f));
    }

    [Fact]
    public void AValueAboveTheHighEndIsOne()
    {
        Assert.Equal(1f, Utils.LerpStep(0f, 10f, 20f));
    }
}
