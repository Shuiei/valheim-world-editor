using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// An oriented box: its eight corners are its middle moved by its half sizes along its three axes, and its radius is the
/// length of its half sizes (the sphere round it from its middle).
/// </summary>
public sealed class StabilityObbTests
{
    private static readonly float[] Expected3 = [0f, 2f];
    private static readonly float[] Expected2 = [0f, 4f];
    private static readonly float[] Expected1 = [0f, 6f];
    /// <summary>A box centred at (1, 2, 3) on the world axes, with half sizes 1, 2 and 3.</summary>
    private static readonly Stability.Obb Box = new(new Vector3(1, 2, 3), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(1, 2, 3));

    /// <summary>A box has eight corners, all different.</summary>
    [Fact]
    public void ABoxHasEightDifferentCorners() =>
        Assert.Equal(8, Box.Corners().Distinct().Count());

    /// <summary>Each corner is the middle plus or minus each half size on its axis.</summary>
    [Fact]
    public void EachCornerIsAMiddlePlusOrMinusEachHalfSize()
    {
        foreach (var c in Box.Corners())
        {
            Assert.Contains(c.X, Expected3);
            Assert.Contains(c.Y, Expected2);
            Assert.Contains(c.Z, Expected1);
        }
    }

    /// <summary>The radius is the length of the half sizes.</summary>
    [Fact]
    public void TheRadiusIsTheLengthOfTheHalfSizes() =>
        Assert.Equal(MathF.Sqrt(14f), Box.Radius, 4);
}
