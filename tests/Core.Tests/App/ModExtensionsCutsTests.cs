using System.Numerics;
using TerrainEditor.App;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The ground cuts an extension makes (ModExtensions.ValidCut): an outline is usable when it has at least three points and
/// every point is a finite number.
/// </summary>
public class ModExtensionsCutsTests
{
    /// <summary>Whether a cut of this outline is valid.</summary>
    private static bool Valid(params Vector2[] outline) =>
        (bool)PrivateCall.Static(typeof(ModExtensions), "ValidCut", new GroundCut(outline))!;

    [Fact]
    public void AnOutlineOfThreeFinitePointsIsValid()
    {
        Assert.True(Valid(new(0, 0), new(1, 0), new(0, 1)));
    }

    [Fact]
    public void AnOutlineOfTwoPointsIsNotValid()
    {
        Assert.False(Valid(new(0, 0), new(1, 0)));
    }

    [Fact]
    public void AnOutlineWithANotANumberPointIsNotValid()
    {
        Assert.False(Valid(new(0, 0), new(1, 0), new(float.NaN, 1)));
    }
}
