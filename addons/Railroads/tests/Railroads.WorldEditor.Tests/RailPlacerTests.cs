using System.Numerics;
using Xunit;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// The mod's pieces set right wherever the editor puts them: track keeps the turn it is put with; a vehicle stands on
/// the nearest line of track within 1.5 m, at its point and facing its way, stopped; a station sign stops trains at the
/// nearest point of straight track within 10 m; the drawbridge is track vehicles stand on but no stop; track put in the
/// same step counts.
/// </summary>
public sealed class RailPlacerTests : IDisposable
{
    private static readonly Quaternion Quarter = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)Math.PI / 2);

    private readonly ModFolder _mod = new();
    private readonly RailPlacer _placer;

    public RailPlacerTests()
    {
        // An 8 m straight and a curve, each a line along z from its origin.
        _mod.Put("track_straight_8", ModelBytes.Write([("stone", [Vector3.Zero, Vector3.UnitZ, Vector3.UnitX], [0, 1, 2])],
            lines: [[Vector3.Zero, new Vector3(0, 0, 4), new Vector3(0, 0, 8)]]));
        _mod.Put("track_curve_r8_45", ModelBytes.Write([("stone", [Vector3.Zero, Vector3.UnitZ, Vector3.UnitX], [0, 1, 2])],
            lines: [[Vector3.Zero, new Vector3(1, 0, 4), new Vector3(2.34f, 0, 5.66f)]]));
        _mod.Put("track_drawbridge", ModelBytes.Write([("stone", [Vector3.Zero, Vector3.UnitZ, Vector3.UnitX], [0, 1, 2])],
            lines: [[Vector3.Zero, new Vector3(0, 0, 8), new Vector3(0, 0, 16)]]));
        _placer = new RailPlacer(new ModelLibrary(new FakeHost { Plugin = _mod.Plugin }, Assert.Fail));
    }

    public void Dispose() => _mod.Dispose();

    /// <summary>A straight at a place, turned a quarter (its line then runs along x).</summary>
    private static FakeObject Straight(Vector3 at) => new("bf_track_straight_8") { Position = at, Orientation = Quarter };

    [Fact]
    public void TrackKeepsTheTurnItIsPutWith()
    {
        var track = new FakeObject("bf_track_curve_r8_45") { Orientation = Quarter };
        track.Quaternions[RailroadsMod.Keys.Turn] = Quaternion.Identity;
        var station = new FakeObject(RailroadsMod.OldStation) { Orientation = Quarter };
        var console = new FakeObject("bf_train_console") { Orientation = Quarter };

        _placer.Place([track, station, console], new ObjectsArea());

        Assert.Equal(Quarter, track.Quaternions[RailroadsMod.Keys.Turn]);
        Assert.Equal(Quarter, station.Quaternions[RailroadsMod.Keys.Turn]);
        Assert.Empty(console.Keys);
    }

    [Fact]
    public void AVehicleStandsOnTheNearestTrackStopped()
    {
        // The straight's line runs from (10, 0, 0) to (18, 0, 0); the locomotive stands beside it, facing the other way.
        var track = Straight(new Vector3(10, 0, 0));
        var train = new FakeObject(RailroadsMod.Locomotive) { Position = new Vector3(13, 0.5f, 1), Orientation = Quaternion.Inverse(Quarter) };
        train.Bytes[RailroadsMod.Keys.Route] = TrackLines.Write([(Vector3.Zero, Vector3.UnitX)]);
        train.Ints[RailroadsMod.Keys.Moving] = 1;
        train.Floats[RailroadsMod.Keys.Speed] = 9;
        train.Ints[RailroadsMod.Keys.Coal] = 12;
        train.Longs[RailroadsMod.Keys.SentBy] = 77;
        train.Floats[RailroadsMod.Keys.TripCoal] = 1.4f;

        _placer.Place([train], new ObjectsArea(track));

        var (a, b) = Assert.Single(TrackLines.Read(train.Bytes[RailroadsMod.Keys.Line])!);
        Assert.True(Vector3.Distance(new Vector3(10, 0, 0), a) < 1e-4f && Vector3.Distance(new Vector3(18, 0, 0), b) < 1e-4f);
        Assert.Equal(3, train.Floats[RailroadsMod.Keys.At], 1e-4f);
        Assert.Equal(-1, train.Ints[RailroadsMod.Keys.Dir]);
        Assert.Equal(new[] { RailroadsMod.Keys.At, RailroadsMod.Keys.Coal, RailroadsMod.Keys.Dir, RailroadsMod.Keys.Line }.Order(StringComparer.Ordinal),
            train.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AVehicleFacingTheTracksWayKeepsNoWay()
    {
        var wagon = new FakeObject("bf_wagon") { Position = new Vector3(12, 0, 0), Orientation = Quarter };
        wagon.Ints[RailroadsMod.Keys.Dir] = -1;

        _placer.Place([wagon], new ObjectsArea(Straight(new Vector3(10, 0, 0))));

        Assert.DoesNotContain(RailroadsMod.Keys.Dir, wagon.Keys);
        Assert.Equal(2, wagon.Floats[RailroadsMod.Keys.At], 1e-4f);
    }

    [Fact]
    public void AVehicleFarFromTrackStandsOnNone()
    {
        var cart = new FakeObject(RailroadsMod.Handcart) { Position = new Vector3(13, 0, 1.6f) };
        cart.Bytes[RailroadsMod.Keys.Line] = TrackLines.Write([(Vector3.Zero, Vector3.UnitX)]);
        cart.Floats[RailroadsMod.Keys.At] = 1;

        _placer.Place([cart], new ObjectsArea(Straight(new Vector3(10, 0, 0))));

        Assert.Empty(cart.Keys);
    }

    [Fact]
    public void ASignStopsTrainsAtTheNearestStraightTrack()
    {
        var sign = new FakeObject(RailroadsMod.Sign) { Position = new Vector3(14, 1, 3) };
        sign.Strings[RailroadsMod.Keys.Name] = "Riverside";
        // A curve nearer the sign than the straight does not count.
        var curve = new FakeObject("bf_track_curve_r8_45") { Position = new Vector3(14, 0, 2) };

        _placer.Place([sign], new ObjectsArea(Straight(new Vector3(10, 0, 0)), curve));

        Assert.True(Vector3.Distance(new Vector3(14, 0, 0), sign.Vectors[RailroadsMod.Keys.Stop]) < 1e-4f);
        Assert.Equal("Riverside", sign.Strings[RailroadsMod.Keys.Name]);
    }

    [Fact]
    public void ASignFarFromStraightTrackIsNoStop()
    {
        var sign = new FakeObject(RailroadsMod.Sign) { Position = new Vector3(14, 0, 10.5f) };
        sign.Vectors[RailroadsMod.Keys.Stop] = new Vector3(1, 2, 3);

        _placer.Place([sign], new ObjectsArea(Straight(new Vector3(10, 0, 0))));

        Assert.Empty(sign.Keys);
    }

    [Fact]
    public void TheDrawbridgeIsTrackVehiclesStandOnButNoStop()
    {
        // Its line runs from (40, 0, 0) to (56, 0, 0); the wagon stands on it, the sign is beside it.
        var bridge = new FakeObject(RailroadsMod.Drawbridge) { Position = new Vector3(40, 0, 0), Orientation = Quarter };
        var wagon = new FakeObject("bf_wagon") { Position = new Vector3(45, 1, 0.5f), Orientation = Quarter };
        var sign = new FakeObject(RailroadsMod.Sign) { Position = new Vector3(48, 1, 3) };
        var post = new FakeObject("bf_feedback_post") { Position = new Vector3(44, 0, 3), Orientation = Quarter };

        _placer.Place([bridge, wagon, sign, post], new ObjectsArea());

        Assert.Equal(Quarter, bridge.Quaternions[RailroadsMod.Keys.Turn]);
        var (a, b) = Assert.Single(TrackLines.Read(wagon.Bytes[RailroadsMod.Keys.Line])!);
        Assert.True(Vector3.Distance(new Vector3(40, 0, 0), a) < 1e-4f && Vector3.Distance(new Vector3(56, 0, 0), b) < 1e-4f);
        Assert.Equal(5, wagon.Floats[RailroadsMod.Keys.At], 1e-4f);
        // Trains stop at signs only beside straight track and loops, as the mod has it.
        Assert.Empty(sign.Keys);
        Assert.Empty(post.Keys);
    }

    [Fact]
    public void TrackPutInTheSameStepCounts()
    {
        var track = Straight(new Vector3(100, 0, 0));
        var sign = new FakeObject(RailroadsMod.Sign) { Position = new Vector3(103, 0, 4) };
        var train = new FakeObject(RailroadsMod.Locomotive) { Position = new Vector3(105, 0, 0), Orientation = Quarter };

        _placer.Place([sign, track, train], new ObjectsArea(Straight(new Vector3(10, 0, 0))));

        Assert.True(Vector3.Distance(new Vector3(103, 0, 0), sign.Vectors[RailroadsMod.Keys.Stop]) < 1e-4f);
        Assert.Equal(5, train.Floats[RailroadsMod.Keys.At], 1e-4f);
        Assert.Equal(Quarter, track.Quaternions[RailroadsMod.Keys.Turn]);
    }

    [Fact]
    public void ItPlacesEveryPieceOfTheMod() => Assert.Equal(RailroadsMod.Pieces.Select(p => p.Prefab), RailPlacer.Prefabs);

    [Fact]
    public void WithoutVehiclesOrSignsNoTrackIsRead()
    {
        // No model is read at all (the library would fail the test on a missing one).
        var placer = new RailPlacer(new ModelLibrary(new FakeHost(), Assert.Fail));
        var track = new FakeObject("bf_track_loop");

        placer.Place([track], new ObjectsArea(new FakeObject("bf_track_junction_x")));

        Assert.Equal(Quaternion.Identity, track.Quaternions[RailroadsMod.Keys.Turn]);
    }

    /// <summary>A fitted track from a place, its far end and ways as given, turned.</summary>
    private static FakeObject Fit(Vector3 at, Vector3 end, Quaternion turn)
    {
        var fit = new FakeObject(RailroadsMod.FittedTrack) { Position = at, Orientation = turn };
        fit.Vectors[RailroadsMod.Keys.FitEnd] = end;
        fit.Vectors[RailroadsMod.Keys.FitWayA] = Vector3.UnitZ;
        fit.Vectors[RailroadsMod.Keys.FitWayB] = Vector3.UnitZ;
        return fit;
    }

    // Turned, a fitted track takes the turn into its far end and ways and stands unturned, as the mod builds it; turned
    // again later, only by that turn.
    [Fact]
    public void ATurnedFitTakesItsTurnIntoItsValues()
    {
        var fit = Fit(new Vector3(5, 0, 5), new Vector3(0, 1, 8), Quarter);
        fit.Quaternions[RailroadsMod.Keys.Turn] = Quarter;

        _placer.Place([fit], new ObjectsArea());

        Assert.True(Vector3.Distance(Vector3.Transform(new Vector3(0, 1, 8), Quarter), fit.Vectors[RailroadsMod.Keys.FitEnd]) < 1e-4f);
        Assert.True(Vector3.Distance(Vector3.Transform(Vector3.UnitZ, Quarter), fit.Vectors[RailroadsMod.Keys.FitWayA]) < 1e-4f);
        Assert.True(Vector3.Distance(Vector3.Transform(Vector3.UnitZ, Quarter), fit.Vectors[RailroadsMod.Keys.FitWayB]) < 1e-4f);
        Assert.Equal(Quaternion.Identity, fit.Orientation);
        Assert.Equal(Quaternion.Identity, fit.Quaternions[RailroadsMod.Keys.Turn]);
        // Turned once more: from where it was.
        fit.Orientation = Quarter;
        _placer.Place([fit], new ObjectsArea());
        Assert.True(Vector3.Distance(new Vector3(0, 1, -8), fit.Vectors[RailroadsMod.Keys.FitEnd]) < 1e-4f);
        // Not turned: as it was; without values, unturned all the same.
        var still = Fit(Vector3.Zero, new Vector3(2, 0, 6), Quaternion.Identity);
        var bare = new FakeObject(RailroadsMod.FittedTrack) { Orientation = Quarter };
        _placer.Place([still, bare], new ObjectsArea());
        Assert.Equal(new Vector3(2, 0, 6), still.Vectors[RailroadsMod.Keys.FitEnd]);
        Assert.Equal(Quaternion.Identity, bare.Orientation);
        Assert.Equal(Quaternion.Identity, bare.Quaternions[RailroadsMod.Keys.Turn]);
    }

    // An editor that cannot turn a copy back: the values still take the turn, the piece keeps its turn in the editor.
    [Fact]
    public void WithoutTurningBackTheValuesStillTakeTheTurn()
    {
        var placer = new RailPlacer(new ModelLibrary(new FakeHost { Plugin = _mod.Plugin }, Assert.Fail), canTurn: false);
        var fit = Fit(Vector3.Zero, new Vector3(0, 0, 8), Quarter);

        placer.Place([fit], new ObjectsArea());

        Assert.True(Vector3.Distance(Vector3.Transform(new Vector3(0, 0, 8), Quarter), fit.Vectors[RailroadsMod.Keys.FitEnd]) < 1e-4f);
        Assert.Equal(Quarter, fit.Orientation);
        Assert.Equal(Quaternion.Identity, fit.Quaternions[RailroadsMod.Keys.Turn]);
    }

    // Vehicles stand on a fitted track's line (from its place, along the world's axes whatever its turn in the editor);
    // a station sign never stops trains on it.
    [Fact]
    public void VehiclesStandOnAFitAndSignsDoNotStopOnIt()
    {
        var fit = Fit(new Vector3(20, 0, 0), new Vector3(0, 0, 8), Quarter);
        fit.Orientation = Quarter;
        var wagon = new FakeObject("bf_wagon") { Position = new Vector3(20.5f, 0.4f, 4) };
        var sign = new FakeObject(RailroadsMod.Sign) { Position = new Vector3(22, 0, 4) };
        var area = new ObjectsArea(fit);

        _placer.Place([wagon, sign], area);

        Assert.True(wagon.Floats.TryGetValue(RailroadsMod.Keys.At, out float at));
        Assert.Equal(4, at, 1);
        Assert.False(sign.Vectors.ContainsKey(RailroadsMod.Keys.Stop));
        var network = TrackNetwork.Of([fit], new ModelLibrary(new FakeHost { Plugin = _mod.Plugin }, Assert.Fail));
        var line = Assert.Single(network.Lines);
        Assert.Equal(new Vector3(20, 0, 0), line.A);
        Assert.Equal(new Vector3(20, 0, 8), line.B);
    }
}
