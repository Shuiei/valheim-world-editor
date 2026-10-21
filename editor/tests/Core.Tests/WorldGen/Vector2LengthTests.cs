using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Vector2's squared length: the sum of the squares of its two components, so a 3-4 vector gives 25 and the sign of a
/// component does not matter.
/// </summary>
public class Vector2LengthTests
{
    [Fact]
    public void TheSquaredLengthIsTheSumOfTheComponentSquares()
    {
        Assert.Equal(25f, Vector2.SqrMagnitude(new Vector2(3f, 4f)));
    }

    [Fact]
    public void ASignedComponentGivesTheSameSquaredLength()
    {
        Assert.Equal(25f, Vector2.SqrMagnitude(new Vector2(-3f, -4f)));
    }

    [Fact]
    public void TheZeroVectorHasNoLength()
    {
        Assert.Equal(0f, Vector2.SqrMagnitude(new Vector2(0f, 0f)));
    }
}
