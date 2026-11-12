using System.Numerics;
using WorldEditor.Tests;
using Xunit;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// The real Railroads (the version the add-on follows, downloaded for the tests): every piece's model is in its folder
/// and read whole, in the mod's materials; its track's lines run as its pieces are named, the drawbridge's (0.3.1) as a
/// 16 m straight's; each piece is drawn, a vehicle on its wheels and with its couplers (apart from its body since 0.3.0),
/// the locomotive with its name boards (0.2.7), the portal wagon without posts on its open side (0.2.8), the drawbridge
/// with its leaves, chains and weights, the crossing barrier with its arm and the crossing lights with their lamps (0.4.7);
/// the buffer stop's track (0.4.15) runs up to its beam; the train workshop (0.5.0) is drawn whole.
/// </summary>
public class RealModTests
{
    /// <summary>The mod's folder, as a mod manager installs it.</summary>
    private static string Folder => Path.Combine(TestMods.Folder, "VladasZ-Railroads", "plugins", "Railroads");

    private static RailModel Model(string name) => ModelFile.Read(File.ReadAllBytes(Path.Combine(Folder, RailroadsMod.MeshFolder, name + ".bfm")));

    public static TheoryData<string> Models() =>
        [.. RailroadsMod.Pieces.Select(p => p.Model)
            .Concat([RailroadsMod.StationLegs, RailroadsMod.Axle, RailroadsMod.SmallAxle, RailroadsMod.BigAxle, RailroadsMod.Beam, RailroadsMod.Coupler])
            .Concat([RailroadsMod.CrossingArm, RailroadsMod.CrossingStripes, RailroadsMod.CrossingLamp])
            .Concat(DrawbridgeParts.Models)];

    [Theory]
    [MemberData(nameof(Models))]
    public void EveryModelTheAddOnDrawsIsReadWhole(string name)
    {
        var model = Model(name);

        Assert.NotEmpty(model.Parts);
        Assert.All(model.Parts, p => Assert.Contains(p.Slot, (string[])["wood", "stone", "iron"]));
        Assert.All(model.Parts, p => Assert.True(p.Triangles.Length > 0 && p.Triangles.Length % 3 == 0));
    }

