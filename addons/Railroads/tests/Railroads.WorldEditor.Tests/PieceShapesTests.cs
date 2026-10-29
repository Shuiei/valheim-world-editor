using System.Numerics;
using Xunit;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// The mod's pieces drawn from their models: each part in its material's colour, in the piece's own frame; a vehicle's
/// wheels under each axle (the locomotive's small ones and its big driving wheels) and its couplers where its model says;
/// the old station's legs unless it was built without; the hand cart's pump handle leaning as it was left; the
/// drawbridge's leaves, chains and weights lowered or raised; nothing without the model.
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

    [Fact]
    public void AVehiclesCouplersStandOutWhereItsModelSays()
    {
        // Railroads 0.3.0 on: a coupler's box at each end of the wagon, a box of another size besides.
        _mod.Put("wagon", ModelBytes.Write([("wood", [new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 0)], [0, 1, 2])],
            given: [new(new Vector3(0, 1.14f, 1.83f), RailroadsMod.CouplerBox), new(new Vector3(0, 1.14f, -1.83f), RailroadsMod.CouplerBox)]));
        _mod.Put("coupler", ModelBytes.Triangle("iron", Vector3.Zero));

        var shapes = Shapes("bf_wagon");

        Assert.Equal(3, shapes.Count);
        // Each from its box's inner end outwards, 0.45 m: forward at the front, turned half round at the back.
        Assert.True(Vector3.Distance(new Vector3(0, 1.14f, 1.605f), shapes[1].Vertices[0]) < 1e-5f, shapes[1].Vertices[0].ToString());
        Assert.Equal(1.605f + 1, shapes[1].Vertices[1].Z, 4);
        Assert.True(Vector3.Distance(new Vector3(0, 1.14f, -1.605f), shapes[2].Vertices[0]) < 1e-5f, shapes[2].Vertices[0].ToString());
        Assert.Equal(-1.605f - 1, shapes[2].Vertices[1].Z, 4);
        Assert.All(shapes.Skip(1), s => Assert.Equal(PieceShapes.Iron, s.Color));
    }

    [Fact]
    public void AnOlderVehicleHasItsCouplersOnItsBody()
    {
        // Railroads before 0.3.0: no coupler's box, the body has them; the coupler's model is not even looked for.
        _mod.Put("wagon", ModelBytes.Triangle("wood", new Vector3(0, 1, 0)));

        Assert.Single(Shapes("bf_wagon"));
        Assert.Empty(_log);
    }

    [Fact]
    public void TheDrawbridgesLeavesStandUpWhenItIsRaised()
    {
        PutBridge();
        var raised = new FakeObject(RailroadsMod.Drawbridge);
        raised.Ints[RailroadsMod.Keys.BridgeUp] = 1;

        var down = Shapes(RailroadsMod.Drawbridge);
        var up = Shapes(RailroadsMod.Drawbridge, raised);

        // The towers, then one shape a material of the moving parts: the leaves' stone, the iron of the rest.
        Assert.Equal([PieceShapes.Wood, PieceShapes.Stone, PieceShapes.Iron], down.Select(s => s.Color));
        // A leaf's far end, 8 m along it from its hinge: in the middle of the bridge lowered, 8 m up raised.
        Assert.Equal(0.34f, down[1].Vertices.Max(v => v.Y), 4);
        Assert.Equal(8.34f, up[1].Vertices.Max(v => v.Y), 4);
        Assert.Contains(down[1].Vertices, v => Vector3.Distance(v, new Vector3(0, 0.34f, 8)) < 1e-4f);
        // 4 sheaves, 4 pinions, 4 weights and 4 chains of 58 links, a triangle each.
        Assert.Equal(3 * (4 + 4 + 4 + (4 * 58)), down[2].Triangles.Length);
    }

    [Fact]
    public void ADrawbridgeWithoutItsChainsModelIsDrawnWithout()
    {
        PutBridge(links: false);

        var shapes = Shapes(RailroadsMod.Drawbridge);

        Assert.Equal(3 * (4 + 4 + 4), shapes[2].Triangles.Length);
        Assert.Contains("track_drawbridge_link.bfm", Assert.Single(_log), StringComparison.Ordinal);
    }

    /// <summary>The drawbridge's models: its towers in wood, its leaves in stone, the rest one iron triangle each.</summary>
    private void PutBridge(bool links = true)
    {
        _mod.Put("track_drawbridge", ModelBytes.Triangle("wood", new Vector3(3, 0, 0)));
        _mod.Put(DrawbridgeParts.Leaf, ModelBytes.Write([("stone", [Vector3.Zero, new Vector3(0, 0, 8), Vector3.UnitX], [0, 1, 2])]));
        foreach (string part in DrawbridgeParts.Models.Where(m => m != DrawbridgeParts.Leaf && (links || m != DrawbridgeParts.Link)))
        {
            _mod.Put(part, ModelBytes.Triangle("iron", Vector3.Zero));
        }
    }
}
