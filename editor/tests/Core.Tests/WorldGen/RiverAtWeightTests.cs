using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// How much river lies under a point: a point at the river's centre gets full weight, its weight falls off linearly to
/// the river's edge, and a point beyond the edge, or in a cell with no river, gets none.
/// </summary>
public sealed class RiverAtWeightTests
{
    /// <summary>A generator whose only river is one point of width 20 at the origin.</summary>
    /// <returns>The generator, with its river grid holding that one point.</returns>
    private static WorldGenerator GeneratorWithOneRiverPoint()
    {
        var gen = SeedLocationsSupport.Seed();
        var cells = (Dictionary<Vector2i, WorldGenerator.RiverPoint[]>)PrivateCall.Field(gen, "_riverCells")!;
        cells.Clear();
        cells[new Vector2i(0, 0)] = [new WorldGenerator.RiverPoint(new Vector2(0f, 0f), 20f)];
        return gen;
    }

    /// <summary>Calls the river lookup at a point and returns its weight and width.</summary>
    /// <param name="gen">The generator.</param>
    /// <param name="x">The point's x.</param>
    /// <param name="z">The point's z.</param>
    private static (float Weight, float Width) RiverAt(WorldGenerator gen, float x, float z)
    {
        object?[] args = [x, z, 0f, 0f];
        PrivateMember.Call(typeof(WorldGenerator), "RiverAt", gen, args);
        return ((float)args[2]!, (float)args[3]!);
    }

    /// <summary>A point at the river's centre has full weight and the river's width.</summary>
    [Fact]
    public void ACentrePointHasFullWeightAndTheRiverWidth() =>
        Assert.Equal((1f, 20f), RiverAt(GeneratorWithOneRiverPoint(), 0f, 0f));

    /// <summary>Halfway to the edge the weight is a half, and the width is still the river's.</summary>
    [Fact]
    public void APointHalfwayToTheEdgeHasHalfWeight() =>
        Assert.Equal((0.5f, 20f), RiverAt(GeneratorWithOneRiverPoint(), 10f, 0f));

    /// <summary>A point beyond the river's width has no weight and no width.</summary>
    [Fact]
    public void APointBeyondTheRiverWidthHasNoWeight() =>
        Assert.Equal((0f, 0f), RiverAt(GeneratorWithOneRiverPoint(), 25f, 0f));

    /// <summary>A point in a cell with no river has no weight.</summary>
    [Fact]
    public void APointInACellWithNoRiverHasNoWeight() =>
        Assert.Equal((0f, 0f), RiverAt(GeneratorWithOneRiverPoint(), 500f, 500f));
}