    [Fact]
    public void EveryModelOfTheModIsOneTheAddOnKnows()
    {
        var known = Models().Select(m => m.Data).ToHashSet();
        var files = Directory.GetFiles(Path.Combine(Folder, RailroadsMod.MeshFolder), "*.bfm").Select(Path.GetFileNameWithoutExtension).ToList();

        // The levers, rods, coal and loads the add-on leaves out, and the locomotive's 2 gauges (Railroads 0.4.1).
        string[] notDrawn =
        [
            "cargo_1", "cargo_2", "cargo_3", "cargo_4", "coal", "crosshead", "lever", "rod_eccentric", "rod_main", "rod_valve", "whistle_lever",
            "gauge_cap", "gauge_face", "gauge_icon_load", "gauge_icon_speed", "gauge_marks", "gauge_marks_end", "gauge_needle", "gauge_rim",
        ];
        Assert.Equal(files.Order(StringComparer.Ordinal), known.Concat(notDrawn).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("track_straight_1", 1)]
    [InlineData("track_straight", 2)]
    [InlineData("track_straight_8", 8)]
    [InlineData("track_straight_32", 32)]
    public void AStraightRunsAsLongAsItsName(string name, float metres)
    {
        var line = Assert.Single(Model(name).Lines);

        Assert.Equal(Vector3.Zero, line[0]);
        Assert.Equal(new Vector3(0, 0, metres), line[^1]);
    }

    [Fact]
    public void TheDrawbridgeRunsAsA16MetreStraight()
    {
        var bridge = Assert.Single(Model("track_drawbridge").Lines);

        Assert.Equal(Assert.Single(Model("track_straight_16").Lines), bridge);
        Assert.Equal(new Vector3(0, 0, DrawbridgeParts.Length), bridge[^1]);
    }

    // The buffer stop (Railroads 0.4.15) stands where it snaps onto a free track end and lays 0.6 m of track from there,
    // ending under its beam: a train standing at that end has its front against the beam.
    [Fact]
    public void TheBufferStopsTrackRunsFromTheTrackEndToItsBeam()
    {
        var model = Model("track_buffer");
        var line = Assert.Single(model.Lines);

        Assert.Equal(Vector3.Zero, line[0]);
        Assert.Equal(new Vector3(0, 0, 0.6f), line[^1]);
        // The beam (its 2 timbers across the rails, over 1 m up) faces the track's end.
        Assert.Equal(2, model.Boxes.Count(b => b.Size.X > 2 && b.Center.Y > 1 && Math.Abs(b.Center.Z - (b.Size.Z / 2) - line[^1].Z) < 0.01f));
    }

    // As Railroads 0.5.0 laid them on a local server, from track written as the editor writes it (and again after a
    // restart): an 8 m straight turned 30 degrees, a buffer stop at each end, the one at its start turned to face away.
    [Fact]
    public void BufferStopsLayTheirTrackWhereTheModLaysIt()
    {
        var models = new ModelLibrary(new FakeHost { Plugin = Path.Combine(Folder, RailroadsMod.DllName) }, Assert.Fail);
        var turn = Quaternion.CreateFromYawPitchRoll((float)Math.PI / 6, 0, 0);
        var start = new Vector3(37.5f, 40, -21.25f);
        FakeObject[] pieces =
        [
            new("bf_track_straight_8") { Position = start, Orientation = turn },
            new(RailroadsMod.BufferStop) { Position = new Vector3(41.5f, 40, -14.3217964f), Orientation = turn },
            new(RailroadsMod.BufferStop) { Position = start, Orientation = turn * Quaternion.CreateFromYawPitchRoll((float)Math.PI, 0, 0) },
        ];

        var lines = TrackNetwork.Of(pieces, models).Lines;

        Assert.Equal(3, lines.Count);
        Assert.True(Vector3.Distance(new Vector3(41.5f, 40, -14.3217964f), lines[0].B) < 1e-4f, lines[0].B.ToString());
        Assert.Equal(lines[0].B, lines[1].A);
        Assert.True(Vector3.Distance(new Vector3(41.8f, 40, -13.8021812f), lines[1].B) < 1e-4f, lines[1].B.ToString());
        Assert.Equal(start, lines[2].A);
        Assert.True(Vector3.Distance(new Vector3(37.2f, 40, -21.7696152f), lines[2].B) < 1e-4f, lines[2].B.ToString());
    }

    [Fact]
    public void EveryVehicleButTheHandCartHasItsCouplersApart()
    {
        foreach (var piece in RailroadsMod.Pieces.Where(p => p.Kind == RailKind.Vehicle))
        {
            var model = Model(piece.Model);
            var couplers = model.Boxes.Where(b => (b.Size - RailroadsMod.CouplerBox).LengthSquared() < 1e-4f).ToList();
            // The locomotive is coupled at its back only; the hand cart never.
            int expected = piece.Prefab switch
            {
                RailroadsMod.Locomotive => 1,
                RailroadsMod.Handcart => 0,
                _ => 2,
            };
            Assert.True(expected == couplers.Count, $"{piece.Prefab}: {couplers.Count} couplers");
            // The body ends where its couplers begin.
            Assert.All(couplers, c => Assert.True(model.Parts.SelectMany(p => p.Positions).Max(v => v.Z * Math.Sign(c.Center.Z)) < Math.Abs(c.Center.Z), piece.Prefab));
        }
    }

    [Fact]
    public void OnlyTrackHasLinesAndOnlyVehiclesHaveAxles()
    {
        foreach (var piece in RailroadsMod.Pieces)
        {
            var model = Model(piece.Model);
            Assert.True((piece.Kind == RailKind.Track) == (model.Lines.Count > 0), piece.Prefab);
            Assert.True((piece.Kind == RailKind.Vehicle) == (model.Axles.Count > 0), piece.Prefab);
        }
    }

    [Fact]
    public void TheLocomotiveHasANameBoardOnEachSideOfItsBoiler()
    {
        var wood = Wood("locomotive");

        foreach (int side in (int[])[1, -1])
        {
            // Beside the boiler, its middle 3.18 m up and 0.45 m forward: 1.2 m long.
            var board = wood.Where(c => c.X * side is > 0.6f and < 0.7f && c.Y is > 2.9f and < 3.45f && c.Z is > -0.25f and < 1.15f).ToList();
            Assert.NotEmpty(board);
            Assert.Equal(1.2f, board.Max(c => c.Z) - board.Min(c => c.Z), 2);
        }
    }

    [Fact]
    public void ThePortalWagonHasPostsOnlyOnItsClosedSide()
    {
        var wood = Wood("portal");

        // A post at an end of the portal, 0.7 m to the side and 2 m along.
        int Post(int side, int end) => wood.Count(c => c.X * side is > 0.45f and < 0.95f && c.Z * end is > 1.75f and < 2.25f && c.Y > 1.3f);
        Assert.Equal((0, 0), (Post(1, 1), Post(1, -1)));
        Assert.True(Post(-1, 1) > 0 && Post(-1, -1) > 0);
    }

    [Fact]
    public void TheCrossingsArmAndLampsAreWhereTheirPostsHoldThem()
    {
        // The barrier's pin, across its 2 posts; each lamp under its hood on the lights' crossbar.
        Assert.Contains(Model("crossing_barrier").Boxes, b => Vector3.Distance(b.Center, RailroadsMod.ArmAt) < 1e-4f);
        var hoods = Model("crossing_lights").Boxes;
        Assert.All(RailroadsMod.LampsAt, at => Assert.Contains(hoods, b => Vector3.Distance(b.Center, at + new Vector3(0, 0.17f, 0)) < 1e-4f));
        // The arm reaches 4.3 m from its pin, its stripes on it.
        Assert.Equal(4.3f, Model(RailroadsMod.CrossingArm).Parts.SelectMany(p => p.Positions).Max(v => v.Z), 3);
        Assert.True(Model(RailroadsMod.CrossingStripes).Parts.SelectMany(p => p.Positions).Max(v => v.Z) < 4.3f);
    }

    /// <summary>The corners of a model's wood.</summary>
    private static List<Vector3> Wood(string name) => [.. Model(name).Parts.Where(p => p.Slot == "wood").SelectMany(p => p.Positions)];

    [Fact]
    public void EveryPieceIsDrawnFromTheModsFolder()
    {
        var host = new FakeHost { Plugin = Path.Combine(Folder, RailroadsMod.DllName) };
        var models = new ModelLibrary(host, Assert.Fail);
        foreach (var piece in RailroadsMod.Pieces)
        {
            var shapes = new PieceShapes(piece, models).ShapesOf(new FakeObject(piece.Prefab), new NoWorld()).ToList();
            Assert.Equal(Model(piece.Model).Parts.Count + Wheels(piece) + Extra(piece), shapes.Count);
        }
    }

    /// <summary>The parts of a piece's wheels: each axle's wheel set, the locomotive's driving wheels too.</summary>
    private static int Wheels(RailPiece piece)
    {
        if (piece.Kind != RailKind.Vehicle)
        {
            return 0;
        }
        bool locomotive = piece.Prefab == RailroadsMod.Locomotive;
        int sets = Model(piece.Model).Axles.Count * Model(locomotive ? RailroadsMod.SmallAxle : RailroadsMod.Axle).Parts.Count;
        return sets + (locomotive ? Model(RailroadsMod.BigAxle).Parts.Count : 0);
    }

    /// <summary>
    /// The parts drawn besides the body and wheels: a vehicle's couplers, the old station's legs, the hand cart's pump
    /// handle, the drawbridge's moving parts (one shape a material), the crossing barrier's arm and its stripes, the
    /// crossing lights' 2 lamps.
    /// </summary>
    private static int Extra(RailPiece piece) => piece.Prefab switch
    {
        RailroadsMod.OldStation => Model(RailroadsMod.StationLegs).Parts.Count,
        RailroadsMod.Handcart => Model(RailroadsMod.Beam).Parts.Count,
        RailroadsMod.CrossingBarrier => Model(RailroadsMod.CrossingArm).Parts.Count + Model(RailroadsMod.CrossingStripes).Parts.Count,
        RailroadsMod.CrossingLights => RailroadsMod.LampsAt.Count * Model(RailroadsMod.CrossingLamp).Parts.Count,
        RailroadsMod.Drawbridge => DrawbridgeParts.Models.SelectMany(m => Model(m).Parts).Select(p => p.Slot).Distinct().Count(),
        _ => 0,
    } + (piece.Kind == RailKind.Vehicle
        ? Model(piece.Model).Boxes.Count(b => (b.Size - RailroadsMod.CouplerBox).LengthSquared() < 1e-4f) * Model(RailroadsMod.Coupler).Parts.Count
        : 0);
}
