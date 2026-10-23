using System.Numerics;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace ValheimRAFT.WorldEditor.Tests;

public sealed class RopeLinesTests
{
    private static FakeObject Holding(string prefab, Vector3 at, int id, Quaternion? turn = null)
    {
        var o = new FakeObject(prefab, at, turn);
        o.Ints[Keys.Id] = id;
        return o;
    }

    private static Vector3 Start(Shape shape) => (shape.Vertices[0] + shape.Vertices[1]) / 2;

    private static Vector3 End(Shape shape) => (shape.Vertices[2] + shape.Vertices[3]) / 2;

    private static void Near(Vector3 expected, Vector3 actual) => Assert.True(Vector3.Distance(expected, actual) < 1e-4f, $"{actual} is not {expected}");

    [Fact]
    public void ARopeRunsFromTheAnchorsTiePointToWhatItNames()
    {
        var turn = Quaternion.CreateFromYawPitchRoll(0.7f, 0.3f, 0);
        var anchor = Holding(Keys.RopeAnchor, new Vector3(10, 31, 5), 1, turn);
        anchor.Bytes[Keys.Ropes] = RopeList.Write([new Rope(2, 0), new Rope(99, 0)]);
        var chest = Holding("piece_chest_wood", new Vector3(14, 31, 8), 2);

        // The rope to 99 is not drawn: nothing in the area holds that id.
        var shape = Assert.Single(new RopeLines().ShapesOf(anchor, new FakeWorld(anchor, chest)));

        Assert.True(shape.WorldSpace);
        Assert.True(shape.BothSides);
        Near(anchor.Position + Vector3.Transform(RopeLines.AnchorTie, turn), Start(shape));
        Near(chest.Position, End(shape));
        Assert.Equal(12, shape.Triangles.Length);
    }

    [Fact]
    public void ARopeIsTiedToAnotherAnchorWhereItStandsAndToTheDockAtItsTiePoint()
    {
        var turn = Quaternion.CreateFromYawPitchRoll(1.2f, 0, 0);
        var a = Holding(Keys.RopeAnchor, new Vector3(0, 30, 0), 1);
        var b = Holding(Keys.RopeAnchor, new Vector3(0, 30, 6), 2, turn);
        var dock = Holding(Keys.DockAnchor, new Vector3(8, 31, 0), 3, turn);
        a.Bytes[Keys.Ropes] = RopeList.Write([new Rope(2, 0), new Rope(3, 0)]);

        var shapes = new RopeLines().ShapesOf(a, new FakeWorld(a, b, dock)).ToList();

        Assert.Equal(2, shapes.Count);
        Near(b.Position, End(shapes[0]));
        Near(dock.Position + Vector3.Transform(RopeLines.DockTie, turn), End(shapes[1]));
    }

    [Fact]
    public void ARopeToAVanillaShipOrAnAnimalEndsAtTheTiePointTheModPicks()
    {
        var turn = Quaternion.CreateFromYawPitchRoll(0.5f, 0, 0);
        var anchor = Holding(Keys.RopeAnchor, new Vector3(0, 30, 0), 1);
        var karve = Holding("Karve", new Vector3(20, 30, 0), 2, turn);
        var boar = Holding("Boar", new Vector3(0, 30, 15), 3);
        anchor.Bytes[Keys.Ropes] = RopeList.Write([new Rope(2, 2), new Rope(3, 0)]);

        var shapes = new RopeLines().ShapesOf(anchor, new FakeWorld(anchor, karve, boar)).ToList();

        // The Karve's third tie point (its back left rope), the boar's neck.
        Near(karve.Position + Vector3.Transform(new Vector3(-0.95f, 0.805f, -2.452f), turn), End(shapes[0]));
        Near(boar.Position + new Vector3(0, 0.67175f, 0.41237f), End(shapes[1]));
        // A tie point the prefab does not have: where it stands.
        Assert.Equal(Vector3.Zero, RopeLines.TieOn("Karve", 9));
        Assert.Equal(Vector3.Zero, RopeLines.TieOn("piece_chest_wood", 0));
    }

    [Fact]
    public void TheDocksRopesStartAtItsTiePoint()
    {
        var dock = Holding(Keys.DockAnchor, new Vector3(8, 31, 0), 3);
        var chest = Holding("piece_chest_wood", new Vector3(0, 31, 0), 2);
        dock.Bytes[Keys.Ropes] = RopeList.Write([new Rope(2, 0)]);

        Near(dock.Position + RopeLines.DockTie, Start(Assert.Single(new RopeLines().ShapesOf(dock, new FakeWorld(dock, chest)))));
    }

    [Fact]
    public void AnAnchorWithoutRopesOrWithRopesKeptTheOldWayDrawsNothing()
    {
        var anchor = Holding(Keys.RopeAnchor, Vector3.Zero, 1);
        Assert.Empty(new RopeLines().ShapesOf(anchor, new FakeWorld(anchor)));
        anchor.Bytes[Keys.Ropes] = [1, .. new byte[13]];
        Assert.Empty(new RopeLines().ShapesOf(anchor, new FakeWorld(anchor)));
    }

    [Fact]
    public void ARopeStraightDownIsStillARope()
    {
        var shape = RopeLines.Line(new Vector3(0, 10, 0), Vector3.Zero)!;
        Assert.All(shape.Vertices, v => Assert.False(float.IsNaN(v.X + v.Y + v.Z)));
        Assert.Null(RopeLines.Line(Vector3.One, Vector3.One));
    }
}
