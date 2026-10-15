using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Generated dungeons (DungeonGen): the same settings make the same dungeon; every object is one of the game's; it
/// holds in the sky (black marble slabs, nothing falls); every room can be reached, the boss's key without going
/// through the boss's gate; doorways open between rooms, not into walls.
/// </summary>
public class DungeonGenTests
{
    /// <summary>
    /// Settings to make dungeons with: every style in six biomes, sizes and levels 1 to 3, each with its own seed.
    /// </summary>
    public static IEnumerable<object[]> Samples()
    {
        int seed = 1;
        foreach (string? style in DungeonKit.Styles.Select(s => s.Name))
        {
            foreach (string? biome in new[] { "Black Forest", "Swamp", "Mistlands", "Ashlands", "Deep North", "Plains" })
            {
                yield return new object[] { style, biome, 1 + seed % 3, 1 + seed % 3, seed++ };
            }
        }
    }

    /// <summary>
    /// The samples of two styles only (the same seeds as in Samples).
    /// </summary>
    public static IEnumerable<object[]> SamplesOf(string style1, string style2) => Samples().Where(s => (string)s[0] == style1 || (string)s[0] == style2);

    /// <summary>
    /// Makes a dungeon with these settings.
    /// </summary>
    private static DungeonGen.Result Make(string style, string biome, int size, int levels, int seed) =>
        DungeonGen.Make(new DungeonGen.Settings(Biome: biome, Style: style, Size: size, Levels: levels, Seed: seed));

    [Fact]
    public void TheSameSettingsMakeTheSameDungeon()
    {
        var a = Make("Crypt", "Swamp", 3, 2, 42);
        var b = Make("Crypt", "Swamp", 3, 2, 42);
        Assert.Equal(a.Items.Count, b.Items.Count);
        Assert.True(a.Items.Zip(b.Items).All(p => p.First.Prefab == p.Second.Prefab && p.First.Position == p.Second.Position));
        Assert.NotEqual(a.Items.Count, Make("Crypt", "Swamp", 3, 2, 43).Items.Count);
    }

    /// <summary>
    /// Checked in DungeonGenHolds*Tests: the slowest check (the stability of every piece), split in classes of two
    /// styles that run side by side.
    /// </summary>
    internal static void MadeOfTheGamesObjectsAndHolds(string style, string biome, int size, int levels, int seed)
    {
        var r = Make(style, biome, size, levels, seed);
        var unknown = r.Items.Select(i => i.Prefab).Distinct().Where(n => PrefabCatalog.Details(StableHash.Of(n)) == null).ToList();
        Assert.Empty(unknown);
        var pieces = r.Items.Where(i => Stability.Known(i.Prefab)).Select(i => new Stability.Piece(i.Prefab, i.Position, i.Rotation)).ToList();
        var falls = Stability.Solve(pieces, (_, _) => -1000).Falls;
        // What hangs on goblin walls holds in game (they are not pieces: ground to what touches them),
        // but the check does not see those walls.
        bool goblinWalls = biome == "Plains";
        var real = falls.Where(i => !(goblinWalls && pieces[i].Prefab is "sign" or "piece_banner01" or "piece_banner02" or "piece_banner05" or "piece_banner06" or "piece_banner07"))
            .Select(i => $"{pieces[i].Prefab} at {pieces[i].Position}").ToList();
        Assert.Empty(real);
    }

