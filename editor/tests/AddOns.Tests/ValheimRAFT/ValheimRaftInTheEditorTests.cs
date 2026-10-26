using System.Globalization;
using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The ValheimRAFT add-on in the editor, on a ship the real mod saved and built again (its tests' reference fixture):
/// with the add-on's links, the editor draws every piece where the mod built it, the swivel's wall too; a pasted copy
/// of the ship keeps its ropes between its own pieces; the inspector reads the ropes; the sail is drawn.
/// </summary>
[Collection("AddOns")]
public sealed class ValheimRaftInTheEditorTests : IDisposable
{
    private const string Id = "PersistentID", Offset = "MBPosition", Turn = "MBRotationVec", Ropes = "MBRopeAnchor_Ropes";

    private readonly AddOnSandbox _sandbox = new();

    public ValheimRaftInTheEditorTests() => _sandbox.Run("ValheimRAFT");

    public void Dispose() => _sandbox.Dispose();

    /// <summary>A piece of the ship as the mod saved it, and where it drew it when the world loaded again.</summary>
    private sealed record Piece(string Prefab, ZdoData Data, Vector3 Drawn, Quaternion DrawnTurn);

    /// <summary>The fixture: the ship's root (its data, its saved place and turn) and its pieces.</summary>
    private sealed record Ship(ZdoData Root, Vector3 At, Quaternion Turn, List<Piece> Pieces);

    /// <summary>The add-on's reference fixture (addons/ValheimRAFT/tests/.../fixtures/valheimraft-reference.txt).</summary>
    private static Ship Read()
    {
        string file = Directory.GetFiles(Path.Combine(RepositoryAddOns.Folder, "ValheimRAFT", "tests"), "valheimraft-reference.txt", SearchOption.AllDirectories)
            .First(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        Ship? ship = null;
        var pieces = new List<Piece>();
        foreach (string[] t in File.ReadLines(file).Where(l => l.Length > 0 && !l.StartsWith('#')).Select(l => l.Split(' ')))
        {
            float F(int i) => float.Parse(t[i], CultureInfo.InvariantCulture);
            int I(int i) => int.Parse(t[i], CultureInfo.InvariantCulture);
            switch (t[0])
            {
                case "R":
                    var root = new ZdoData { Prefab = StableHash.Of("ValheimVehicles_WaterVehicleShip") };
                    root.SetInt(StableHash.Of(Id), I(1));
                    ship = new Ship(root, new Vector3(F(2), F(3), F(4)), new Quaternion(F(5), F(6), F(7), F(8)), pieces);
                    break;
                case "P":
                    var z = new ZdoData { Prefab = StableHash.Of(t[1]) };
                    z.SetInt(StableHash.Of(t[2]), I(3));
                    if (I(4) != 0)
                    {
                        z.SetInt(StableHash.Of(Id), I(4));
                    }
                    z.SetVec3(StableHash.Of(Offset), new Vector3(F(5), F(6), F(7)));
                    z.SetVec3(StableHash.Of(Turn), new Vector3(F(8), F(9), F(10)));
                    pieces.Add(new Piece(t[1], z, new Vector3(F(11), F(12), F(13)), new Quaternion(F(14), F(15), F(16), F(17))));
                    break;
                case "B" when t[3] == Ropes:
                    pieces.Single(p => p.Prefab == t[1] && IdOf(p.Data) == I(2)).Data.SetBytes(StableHash.Of(Ropes), Convert.FromHexString(t[4]));
                    break;
                default:
                    // The tie points and the other values: the add-on's own tests check those.
                    break;
            }
        }
        return ship!;
    }

    /// <summary>An object's persistent id (0: none).</summary>
    private static int IdOf(ZdoData z) => ZdoData.TryGet(z.IntList, StableHash.Of(Id), out int id) ? id : 0;

    /// <summary>An object's values as the clipboard holds them (ints, vectors and bytes: what the fixture has).</summary>
    private static List<ObjectField> Fields(ZdoData z) =>
    [
        .. z.IntList.Select(v => new ObjectField("ints", v.Key, v.Value.ToString(CultureInfo.InvariantCulture))),
        .. z.Vec3List.Select(v => new ObjectField("vec3", v.Key, string.Create(CultureInfo.InvariantCulture, $"{v.Value.X:R} {v.Value.Y:R} {v.Value.Z:R}"))),
        .. z.ByteList.Select(v => new ObjectField("bytes", v.Key, Convert.ToBase64String(v.Value))),
    ];

    [Fact]
    public void EveryPieceIsDrawnWhereTheModBuiltIt()
    {
        var ship = Read();
        var members = new List<LinkIndex.Member>
        {
            new(1, ship.At, BlueprintFormats.ToEuler(ship.Turn), ObjectLinks.Read(ship.Root)!),
        };
        // Pieces are saved at the ship's place (the mod builds them from their offsets): what the editor draws comes
        // from the links alone.
        members.AddRange(ship.Pieces.Select((p, i) => new LinkIndex.Member(i + 2, ship.At, Vector3.Zero, ObjectLinks.Read(p.Data)!)));

        var links = LinkIndex.Of(members);

        Assert.Equal(ship.Pieces.Count + 1, links.Group(1).Count);
        for (int i = 0; i < ship.Pieces.Count; i++)
        {
            var (at, turn) = links.Pose(i + 2)!.Value;
            var piece = ship.Pieces[i];
            Assert.True(Vector3.Distance(piece.Drawn, at) < 1e-3f, $"{piece.Prefab} is drawn at {at}, the mod built it at {piece.Drawn}");
            Assert.True(Math.Abs(Quaternion.Dot(piece.DrawnTurn, BlueprintFormats.FromEuler(turn))) > 1 - 1e-5f, $"{piece.Prefab} is turned {turn}");
        }
    }

    /// <summary>An object of the second fixture: where it was saved, where the mod built it, and its data.</summary>
    private sealed record Built(string Prefab, Vector3 Saved, Quaternion SavedTurn, Vector3 At, Quaternion Turn, ZdoData Data);

    /// <summary>The add-on's second fixture (valheimraft-swivels.txt): a ship with turned and moved swivels, a sail on a mast, a cart and a land vehicle.</summary>
    private static List<Built> ReadSwivels()
    {
        string file = Directory.GetFiles(Path.Combine(RepositoryAddOns.Folder, "ValheimRAFT", "tests"), "valheimraft-swivels.txt", SearchOption.AllDirectories)
            .First(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        var all = new List<Built>();
        var byTag = new Dictionary<string, Built>();
        foreach (string[] t in File.ReadLines(file).Where(l => l.Length > 0 && !l.StartsWith('#')).Select(l => l.Split(' ')))
        {
            float F(int i) => float.Parse(t[i], CultureInfo.InvariantCulture);
            if (t[0] == "O")
            {
                var b = new Built(t[2], new Vector3(F(3), F(4), F(5)), new Quaternion(F(6), F(7), F(8), F(9)), new Vector3(F(10), F(11), F(12)),
                    new Quaternion(F(13), F(14), F(15), F(16)), new ZdoData { Prefab = StableHash.Of(t[2]) });
                byTag[t[1]] = b;
                all.Add(b);
                continue;
            }
            var z = byTag[t[1]].Data;
            switch (t[0])
            {
                case "I":
                    z.SetInt(StableHash.Of(t[2]), int.Parse(t[3], CultureInfo.InvariantCulture));
                    break;
                case "F":
                    z.FloatList.Add((StableHash.Of(t[2]), F(3)));
                    break;
                case "V":
                    z.SetVec3(StableHash.Of(t[2]), new Vector3(F(3), F(4), F(5)));
                    break;
                default:
                    throw new InvalidDataException($"valheimraft-swivels.txt: unknown line kind '{t[0]}'.");
            }
        }
        return all;
    }

    [Fact]
    public void SwivelsSailsOnMastsCartsAndParkedVehiclesAreDrawnWhereTheModBuiltThem()
    {
        var objects = ReadSwivels();
        var members = objects.Select((o, i) => ObjectLinks.Read(o.Data) is { } link
            ? new LinkIndex.Member(i, o.Saved, BlueprintFormats.ToEuler(o.SavedTurn), link) { Prefab = o.Data.Prefab }
            : null).OfType<LinkIndex.Member>().ToList();

        // With the objects' data: the add-on's placer turns the swivels' parts and puts the parked vehicle in place.
        var links = LinkIndex.Of(members, i => objects[i].Data);

        Assert.Equal(objects.Count, members.Count);
        Assert.Equal(objects.Count, links.Group(objects.FindIndex(o => o.Prefab == "ValheimVehicles_WaterVehicleShip")).Count);
        for (int i = 0; i < objects.Count; i++)
        {
            var (at, turn) = links.Pose(i)!.Value;
            var o = objects[i];
            Assert.True(Vector3.Distance(o.At, at) < 1e-3f, $"{o.Prefab} is drawn at {at}, the mod built it at {o.At}");
            Assert.True(Math.Abs(Quaternion.Dot(o.Turn, BlueprintFormats.FromEuler(turn))) > 1 - 1e-5f, $"{o.Prefab} is turned {turn}");
        }
    }

    [Fact]
    public void APastedShipsRopesAreTiedToItsOwnPieces()
    {
        var ship = Read();
        static NewObject Made(ZdoData z, Vector3 at) => new(0, z.Prefab, at, Vector3.Zero, 1, Data: Fields(z));
        var copy = ObjectLinks.Remap([Made(ship.Root, ship.At), .. ship.Pieces.Select(p => Made(p.Data, ship.At))],
            new HashSet<int>(ship.Pieces.Select(p => IdOf(p.Data)).Append(IdOf(ship.Root))));

        int NewId(string prefab) => int.Parse(copy.Single(o => o.Prefab == StableHash.Of(prefab) && o.Data!.Any(f => f.Key == StableHash.Of(Id) && f.Value != null))
            .Data!.Single(f => f.Key == StableHash.Of(Id)).Value!, CultureInfo.InvariantCulture);
        var tied = copy.Where(o => o.Prefab == StableHash.Of("MBRopeAnchor"))
            .Select(o => Convert.FromBase64String(o.Data!.Single(f => f.Key == StableHash.Of(Ropes)).Value!))
            .Select(b => BitConverter.ToInt32(b, 1)).Order().ToList();

        Assert.Equal(new[] { NewId("piece_chest_wood"), NewId("MBRopeAnchor") }.Order(), tied);
        Assert.DoesNotContain(tied, t => ship.Pieces.Any(p => IdOf(p.Data) == t));
    }

    [Fact]
    public void APastedShipIsNotDockedAndWorksOutItsHullAgain()
    {
        // ValheimRAFT 5.1: the ship names the dock clamp it is docked at by the game's object id, and keeps its hull shape.
        var ship = Read();
        var root = new NewObject(0, ship.Root.Prefab, ship.At, Vector3.Zero, 1, Data:
        [
            .. Fields(ship.Root),
            new ObjectField("longs", StableHash.Of("vehicle_dockZdoId_u"), "-4211"),
            new ObjectField("longs", StableHash.Of("vehicle_dockZdoId_i"), "17"),
            new ObjectField("bytes", StableHash.Of("VehicleConvexHullData"), Convert.ToBase64String([1, 1, 0])),
            new ObjectField("ints", StableHash.Of("VehicleConvexHullDataVersionHash"), "3"),
            new ObjectField("ints", StableHash.Of("vehicle_buildMode"), "1"),
        ]);

        var copy = ObjectLinks.Remap([root], new HashSet<int> { IdOf(ship.Root) }).Single().Data!;

        string? Value(string key) => copy.SingleOrDefault(f => f.Key == StableHash.Of(key))?.Value;
        Assert.All<string>(["vehicle_dockZdoId_u", "vehicle_dockZdoId_i", "VehicleConvexHullData", "VehicleConvexHullDataVersionHash"], k => Assert.Null(Value(k)));
        Assert.Equal("1", Value("vehicle_buildMode"));
    }

    [Fact]
    public void TheInspectorSaysWhatARopeIsTiedTo()
    {
        var anchor = Read().Pieces.First(p => p.Prefab == "MBRopeAnchor" && IdOf(p.Data) != 0);
        var view = new ObjectView("MBRopeAnchor", anchor.Drawn, Vector3.Zero, anchor.Data);
        Assert.Equal("1 rope to: 1654752419", ModExtensions.Decode(StableHash.Of(Ropes), view, anchor.Data.ByteList.Single().Value));
    }

    [Fact]
    public void TheSailIsDrawnBetweenItsCorners()
    {
        var z = new ZdoData { Prefab = StableHash.Of("MBSail") };
        z.SetInt(StableHash.Of("m_sailCornersCountHash"), 3);
        z.SetVec3(StableHash.Of("m_sailCorner1Hash"), Vector3.Zero);
        z.SetVec3(StableHash.Of("m_sailCorner2Hash"), new Vector3(0, -3, 0));
        z.SetVec3(StableHash.Of("m_sailCorner3Hash"), new Vector3(-4, -3, 0.5f));

        var shape = Assert.Single(ModExtensions.ShapesOf(z.Prefab, new ObjectView("MBSail", Vector3.Zero, Vector3.Zero, z), ModExtensions.NoWorld));

        Assert.Equal(3, shape.Vertices.Length);
        Assert.True(shape.BothSides);
    }
}
