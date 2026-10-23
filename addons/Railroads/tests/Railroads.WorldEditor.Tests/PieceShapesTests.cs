using System.Numerics;
using Xunit;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// The mod's pieces drawn from their models: each part in its material's colour, in the piece's own frame; a vehicle's
/// wheels under each axle (the locomotive's small ones and its big driving wheels); the old station's legs unless it was
/// built without; the hand cart's pump handle leaning as it was left; nothing without the model.
/// </summary>
public sealed class PieceShapesTests : IDisposable
{
    private readonly ModFolder _mod = new();
    private readonly List<string> _log = [];
    private readonly ModelLibrary _models;

    public PieceShapesTests()
    {
        _models = new ModelLibrary(new FakeHost { Plugin = _mod.Plugin }, _log.Add);
    }

    public void Dispose() => _mod.Dispose();

    private static RailPiece Piece(string prefab) => RailroadsMod.Pieces.Single(p => p.Prefab == prefab);

    private List<ValheimWorldEditor.Sdk.Shape> Shapes(string prefab, FakeObject? obj = null) =>
        [.. new PieceShapes(Piece(prefab), _models).ShapesOf(obj ?? new FakeObject(prefab), new NoWorld())];

    [Fact]
    public void TrackIsDrawnInItsMaterialsColours()
    {
        _mod.Put("track_straight_8", ModelBytes.Write([
            ("wood", [Vector3.Zero, Vector3.UnitZ, Vector3.UnitX], [0, 1, 2]),
            ("stone", [Vector3.UnitY, new Vector3(0, 1, 1), new Vector3(1, 1, 0)], [0, 1, 2]),
        ]));

        var shapes = Shapes("bf_track_straight_8");

        Assert.Equal([PieceShapes.Wood, PieceShapes.Stone], shapes.Select(s => s.Color));
        Assert.Equal([Vector3.UnitY, new Vector3(0, 1, 1), new Vector3(1, 1, 0)], shapes[1].Vertices);
        Assert.All(shapes, s => Assert.False(s.WorldSpace || s.BothSides));
        Assert.Equal(PieceShapes.Iron, PieceShapes.ColourOf("iron"));
        Assert.Empty(_log);
    }

    [Fact]
    public void WithoutItsModelAPieceHasNoShape()
    {
        Assert.Empty(Shapes("bf_track_loop"));
        Assert.Contains("track_loop.bfm", Assert.Single(_log), StringComparison.Ordinal);
    }

    [Fact]
    public void AWagonStandsOnAWheelSetUnderEachAxle()
    {
        _mod.Put("wagon", ModelBytes.Triangle("wood", new Vector3(0, 1, 0), axles: [new Vector3(0, 0.4f, -1.5f), new Vector3(0, 0.4f, 1.5f)]));
        _mod.Put("axle", ModelBytes.Triangle("iron", Vector3.Zero));

        var shapes = Shapes("bf_wagon");

        Assert.Equal(3, shapes.Count);
        Assert.Equal(new Vector3(0, 0.4f, -1.5f), shapes[1].Vertices[0]);
        Assert.Equal(new Vector3(0, 0.4f, 1.5f), shapes[2].Vertices[0]);
        Assert.Equal(PieceShapes.Iron, shapes[2].Color);
    }

    [Fact]
    public void TheLocomotiveHasSmallWheelsAndItsDrivingWheels()
    {
        _mod.Put("locomotive", ModelBytes.Triangle("iron", new Vector3(0, 2, 0), axles: [new Vector3(0, 0.4f, 2)]));
        _mod.Put("axle", ModelBytes.Triangle("wood", Vector3.Zero));
        _mod.Put("axle_small", ModelBytes.Triangle("stone", Vector3.Zero));
        _mod.Put("axle_big", ModelBytes.Triangle("iron", Vector3.Zero));

        var shapes = Shapes(RailroadsMod.Locomotive);

        Assert.Equal(3, shapes.Count);
        Assert.Equal((new Vector3(0, 0.4f, 2), PieceShapes.Stone), (shapes[1].Vertices[0], shapes[1].Color));
        Assert.Equal(RailroadsMod.BigAxleAt, shapes[2].Vertices[0]);
    }

    [Fact]
    public void TheOldStationHasItsLegsUnlessBuiltWithout()
    {
        _mod.Put("station", ModelBytes.Triangle("wood", new Vector3(2, 1, 0)));
        _mod.Put("station_legs", ModelBytes.Triangle("wood", new Vector3(2, 0, 0)));
        var bare = new FakeObject(RailroadsMod.OldStation);
        bare.Ints[RailroadsMod.Keys.Bare] = 1;

        Assert.Equal(2, Shapes(RailroadsMod.OldStation).Count);
        Assert.Single(Shapes(RailroadsMod.OldStation, bare));
    }

    [Fact]
    public void TheHandCartsPumpHandleLeansAsLeft()
    {
        _mod.Put("handcart", ModelBytes.Triangle("wood", new Vector3(0, 1, 0)));
        _mod.Put("handcart_beam", ModelBytes.Write([("wood", [Vector3.Zero, new Vector3(0, 0, 1), Vector3.UnitX], [0, 1, 2])]));
        var leaning = new FakeObject(RailroadsMod.Handcart);
        leaning.Floats[RailroadsMod.Keys.Beam] = 7.5f;

        var level = Shapes(RailroadsMod.Handcart)[1].Vertices;
        var leant = Shapes(RailroadsMod.Handcart, leaning)[1].Vertices;

        Assert.Equal(RailroadsMod.BeamAt + Vector3.UnitZ, level[1]);
        // 7.5 × 12° = 90° about x: the handle's end turned from forward to down (Unity's turn, as the game's).
        Assert.Equal(RailroadsMod.BeamAt.Y - 1, leant[1].Y, 4);
        Assert.Equal(RailroadsMod.BeamAt.Z, leant[1].Z, 4);
    }
}
