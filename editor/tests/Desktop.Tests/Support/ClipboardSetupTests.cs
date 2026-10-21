using System.Numerics;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.ClipboardSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The copy and paste setup helpers, tested once here so the tests using them can rely on them.
/// </summary>
public sealed class ClipboardSetupTests
{
    [Fact]
    public void ASingleObjectClipHoldsOnePointTheObjectTheOutlineAndTheName()
    {
        var wall = new CopyData.Obj(StableHash.Of("woodwall"), "woodwall", -6, 0, 1, Vector3.Zero, 0, null);
        var c = SingleObjectClip(wall, [new(-8, -2), new(2, -2)], "Wall");
        Assert.Equal((1, 1), (c.W, c.H));
        Assert.Equal(wall, Assert.Single(c.Objects));
        Assert.Equal([new Vector2(-8, -2), new Vector2(2, -2)], c.Poly);
        Assert.Equal("Wall", c.Name);
        Assert.True(float.IsNaN(c.Rel[0]));
    }

    [Fact]
    public void AGroundOnlyClipHasNoObjectsAndTheSizeGiven()
    {
        var c = GroundOnlyClip(3, 2, [new(-1, -1), new(1, 1)]);
        Assert.Equal((3, 2), (c.W, c.H));
        Assert.Empty(c.Objects);
        Assert.Equal([0f], c.Rel);
        Assert.Equal([1f], c.Wt);
        Assert.Null(c.Name);
    }

    [Fact]
    public void RoundKeepsThreeDecimalsAndMakesNegativeZeroZero()
    {
        var r = Round(new Vector2(1.23456f, -0.0001f));
        Assert.Equal(1.235f, r.X, 3);
        Assert.False(float.IsNegative(r.Y));
    }
}
