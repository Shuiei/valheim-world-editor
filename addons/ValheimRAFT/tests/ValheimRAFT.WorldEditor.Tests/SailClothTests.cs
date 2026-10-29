using System.Numerics;
using Xunit;

namespace ValheimRAFT.WorldEditor.Tests;

public sealed class SailClothTests
{
    private static FakeObject Sail(int? count, params Vector3[] corners)
    {
        var sail = new FakeObject(Keys.Sail);
        if (count is int c)
        {
            sail.Ints[Keys.SailCorners] = c;
        }
        for (int i = 0; i < corners.Length; i++)
        {
            sail.Vectors[Keys.SailCornerKey(i + 1)] = corners[i];
        }
        return sail;
    }

    private static readonly Vector3[] Four = [Vector3.Zero, new(0, -3, 0), new(-4, -3, 0.5f), new(-4, 0, 0.5f)];

    [Fact]
    public void AFourCorneredSailIsDrawnAsItsClothBetweenItsCorners()
    {
        var shape = Assert.Single(new SailCloth().ShapesOf(Sail(4, Four), new FakeWorld()));

        Assert.Equal(Four, shape.Vertices);
        Assert.Equal([0, 1, 2, 0, 2, 3], shape.Triangles);
        Assert.True(shape.BothSides);
        Assert.False(shape.WorldSpace);
        Assert.Equal(1, shape.Color.W);
    }

    [Fact]
    public void AThreeCorneredSailIsOneTriangle()
    {
        var shape = Assert.Single(new SailCloth().ShapesOf(Sail(3, Four[..3]), new FakeWorld()));

        Assert.Equal([0, 1, 2], shape.Triangles);
    }

    [Fact]
    public void ASailOfAnOlderVersionKeepsFourCornersWithoutTheirCount()
    {
        Assert.Equal(Four, SailCloth.Corners(Sail(null, Four)));
        // Three corners without a count were never a sail the mod weaves.
        Assert.Null(SailCloth.Corners(Sail(null, Four[..3])));
    }

    [Fact]
    public void ASailNotMadeYetOrDamagedDrawsNothing()
    {
        Assert.Empty(new SailCloth().ShapesOf(Sail(null), new FakeWorld()));
        Assert.Empty(new SailCloth().ShapesOf(Sail(4, Four[..2]), new FakeWorld()));
        Assert.Empty(new SailCloth().ShapesOf(Sail(5, [.. Four, Vector3.One]), new FakeWorld()));
        Assert.Empty(new SailCloth().ShapesOf(Sail(3, Vector3.Zero, new Vector3(float.NaN, 0, 0), Vector3.One), new FakeWorld()));
    }

    [Fact]
    public void ASailIsTintedByItsColour()
    {
        var red = Sail(4, Four);
        red.Bytes[Keys.SailColour] = [255, 0, 0, 255];
        var plain = Sail(4, Four);

        var tinted = Assert.Single(new SailCloth().ShapesOf(red, new FakeWorld())).Color;
        var cloth = Assert.Single(new SailCloth().ShapesOf(plain, new FakeWorld())).Color;

        Assert.Equal(cloth.X, tinted.X);
        Assert.Equal(0, tinted.Y);
        Assert.Equal(0, tinted.Z);
        Assert.Equal(1, tinted.W);
    }

    [Fact]
    public void AColourIsShownInWords()
    {
        var sail = new SailCloth();
        Assert.Equal("#FF8000, 100% opaque", sail.Describe(new FakeObject(Keys.Sail), [255, 128, 0, 255]));
        Assert.Equal("#000000, 50% opaque", sail.Describe(new FakeObject(Keys.Sail), [0, 0, 0, 128]));
        Assert.Null(sail.Describe(new FakeObject(Keys.Sail), [1, 2, 3]));
        Assert.Null(SailCloth.Colour([1, 2, 3, 4, 5]));
    }
}
