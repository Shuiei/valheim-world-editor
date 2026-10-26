using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The linear blend inside the noise (UnityPerlin.Lerp): it returns a at t = 0, b at t = 1, and does not clamp t.
/// </summary>
public class UnityPerlinBlendTests
{
    private static float Lerp(float t, float a, float b) =>
        (float)PrivateCall.Static(typeof(UnityPerlin), "Lerp", t, a, b)!;

    [Fact]
    public void BlendIsTheStartAtZeroAndTheEndAtOne()
    {
        Assert.Equal(2f, Lerp(0f, 2f, 6f));
        Assert.Equal(6f, Lerp(1f, 2f, 6f));
    }

    [Fact]
    public void BlendAtAHalfIsTheMidpoint()
    {
        Assert.Equal(4f, Lerp(0.5f, 2f, 6f));
    }

    [Fact]
    public void BlendExtrapolatesBeyondTheEndsWithoutClamping()
    {
        Assert.Equal(10f, Lerp(2f, 2f, 6f));
        Assert.Equal(-2f, Lerp(-1f, 2f, 6f));
    }
}