    // A style added later is checked too: the DungeonGenHolds*Tests classes cover every style.
    [Fact]
    public void TheHoldingChecksCoverEveryStyle()
    {
        var styles = typeof(DungeonGenTests).Assembly.GetTypes()
            .Where(t => t.Name.StartsWith("DungeonGenHolds", StringComparison.Ordinal))
            .SelectMany(t => t.GetMethods().SelectMany(m => m.GetCustomAttributes(typeof(MemberDataAttribute), false)))
            .SelectMany(a => ((MemberDataAttribute)a).Arguments.OfType<string>())
            .ToList();
        Assert.Equal(DungeonKit.Styles.Select(s => s.Name).Order(), styles.Order());
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void EveryRoomCanBeReachedAndTheKeyBeforeTheBoss(string style, string biome, int size, int levels, int seed)
    {
        var r = Make(style, biome, size, levels, seed);
        var map = r.Map!;
        int entrance = map.ToList().FindIndex(m => m.Name == "Entrance hall");
        Assert.True(entrance >= 0);
        HashSet<int> Reach(bool throughKeyGate)
        {
            var seen = new HashSet<int> { entrance };
            var queue = new Queue<int>(seen);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                foreach (var d in r.Doors!.Where(d => d.A == at || d.B == at))
                {
                    if (d.Kind == "Key" && !throughKeyGate)
                    {
                        continue;
                    }
                    int other = d.A == at ? d.B : d.A;
                    if (seen.Add(other))
                    {
                        queue.Enqueue(other);
                    }
                }
            }
            return seen;
        }
        Assert.Equal(map.Count, Reach(true).Count);
        int key = map.ToList().FindIndex(m => m.Key);
        if (key >= 0)
        {
            Assert.Contains(key, Reach(false));
            Assert.DoesNotContain(map.ToList().FindIndex(m => m.Name == "Arena"), Reach(false));
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void RoomsDoNotOverlapAndDoorwaysJoinTheirRooms(string style, string biome, int size, int levels, int seed)
    {
        var r = Make(style, biome, size, levels, seed);
        var map = r.Map!;
        for (int i = 0; i < map.Count; i++)
        {
            for (int j = i + 1; j < map.Count; j++)
            {
                var (a, b) = (map[i], map[j]);
                bool overlap = a.Level == b.Level && a.X0 < b.X1 - 0.01f && b.X0 < a.X1 - 0.01f && a.Z0 < b.Z1 - 0.01f && b.Z0 < a.Z1 - 0.01f;
                Assert.False(overlap && !(a.Corridor && b.Corridor), $"{a.Name} and {b.Name} overlap");
            }
        }
        foreach (var d in r.Doors!.Where(d => d.Kind != "Stairs"))
        {
            // On the edge of both rooms' boxes.
            foreach (var room in new[] { map[d.A], map[d.B] })
            {
                bool onEdge = (MathF.Abs(d.X - room.X0) < 0.01f || MathF.Abs(d.X - room.X1) < 0.01f) && d.Z > room.Z0 && d.Z < room.Z1
                    || (MathF.Abs(d.Z - room.Z0) < 0.01f || MathF.Abs(d.Z - room.Z1) < 0.01f) && d.X > room.X0 && d.X < room.X1;
                Assert.True(onEdge, $"a {d.Kind} doorway at {d.X}, {d.Z} is not on the edge of {room.Name}");
            }
        }
    }

    [Fact]
    public void ItGoesHigherThanWhatIsAlreadyUpThere()
    {
        var r = Make("Crypt", "Swamp", 2, 1, 4);
        var origin = new Vector3(100, 5050, 100);
        Assert.Equal(origin, DungeonGen.Clear(origin, r, DungeonGen.Taken([], [])));
        // A crypt's entrance nearby that nobody entered yet: its dungeon will come 5000 m above it.
        var crypt = (new Vector3(130, 40, 120), StableHash.Of("Crypt3"));
        var raised = DungeonGen.Clear(origin, r, DungeonGen.Taken([], [crypt]));
        Assert.True(raised.Y + r.Items.Min(i => i.Position.Y) - 6 >= 40 + 5250 - 0.01f, $"raised to {raised.Y}");
        // A cave already laid out: its dungeon object keeps the space of its rooms, not just its point.
        var cave = (StableHash.Of("DG_Cave"), new Vector3(150, 5050, 140));
        Assert.True(DungeonGen.Clear(origin, r, DungeonGen.Taken([cave], [])).Y > origin.Y);
        // Other locations take nothing.
        Assert.Equal(origin, DungeonGen.Clear(origin, r, DungeonGen.Taken([], [(new Vector3(130, 40, 120), StableHash.Of("StoneTower1"))])));
    }

    [Fact]
    public void TheKeyChestHoldsTheKey()
    {
        var r = Make("Crypt", "Swamp", 2, 2, 7);
        var chest = r.Items.Single(i => i.Data?.Any(d => d.Key == "items") == true);
        var items = InventoryData.Read(Convert.FromBase64String(chest.Data!.Single(d => d.Key == "items").Value));
        Assert.Contains(items.Items, i => i.Prefab == StableHash.Of("CryptKey"));
        Assert.Contains(chest.Data!, d => d.Key == "addedDefaultItems" && d.Value == "1");
        Assert.Contains(r.Items, i => i.Prefab == "sunken_crypt_gate");
    }

    [Theory]
    [InlineData("Meadows")]
    [InlineData("Black Forest")]
    [InlineData("Swamp")]
    [InlineData("Mountain")]
    [InlineData("Plains")]
    [InlineData("Mistlands")]
    [InlineData("Ashlands")]
    [InlineData("Deep North")]
    public void EveryBiomesOwnRoomsCanBeGenerated(string biome)
    {
        var r = DungeonGen.Make(new DungeonGen.Settings(Made: DungeonGen.Made.Rooms, Biome: biome, Size: 2, Seed: 5));
        Assert.True(r.Rooms!.Count > 3);
    }

    [Fact]
    public void TheGamesRoomsCanBeGeneratedToo()
    {
        var r = DungeonGen.Make(new DungeonGen.Settings(Made: DungeonGen.Made.Rooms, Biome: "Mountain", Size: 2, Seed: 3));
        Assert.NotNull(r.Kind);
        Assert.True(r.Rooms!.Count > 5);
        Assert.Empty(Dungeons.Openings(r.Rooms, freeOnly: true));
    }

    /// <summary>
    /// A two-level Swamp crypt (its boss: a Draugr elite), with settings changed.
    /// </summary>
    private static DungeonGen.Result Crypt(Func<DungeonGen.Settings, DungeonGen.Settings>? change = null) =>
        DungeonGen.Make((change ?? (s => s))(new DungeonGen.Settings(Biome: "Swamp", Style: "Crypt", Size: 2, Levels: 2, Seed: 4)));

    /// <summary>
    /// The distinct prefabs of a dungeon's objects that pass the test.
    /// </summary>
    private static HashSet<string> Prefabs(DungeonGen.Result r, Func<string, bool> which) => [.. r.Items.Select(i => i.Prefab).Where(which)];

    [Fact]
    public void ChosenFoesAndBossTakeTheBiomesPlace()
    {
        static bool Spawner(string p) => p.StartsWith("Spawner_", StringComparison.Ordinal);
        Assert.Contains("Spawner_Blob", Prefabs(Crypt(), Spawner));
        // Foes by creature or spawner name; one of no biome is taken as it is.
        var foes = Prefabs(Crypt(s => s with { Foes = ["Draugr", "Spawner_MadeUp"] }), Spawner);
        Assert.Contains("Spawner_Draugr", foes);
        Assert.Contains("Spawner_MadeUp", foes);
        Assert.DoesNotContain("Spawner_Blob", foes);
        // The boss: the biome's, another, or none.
        Assert.Contains("Draugr_Elite", Prefabs(Crypt(), p => !Spawner(p)));
        var troll = Prefabs(Crypt(s => s with { Boss = "Troll" }), p => !Spawner(p));
        Assert.Contains("Troll", troll);
        Assert.DoesNotContain("Draugr_Elite", troll);
        Assert.DoesNotContain("Draugr_Elite", Prefabs(Crypt(s => s with { Boss = "" }), p => !Spawner(p)));
    }

    [Fact]
    public void FewerLightsWithoutLightAndLessRubbleWithoutDecay()
    {
        static int Lights(DungeonGen.Result r) => r.Items.Count(i => i.Prefab.Contains("torch", StringComparison.Ordinal) || i.Prefab.Contains("brazier", StringComparison.OrdinalIgnoreCase));
        var plain = Crypt();
        Assert.True(Lights(Crypt(s => s with { Light = 0 })) < Lights(plain));
        Assert.True(Crypt(s => s with { Decay = 0 }).Items.Count < plain.Items.Count);
        Assert.True(Crypt(s => s with { Decay = 1 }).Items.Count > plain.Items.Count);
    }

    [Fact]
    public void ASmallCryptWithoutRoomForItsArenaSaysSo()
    {
        var r = DungeonGen.Make(new DungeonGen.Settings(Biome: "Meadows", Style: "Crypt", Size: 1, Levels: 2, Seed: 1));
        Assert.Contains("no room for the boss arena", r.Notes);
    }

    [Fact]
    public void AKindWithNoRoomsToJoinIsMadeACave()
    {
        var r = DungeonGen.Make(new DungeonGen.Settings(Made: DungeonGen.Made.Rooms, Kind: "DG_GoblinCamp", Size: 2, Seed: 3));
        Assert.Equal("DG_Cave", r.Kind!.Name);
        Assert.Contains(r.Notes, n => n.Contains("a cave instead", StringComparison.Ordinal));
    }

    [Fact]
    public void TheGamesRoomsLootAndFoesFollowTheSettings()
    {
        static DungeonGen.Result Cave(Func<DungeonGen.Settings, DungeonGen.Settings> change) =>
            DungeonGen.Make(change(new DungeonGen.Settings(Made: DungeonGen.Made.Rooms, Biome: "Mountain", Size: 3, Seed: 3)));
        static int Chests(DungeonGen.Result r) => r.Items.Count(i => i.Prefab.StartsWith("TreasureChest_", StringComparison.Ordinal));
        int chests = Chests(Cave(s => s));
        Assert.True(chests > 0);
        // No loot: every chest taken; twice the loot: a second chest beside each.
        Assert.Equal(0, Chests(Cave(s => s with { Loot = 0 })));
        Assert.Equal(2 * chests, Chests(Cave(s => s with { Loot = 2 })));
        // Chosen foes in the rooms' spawners' places.
        var spawners = Cave(s => s with { Foes = ["Spawner_MadeUp"] }).Items.Where(i => i.Prefab.StartsWith("Spawner_", StringComparison.Ordinal)).Select(i => i.Prefab).Distinct();
        Assert.Equal(["Spawner_MadeUp"], spawners);
    }

    [Fact]
    public void AnythingElseHighUpTakesItsOwnSpotAndAnEmptyDungeonStaysPut()
    {
        var r = Make("Crypt", "Swamp", 2, 1, 4);
        var origin = new Vector3(100, 5000, 100);
        // A rock (not a dungeon) right where the dungeon would go: it goes higher.
        var taken = DungeonGen.Taken([(StableHash.Of("Rock_3"), origin)], []);
        Assert.Equal(new DungeonGen.Box(origin - Vector3.One, origin + Vector3.One), Assert.Single(taken));
        Assert.True(DungeonGen.Clear(origin, r, taken).Y > origin.Y);
        var empty = new DungeonGen.Result([], Vector3.Zero, null, null, []);
        Assert.Equal(origin, DungeonGen.Clear(origin, empty, taken));
    }

    [Fact]
    public void TheGamesRoomsAreLaidOutFromADungeonObjectWhenOneIsGiven()
    {
        // A Frost Cave from its dungeon object at the zone's middle, as the game lays its own out from the cave's
        // entrance: the first room's way in at the object, the rooms in world metres, in the zone's box.
        var pose = new DungeonEntrances.Pose(new Vector3(0, 5165.023f, -2654), Quaternion.Identity);
        var r = DungeonGen.Make(new DungeonGen.Settings(Made: DungeonGen.Made.Rooms, Biome: "Mountain", Size: 2, Seed: 3), 0, pose);
        Assert.Equal(pose, r.Dungeon);
        var first = r.Rooms![0];
        Assert.True(first.Room!.Entrance);
        Assert.True(Vector3.Distance(pose.Position, first.At(first.Room.Openings.First(o => o.Entrance)).Position) < 0.01f);
        Assert.All(r.Rooms, p => Assert.True(Dungeons.Inside(r.Kind!, pose.Position, p), p.Name));
        Assert.All(r.Items, i => Assert.True(i.Position.Y > 5000));
        Assert.True(Vector3.Distance(r.Arrival, pose.Position) < 20, r.Arrival.ToString());
        // From the origin: no dungeon object, the first room there, facing +z.
        var plain = DungeonGen.Make(new DungeonGen.Settings(Made: DungeonGen.Made.Rooms, Biome: "Mountain", Size: 2, Seed: 3));
        Assert.Null(plain.Dungeon);
        Assert.Equal(Vector3.Zero, plain.Rooms![0].Position);
    }

    [Fact]
    public void ManyLevelsAreAllMadeDownToTheBoss()
    {
        var r = DungeonGen.Make(new DungeonGen.Settings(Size: 1, Levels: 8, Seed: 21));
        Assert.Equal(Enumerable.Range(0, 8), r.Map!.Select(m => m.Level).Distinct().Order());
        Assert.Equal(7, r.Map!.First(m => m.Name == "Arena").Level);
        // Beyond the most: the most.
        Assert.Equal(DungeonGen.MaxLevels, DungeonGen.Make(new DungeonGen.Settings(Size: 1, Levels: 99, Seed: 21)).Map!.Select(m => m.Level).Distinct().Count());
    }
}
