using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The world generator's small sums at their edges: a step whose two ends are the same.
/// </summary>
public class GenMathTests
{
    [Fact]
    public void AStepWithTheSameEndsIsAStepNotNaN()
    {
        // 0 / 0 gave NaN at v == lo, and NaN went on into the ground.
        Assert.Equal(1f, GenMath.Step(5f, 5f, 5f));
        Assert.Equal(0f, GenMath.Step(5f, 5f, 4f));
        Assert.Equal(1.0, GenMath.Step(5.0, 5.0, 5.0));
        Assert.Equal(1f, GenMath.Smooth(5f, 5f, 5f));
        Assert.Equal(0f, GenMath.Smooth(5f, 5f, 4f));
        // Between distinct ends, as before.
        Assert.Equal(0.5f, GenMath.Step(0f, 2f, 1f));
        Assert.Equal(0.5f, GenMath.Smooth(0f, 2f, 1f));
    }
}
