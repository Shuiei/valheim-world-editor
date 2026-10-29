using System.Numerics;
using Xunit;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// The mod's model files read: each part's slot, corners and triangles, the boxes' centres and sizes, the track's lines
/// and the axles, the rest read past; anything else (another version, a file ending early or running on, a triangle naming a corner its part lacks)
/// refused with why.
/// </summary>
public class ModelFileTests
{
    private static readonly Vector3[] Square = [new(0, 0, 0), new(0, 0, 1), new(1, 0, 1), new(1, 0, 0)];

    [Fact]
    public void ItsPartsLinesAndAxlesAreRead()
    {
        byte[] bytes = ModelBytes.Write(
            [("wood", Square, [0, 1, 2, 0, 2, 3]), ("stone", [new(0, 1, 0), new(0, 1, 1), new(1, 1, 0)], [0, 1, 2])],
            lines: [[Vector3.Zero, new Vector3(0, 0, 4), new Vector3(0, 0, 8)]],
            axles: [new Vector3(0, 0.4f, -1), new Vector3(0, 0.4f, 1)]);

        var model = ModelFile.Read(bytes);

        Assert.Equal(["wood", "stone"], model.Parts.Select(p => p.Slot));
        Assert.Equal(Square, model.Parts[0].Positions);
        Assert.Equal([0, 1, 2, 0, 2, 3], model.Parts[0].Triangles);
        Assert.Equal([0, 1, 2], model.Parts[1].Triangles);
        Assert.Equal(new Vector3(0, 0, 8), Assert.Single(model.Lines)[2]);
        Assert.Equal([new Vector3(0, 0.4f, -1), new Vector3(0, 0.4f, 1)], model.Axles);
    }

    [Fact]
    public void ItsBoxesAreReadByCentreAndSize()
    {
        var coupler = new RailModel.Box(new Vector3(0, 1.14f, 1.83f), RailroadsMod.CouplerBox);

        var model = ModelFile.Read(ModelBytes.Write([("wood", Square, [0, 1, 2])], boxes: 2, given: [coupler]));

        Assert.Equal(3, model.Boxes.Count);
        Assert.Equal(coupler, model.Boxes[0]);
        Assert.Equal(new RailModel.Box(new Vector3(0.5f), new Vector3(0.5f)), model.Boxes[2]);
    }

    [Fact]
    public void AModelWithoutTrackOrWheelsHasNone()
    {
        var model = ModelFile.Read(ModelBytes.Write([("iron", Square, [0, 1, 2])], boxes: 0, snaps: 0));

        Assert.Empty(model.Boxes);
        Assert.Empty(model.Lines);
        Assert.Empty(model.Axles);
    }

    [Fact]
    public void WhatIsNoModelFileIsRefused()
    {
        byte[] good = ModelBytes.Write([("wood", Square, [0, 1, 2])]);
        byte[] otherVersion = [.. good];
        otherVersion[3] = (byte)'4';

        Assert.Contains("BFM5", Assert.Throws<InvalidDataException>(() => ModelFile.Read(otherVersion)).Message, StringComparison.Ordinal);
        Assert.Contains("early", Assert.Throws<InvalidDataException>(() => ModelFile.Read(good[..^5])).Message, StringComparison.Ordinal);
        Assert.Contains("past its end", Assert.Throws<InvalidDataException>(() => ModelFile.Read([.. good, 0])).Message, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => ModelFile.Read([]));
    }

    [Fact]
    public void ATriangleOutsideItsPartIsRefused()
    {
        Assert.Contains("corner 4 of 4", Assert.Throws<InvalidDataException>(() => ModelFile.Read(ModelBytes.Write([("wood", Square, [0, 1, 4])]))).Message,
            StringComparison.Ordinal);
        Assert.Contains("not whole triangles", Assert.Throws<InvalidDataException>(() => ModelFile.Read(ModelBytes.Write([("wood", Square, [0, 1])]))).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ADamagedCountIsRefused()
    {
        byte[] bytes = ModelBytes.Write([("wood", Square, [0, 1, 2])]);
        // The part count, right after "BFM5".
        BitConverter.GetBytes(-1).CopyTo(bytes, 4);

        Assert.Contains("-1 parts", Assert.Throws<InvalidDataException>(() => ModelFile.Read(bytes)).Message, StringComparison.Ordinal);
    }
}
