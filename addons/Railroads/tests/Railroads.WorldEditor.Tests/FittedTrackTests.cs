using System.Numerics;
using Xunit;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// The fitted track's line and rails (FittedTrack): the curve from its place to its far end, leaving and arriving the
/// ways its values say, laid out in equal steps of at most a quarter metre and ending on the far end exactly; forward
/// when a way is not said, none without a far end; the 1 m straight bent along it a whole number of times, where the
/// line is whatever the piece's own turn.
/// </summary>
public sealed class FittedTrackTests : IDisposable
{
    private readonly ModFolder _mod = new();
    private readonly ModelLibrary _models;

    public FittedTrackTests()
    {
        // The 1 m straight: a wood sleeper across, a stone rail along.
        _mod.Put(FittedTrack.RailModelName, ModelBytes.Write([
            ("wood", [new Vector3(-1, 0, 0.5f), new Vector3(1, 0, 0.5f), new Vector3(0, 0.2f, 0.5f)], [0, 1, 2]),
            ("stone", [new Vector3(0.7f, 0.3f, 0), new Vector3(0.7f, 0.3f, 1), new Vector3(0.7f, 0.4f, 0)], [0, 1, 2]),
        ], lines: [[Vector3.Zero, Vector3.UnitZ]]));
        _models = new ModelLibrary(new FakeHost { Plugin = _mod.Plugin }, Assert.Fail);
    }

    public void Dispose() => _mod.Dispose();

    /// <summary>The steps of a line, each one's length.</summary>
    private static List<float> Steps(Vector3[] line) => [.. line.Zip(line.Skip(1), Vector3.Distance)];

    [Fact]
    public void AStraightFitIsLaidOutInQuarterMetres()
    {
        var line = FittedTrack.Line(new Vector3(0, 0, 8), Vector3.UnitZ, Vector3.UnitZ);

        Assert.Equal(33, line.Length);
        Assert.Equal(Vector3.Zero, line[0]);
        Assert.Equal(new Vector3(0, 0, 8), line[^1]);
        Assert.All(line, p => Assert.Equal(0, p.X, 4));
        Assert.All(Steps(line), s => Assert.Equal(0.25f, s, 3));
    }

    [Fact]
    public void ACurvedFitLeavesAndArrivesTheWaysItSays()
    {
        var end = new Vector3(6, 1, 9);

        var line = FittedTrack.Line(end, Vector3.UnitZ, Vector3.UnitX);

        Assert.Equal(end, line[^1]);
        var first = Vector3.Normalize(line[1] - line[0]);
        var last = Vector3.Normalize(line[^1] - line[^2]);
        Assert.True(Vector3.Dot(first, Vector3.UnitZ) > 0.99f, $"leaves along {first}");
        Assert.True(Vector3.Dot(last, Vector3.UnitX) > 0.99f, $"arrives along {last}");
        var steps = Steps(line);
        Assert.All(steps, s => Assert.True(s <= 0.25f + 1e-3f, $"a step of {s}"));
        // Equal steps along the curve (the last as the rest, to the far end exactly).
        Assert.True(steps.Max() - steps.Min() < 0.01f);
    }

    [Fact]
    public void ThePiecesValuesGiveItsLine()
    {
        var fit = new FakeObject(RailroadsMod.FittedTrack);
        Assert.Null(FittedTrack.LineOf(fit));

        fit.Vectors[RailroadsMod.Keys.FitEnd] = new Vector3(0, 0, 4);
        Assert.Equal(FittedTrack.Line(new Vector3(0, 0, 4), Vector3.UnitZ, Vector3.UnitZ), FittedTrack.LineOf(fit));
        fit.Vectors[RailroadsMod.Keys.FitWayA] = Vector3.UnitX;
        fit.Vectors[RailroadsMod.Keys.FitWayB] = Vector3.UnitX;
        Assert.Equal(FittedTrack.Line(new Vector3(0, 0, 4), Vector3.UnitX, Vector3.UnitX), FittedTrack.LineOf(fit));
    }

