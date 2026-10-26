using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Whether two mesh bounds are the same: each of the six numbers agrees within one hundredth of the mesh's largest
/// size, and at least 0.01 m, so a small mesh still needs its numbers to agree closely.
/// </summary>
public sealed class BundleModelsSameBoundsTests
{
    /// <summary>Calls the bounds comparison.</summary>
    private static bool SameBounds(float[] a, float[] b) =>
        (bool)PrivateMethods.Call(typeof(BundleModels), "SameBounds", null, a, b)!;

    /// <summary>Identical bounds are the same.</summary>
    [Fact]
    public void IdenticalBoundsAreTheSame() =>
        Assert.True(SameBounds([0, 0, 0, 100, 100, 100], [0, 0, 0, 100, 100, 100]));

    /// <summary>A difference of exactly the tolerance (1 m for a 100 m mesh) is still the same.</summary>
    [Fact]
    public void ADifferenceOfExactlyTheToleranceIsTheSame() =>
        Assert.True(SameBounds([1, 0, 0, 100, 100, 100], [0, 0, 0, 100, 100, 100]));

    /// <summary>A difference over the tolerance is not the same.</summary>
    [Fact]
    public void ADifferenceOverTheToleranceIsNotTheSame() =>
        Assert.False(SameBounds([1.5f, 0, 0, 100, 100, 100], [0, 0, 0, 100, 100, 100]));

    /// <summary>A small mesh has a tolerance of 0.01 m, so 0.02 m off is not the same.</summary>
    [Fact]
    public void ASmallMeshHasAHundredthOfAMetreTolerance() =>
        Assert.False(SameBounds([0, 0.02f, 0, 1, 1, 1], [0, 0, 0, 1, 1, 1]));

}
