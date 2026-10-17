using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The improved-noise gradient (UnityPerlin.Grad): the low four bits of a hash pick a sign and an axis pair, so the
/// result is the dot product of (x, y) with one of the gradients the code defines.
/// </summary>
public class UnityPerlinGradientTests
{
    private static float Grad(int hash, float x, float y) =>
        (float)PrivateCall.Static(typeof(UnityPerlin), "Grad", hash, x, y)!;

    [Theory]
    [InlineData(0, 5f)]
    [InlineData(1, 1f)]
    [InlineData(2, -1f)]
    [InlineData(3, -5f)]
    public void LowHashesPickTheSignedXAndYGradients(int hash, float expected)
    {
        Assert.Equal(expected, Grad(hash, 2f, 3f));
    }

    [Fact]
    public void HashFourReadsXAlone()
    {
        Assert.Equal(2f, Grad(4, 2f, 3f));
    }

    [Fact]
    public void HashesEightAndUpReadYFirst()
    {
        Assert.Equal(3f, Grad(8, 2f, 3f));
        Assert.Equal(5f, Grad(12, 2f, 3f));
        Assert.Equal(1f, Grad(14, 2f, 3f));
    }

    [Fact]
    public void OnlyTheLowFourBitsOfTheHashCount()
    {
        Assert.Equal(Grad(0, 2f, 3f), Grad(16, 2f, 3f));
        Assert.Equal(Grad(3, 2f, 3f), Grad(19, 2f, 3f));
    }
}