    // Values no fitted track the mod builds has (a damaged world or blueprint): no line, rather than millions of points.
    [Theory]
    [InlineData(1e7f, 0, 0)]
    [InlineData(0, 0, float.NaN)]
    [InlineData(70, 0, 0)]
    [InlineData(0, 3, 33)]
    public void AFarEndNoFitHasGivesNoLine(float x, float y, float z)
    {
        var fit = new FakeObject(RailroadsMod.FittedTrack);
        fit.Vectors[RailroadsMod.Keys.FitEnd] = new Vector3(x, y, z);

        Assert.Null(FittedTrack.LineOf(fit));
    }

    [Fact]
    public void AWayNoFitHasGivesNoLine()
    {
        var fit = new FakeObject(RailroadsMod.FittedTrack);
        fit.Vectors[RailroadsMod.Keys.FitEnd] = new Vector3(0, 0, FittedTrack.MaxReach);
        Assert.NotNull(FittedTrack.LineOf(fit));
        fit.Vectors[RailroadsMod.Keys.FitWayA] = new Vector3(0, 0, 1000);
        Assert.Null(FittedTrack.LineOf(fit));
        // The mod lays none for a way shorter than half a metre or longer than one and a half (Railroads 0.4.1).
        fit.Vectors[RailroadsMod.Keys.FitWayA] = new Vector3(0, 0, 1.6f);
        Assert.Null(FittedTrack.LineOf(fit));
        fit.Vectors[RailroadsMod.Keys.FitWayA] = new Vector3(0.4f, 0, 0);
        Assert.Null(FittedTrack.LineOf(fit));
        fit.Vectors[RailroadsMod.Keys.FitWayA] = new Vector3(0, 0, 1.5f);
        Assert.NotNull(FittedTrack.LineOf(fit));
        fit.Vectors[RailroadsMod.Keys.FitWayA] = Vector3.UnitZ;
        fit.Vectors[RailroadsMod.Keys.FitWayB] = new Vector3(float.PositiveInfinity, 0, 0);
        Assert.Null(FittedTrack.LineOf(fit));
    }

    [Fact]
    public void TheRailsAreTheStraightBentAlongTheLineWhateverThePiecesTurn()
    {
        var rail = _models.Get(FittedTrack.RailModelName)!;
        var line = FittedTrack.Line(new Vector3(3, 0, 5), Vector3.UnitZ, Vector3.UnitX);
        float length = Steps(line).Sum();
        int pieces = (int)Math.Round(length);

        var flat = FittedTrack.Rails(line, rail, Quaternion.Identity).ToList();

        Assert.Equal([PieceShapes.Wood, PieceShapes.Stone], flat.Select(s => s.Color));
        Assert.Equal(3 * pieces, flat[0].Vertices.Length);
        Assert.Equal(3 * pieces, flat[0].Triangles.Length);
        Assert.Equal(3 * (pieces - 1), flat[0].Triangles[^3]);
        // The first sleeper about half a metre in, across the line's first way (north): from x -1 to 1, the curve only
        // beginning to bend there.
        Assert.Equal(-1, flat[0].Vertices[0].X, 1);
        Assert.Equal(1, flat[0].Vertices[1].X, 1);
        // The last rail corner by the far end, the rail 0.7 m to its right: the line arrives eastwards, so south of it
        // (the last quarter metre still bends a few degrees: a few centimetres off).
        var lastRail = flat[1].Vertices[^2];
        Assert.True(Math.Abs(lastRail.X - 3) < 0.1f, $"x {lastRail.X}");
        Assert.True(Math.Abs(lastRail.Z - (5 - 0.7f)) < 0.1f, $"z {lastRail.Z}");
        // A turned piece: the same rails in the world, so in its own frame turned back.
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.1f);
        var turned = FittedTrack.Rails(line, rail, turn).ToList();
        for (int i = 0; i < flat[1].Vertices.Length; i++)
        {
            Assert.True(Vector3.Distance(flat[1].Vertices[i], Vector3.Transform(turned[1].Vertices[i], turn)) < 1e-4f);
        }
    }
}
