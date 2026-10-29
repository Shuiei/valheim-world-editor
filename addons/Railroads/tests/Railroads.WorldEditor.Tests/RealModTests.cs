using System.Numerics;
using WorldEditor.Tests;
using Xunit;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// The real Railroads (the version the add-on follows, downloaded for the tests): every piece's model is in its folder
/// and read whole, in the mod's materials; its track's lines run as its pieces are named, the drawbridge's (0.3.1) as a
/// 16 m straight's; each piece is drawn, a vehicle on its wheels and with its couplers (apart from its body since 0.3.0),
/// the locomotive with its name boards (0.2.7), the portal wagon without posts on its open side (0.2.8), the drawbridge
/// with its leaves, chains and weights.
/// </summary>
public class RealModTests
{
    /// <summary>The mod's folder, as a mod manager installs it.</summary>
    private static string Folder => Path.Combine(TestMods.Folder, "VladasZ-Railroads", "plugins", "Railroads");

    private static RailModel Model(string name) => ModelFile.Read(File.ReadAllBytes(Path.Combine(Folder, RailroadsMod.MeshFolder, name + ".bfm")));

    public static TheoryData<string> Models() =>
        [.. RailroadsMod.Pieces.Select(p => p.Model)
            .Concat([RailroadsMod.StationLegs, RailroadsMod.Axle, RailroadsMod.SmallAxle, RailroadsMod.BigAxle, RailroadsMod.Beam, RailroadsMod.Coupler])
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

        // The levers, rods, coal and loads the add-on leaves out.
        string[] notDrawn = ["cargo_1", "cargo_2", "cargo_3", "cargo_4", "coal", "crosshead", "lever", "rod_eccentric", "rod_main", "rod_valve", "whistle_lever"];
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
    /// handle, the drawbridge's moving parts (one shape a material).
    /// </summary>
    private static int Extra(RailPiece piece) => piece.Prefab switch
    {
        RailroadsMod.OldStation => Model(RailroadsMod.StationLegs).Parts.Count,
        RailroadsMod.Handcart => Model(RailroadsMod.Beam).Parts.Count,
        RailroadsMod.Drawbridge => DrawbridgeParts.Models.SelectMany(m => Model(m).Parts).Select(p => p.Slot).Distinct().Count(),
        _ => 0,
    } + (piece.Kind == RailKind.Vehicle
        ? Model(piece.Model).Boxes.Count(b => (b.Size - RailroadsMod.CouplerBox).LengthSquared() < 1e-4f) * Model(RailroadsMod.Coupler).Parts.Count
        : 0);
}
