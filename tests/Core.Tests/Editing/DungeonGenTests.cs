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
                bool overlap = a.Level == b.Level && a.Floor.Any(p => b.Floor.Any(q => p.X0 < q.X1 - 0.01f && q.X0 < p.X1 - 0.01f && p.Z0 < q.Z1 - 0.01f && q.Z0 < p.Z1 - 0.01f));
                Assert.False(overlap && !(a.Corridor && b.Corridor), $"{a.Name} and {b.Name} overlap");
            }
        }
        foreach (var d in r.Doors!.Where(d => d.Kind != "Stairs"))
        {
            // On the edge of both rooms' boxes.
            foreach (var room in new[] { map[d.A], map[d.B] })
            {
                // On a side of one of its parts (a shaped room's doorway may straddle two parts along one wall).
                bool onEdge = room.Floor.Any(p => (MathF.Abs(d.X - p.X0) < 0.01f || MathF.Abs(d.X - p.X1) < 0.01f) && d.Z >= p.Z0 - 0.01f && d.Z <= p.Z1 + 0.01f
                    || (MathF.Abs(d.Z - p.Z0) < 0.01f || MathF.Abs(d.Z - p.Z1) < 0.01f) && d.X >= p.X0 - 0.01f && d.X <= p.X1 + 0.01f);
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
        // Small crypts of two levels: some seeds leave no room on the last level for the antechamber and the arena.
        var made = Enumerable.Range(1, 80).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Meadows", Style: "Crypt", Size: 1, Levels: 2, Seed: seed))).ToList();
        Assert.Contains(made, r => r.Notes.Contains("no room for the boss arena"));
        Assert.Contains(made, r => !r.Notes.Contains("no room for the boss arena"));
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

    [Fact]
    public void RoomsTakeShapesWithinTheirBoxesAndCorridorsBend()
    {
        // Over a few dungeons: some rooms are not their whole box (notched, an apse, an L, a neck), each part within
        // the box; some corridors bend (two parts); every doorway still lands on a wall (checked above for the samples).
        var made = Enumerable.Range(1, 6).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: "Catacombs", Size: 3, Levels: 1, Seed: seed))).ToList();
        var shaped = made.SelectMany(r => r.Map!).Where(m => !m.Corridor && m.Parts != null).ToList();
        Assert.True(shaped.Count >= 5, $"{shaped.Count} shaped rooms");
        Assert.All(shaped, m => Assert.All(m.Parts!, p => Assert.True(p.X0 >= m.X0 && p.X1 <= m.X1 && p.Z0 >= m.Z0 && p.Z1 <= m.Z1 && p.X1 > p.X0 && p.Z1 > p.Z0)));
        // A shaped room's floor is less than its box, never empty.
        Assert.All(shaped, m => Assert.InRange(m.Parts!.Sum(p => (p.X1 - p.X0) * (p.Z1 - p.Z0)), 16f, (m.X1 - m.X0) * (m.Z1 - m.Z0) - 3.9f));
        var bent = made.SelectMany(r => r.Map!).Where(m => m.Corridor && m.Parts is { Count: > 1 }).ToList();
        Assert.True(bent.Count >= 2, $"{bent.Count} bent corridors");
        // A room without parts floors its whole box.
        var whole = made.SelectMany(r => r.Map!).First(m => !m.Corridor && m.Parts == null);
        Assert.Equal([(whole.X0, whole.Z0, whole.X1, whole.Z1)], whole.Floor);
    }

    [Fact]
    public void RoomsOfAKindAreFurnishedInSeveralWays()
    {
        // The same crypt from different seeds: chapels with pews in some, a raised altar in others; burial chambers
        // in rows or on a dais; walls lit by sconces in some rooms, standing torches in others.
        var made = Enumerable.Range(1, 12).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: "Crypt", Size: 3, Levels: 2, Seed: seed))).ToList();
        HashSet<string> All(Func<DungeonGen.Result, bool> which) => [.. made.Where(which).SelectMany(r => r.Items.Select(i => i.Prefab))];
        var prefabs = All(_ => true);
        Assert.Contains("piece_bench01", prefabs);
        Assert.Contains("stone_floor_2x2", prefabs);
        Assert.Contains("stone_stair", prefabs);
        Assert.Contains("piece_walltorch", prefabs);
        Assert.Contains("CastleKit_groundtorch_green", prefabs);
        Assert.Contains("morkhalla_web_corner", prefabs);
        // Chapels: some with pews, some without; burial chambers: some with a dais, some without.
        static bool Holds(DungeonGen.Result r, DungeonGen.PlanRoom m, string prefab) =>
            r.Items.Any(i => i.Prefab == prefab && i.Position.X > m.X0 && i.Position.X < m.X1 && i.Position.Z > m.Z0 && i.Position.Z < m.Z1 && MathF.Abs(i.Position.Y - m.Y) < 3);
        var chapels = made.SelectMany(r => r.Map!.Where(m => m.Name == "Chapel").Select(m => Holds(r, m, "piece_bench01"))).ToList();
        Assert.Contains(true, chapels);
        Assert.Contains(false, chapels);
        var burials = made.SelectMany(r => r.Map!.Where(m => m.Name == "Burial chamber").Select(m => Holds(r, m, "stone_floor_2x2"))).ToList();
        Assert.Contains(true, burials);
        Assert.Contains(false, burials);
    }

    [Fact]
    public void GrandRoomsGetTheirOwnFloorsWithFurnitureOn()
    {
        // A fortress with Rich furniture: over a few seeds, marble tiles in its halls and boards in its quarters.
        var rich = Enumerable.Range(1, 8).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: "Fortress", Size: 3, Levels: 1, Seed: seed, Decor: 2)))
            .First(r => r.Items.Any(i => i.Prefab == "blackmarble_tile_floor_2x2") && r.Items.Any(i => i.Prefab == "wood_floor"));
        // Bare: the marble alone.
        var bare = DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: "Fortress", Size: 3, Levels: 1, Seed: 9, Decor: 0));
        Assert.DoesNotContain(bare.Items, i => i.Prefab is "blackmarble_tile_floor_2x2" or "wood_floor");
        // A chest (stone) on a wooden floor stands on the marble under it: not above the boards.
        var floors = rich.Items.Where(i => i.Prefab == "wood_floor").ToList();
        foreach (var chest in rich.Items.Where(i => i.Prefab.StartsWith("TreasureChest_", StringComparison.Ordinal)))
        {
            // The boards under it (the highest at or below it, not a loft's deck overhead): one standing too high is
            // checked too.
            var under = floors.Where(f => MathF.Abs(f.Position.X - chest.Position.X) < 1 && MathF.Abs(f.Position.Z - chest.Position.Z) < 1 && f.Position.Y <= chest.Position.Y + 0.01f)
                .MaxBy(f => f.Position.Y);
            if (under != null)
            {
                Assert.InRange(chest.Position.Y - under.Position.Y, -0.01f, 0.06f);
            }
        }
    }

    [Fact]
    public void TheLightAndDecaySettingsChangeNothingElse()
    {
        // The same seed, lit otherwise: everything that is not a light stands where it stood. Decayed otherwise:
        // everything that is not rubble (nor a thing decay breaks or puts out) stands where it stood.
        static HashSet<(string, Vector3)> Rest(DungeonGen.Result r, params string[] not) =>
            [.. r.Items.Where(i => !not.Any(n => i.Prefab.Contains(n, StringComparison.OrdinalIgnoreCase))).Select(i => (i.Prefab, i.Position))];
        static void Same(HashSet<(string, Vector3)> a, HashSet<(string, Vector3)> b, string what)
        {
            var differ = a.Except(b).Select(x => $"only without: {x}").Concat(b.Except(a).Select(x => $"only with: {x}")).ToList();
            Assert.True(differ.Count == 0, what + ": " + string.Join("; ", differ.Take(8)));
        }
        string[] lights = ["torch", "brazier", "lantern", "Candle"];
        foreach (string style in new[] { "Crypt", "Fortress", "Catacombs" })
        {
            var dark = DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: style, Size: 3, Levels: 1, Seed: 5, Light: 0));
            var bright = DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: style, Size: 3, Levels: 1, Seed: 5, Light: 2));
            Assert.NotEqual(dark.Items.Count, bright.Items.Count);
            Same(Rest(dark, lights), Rest(bright, lights), style + " light");
            string[] decay = ["Rubble", "Stonepile", "_ruin", "_cracked", "web", "Statue", "stone_pillar", "torch", "brazier", "Pickable_StoneRock"];
            var kept = DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: style, Size: 3, Levels: 1, Seed: 5, Decay: 0));
            var ruined = DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: style, Size: 3, Levels: 1, Seed: 5, Decay: 1));
            Assert.True(kept.Items.Count < ruined.Items.Count);
            Same(Rest(kept, decay), Rest(ruined, decay), style + " decay");
        }
    }

    [Fact]
    public void AlcovesAndNichesAreFurnished()
    {
        // Over a few crypts and catacombs: rooms with alcoves (two tiles wide) and corridors with niches (one), each
        // with something standing in it, well inside (nothing in the room's walls).
        var made = Enumerable.Range(1, 10).SelectMany(seed => new[] { "Crypt", "Catacombs" }.Select(style =>
            DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: style, Size: 3, Levels: 1, Seed: seed)))).ToList();
        var alcoves = made.SelectMany(r => r.Map!.Where(m => !m.Corridor && m.Nooks != null).SelectMany(m => m.Nooks!.Select(n => (r, m, n)))).ToList();
        var niches = made.SelectMany(r => r.Map!.Where(m => m.Corridor && m.Nooks != null).SelectMany(m => m.Nooks!.Select(n => (r, m, n)))).ToList();
        Assert.True(alcoves.Count >= 4, $"{alcoves.Count} alcoves");
        Assert.True(niches.Count >= 4, $"{niches.Count} niches");
        Assert.All(alcoves, a => Assert.True(MathF.Max(a.n.X1 - a.n.X0, a.n.Z1 - a.n.Z0) == 4 && MathF.Min(a.n.X1 - a.n.X0, a.n.Z1 - a.n.Z0) == 2, "an alcove is two tiles along the wall"));
        Assert.All(niches, a => Assert.True(a.n.X1 - a.n.X0 == 2 && a.n.Z1 - a.n.Z0 == 2, "a niche is one tile"));
        static bool Furnished((DungeonGen.Result R, DungeonGen.PlanRoom M, (float X0, float Z0, float X1, float Z1) N) a) =>
            a.R.Items.Any(i => !i.Prefab.StartsWith("stone_wall", StringComparison.Ordinal) && !i.Prefab.Contains("web", StringComparison.Ordinal)
                && i.Position.X > a.N.X0 + 0.2f && i.Position.X < a.N.X1 - 0.2f && i.Position.Z > a.N.Z0 + 0.2f && i.Position.Z < a.N.Z1 - 0.2f
                && i.Position.Y > a.M.Y - 0.5f && i.Position.Y < a.M.Y + 1.5f);
        Assert.True(alcoves.Count(Furnished) >= alcoves.Count * 3 / 4, $"{alcoves.Count(Furnished)} of {alcoves.Count} alcoves furnished");
        Assert.True(niches.Count(Furnished) >= niches.Count * 3 / 4, $"{niches.Count(Furnished)} of {niches.Count} niches furnished");
    }

    [Fact]
    public void TallRoomsGetLoftsAndRoomsGetPitsVaultsAndGatedCorridors()
    {
        // Over a few keeps and crypts: a loft over the way in of some great hall or throne room (its rail and the top
        // of its stairs 3 m up), a sunken shrine or arena (ledges a metre up round the room), a burial vault (walls of
        // the material well inside the room's box), a corridor closed by a gate midway, bones along another.
        // Lofts are rare (a tall room 12 m deep with free sides, half the time): 24 seeds of each.
        var keeps = Enumerable.Range(1, 24).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: "Fortress", Size: 3, Levels: 1, Seed: seed))).ToList();
        var crypts = Enumerable.Range(1, 24).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: "Crypt", Size: 3, Levels: 2, Seed: seed))).ToList();
        static IEnumerable<DungeonGen.Item> In(DungeonGen.Result r, DungeonGen.PlanRoom m, float inset = 0) =>
            r.Items.Where(i => i.Position.X > m.X0 + inset && i.Position.X < m.X1 - inset && i.Position.Z > m.Z0 + inset && i.Position.Z < m.Z1 - inset && i.Position.Y >= m.Y - 0.5f && i.Position.Y < m.Y + 11);
        var lofts = keeps.Concat(crypts).SelectMany(r => r.Map!.Where(m => m.Name is "Great hall" or "Throne room" or "Chapel").Select(m => In(r, m).ToList()))
            .Where(items => items.Any(i => i.Prefab == "stone_fence") && items.Any(i => i.Prefab == "stone_stair" && MathF.Abs(i.Position.Y % 6 - 3.05f) < 0.1f || i.Prefab == "stone_stair" && MathF.Abs(i.Position.Y % 6 + 6 - 3.05f) < 0.1f)).ToList();
        Assert.True(lofts.Count >= 2, $"{lofts.Count} lofts");
        // A loft's rail runs the room's width less the stairs' tile: at least 3 pieces of fence, 4 m up.
        Assert.All(lofts, items => Assert.True(items.Count(i => i.Prefab == "stone_fence") >= 3));
        var pits = crypts.SelectMany(r => r.Map!.Where(m => m.Name is "Shrine" or "Arena").Select(m => In(r, m).Count(i => i.Prefab == "stone_floor_2x2" && MathF.Abs(i.Position.Y - m.Y - 0.55f) < 0.05f))).ToList();
        Assert.True(pits.Any(n => n >= 8), $"ledges: {string.Join(",", pits)}");
        var vaults = crypts.SelectMany(r => r.Map!.Where(m => m.Name == "Burial chamber").Select(m => In(r, m, 1.2f).Count(i => i.Prefab.StartsWith("stone_wall_", StringComparison.Ordinal) && !i.Prefab.EndsWith("_ruin", StringComparison.Ordinal)))).ToList();
        Assert.True(vaults.Any(n => n >= 4), $"vault walls: {string.Join(",", vaults)}");
        var corridors = crypts.SelectMany(r => r.Map!.Where(m => m.Corridor).Select(m => In(r, m).Select(i => i.Prefab).ToList())).ToList();
        Assert.True(corridors.Any(p => p.Any(n => n.StartsWith("dungeon_sunkencrypt_irongate", StringComparison.Ordinal))), "no gated corridor");
        Assert.True(corridors.Any(p => p.Count(n => n is "skull_pile" or "bone_stack") >= 4), $"bones: {string.Join(",", corridors.Select(p => p.Count(n => n is "skull_pile" or "bone_stack")))}");
    }

    [Fact]
    public void DeadEndsHoldACacheAndSomeRoomsStandNearlyBare()
    {
        // Over a few crypts: some corridors lead nowhere (one doorway, no stairs) and each ends in the biome's chest;
        // and a few rooms of the ordinary kinds stand nearly bare: one piece, a light, little else.
        var crypts = Enumerable.Range(1, 16).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: "Crypt", Size: 3, Levels: 2, Seed: seed))).ToList();
        static IEnumerable<DungeonGen.Item> In(DungeonGen.Result r, DungeonGen.PlanRoom m) =>
            r.Items.Where(i => i.Position.X > m.X0 && i.Position.X < m.X1 && i.Position.Z > m.Z0 && i.Position.Z < m.Z1 && i.Position.Y >= m.Y - 0.5f && i.Position.Y < m.Y + 11);
        var deadEnds = new List<(DungeonGen.Result R, DungeonGen.PlanRoom M)>();
        foreach (var r in crypts)
        {
            var map = r.Map!.ToList();
            for (int k = 0; k < map.Count; k++)
            {
                if (map[k].Corridor && r.Doors!.Count(d => d.A == k || d.B == k) == 1)
                {
                    deadEnds.Add((r, map[k]));
                }
            }
        }
        Assert.True(deadEnds.Count >= 3, $"{deadEnds.Count} dead ends");
        Assert.All(deadEnds, e => Assert.Contains(In(e.R, e.M), i => i.Prefab.StartsWith("TreasureChest_", StringComparison.Ordinal)));
        // Nearly bare: of what is not the shell (walls, slabs, columns, beams, doors) nor light, cobweb or rubble, at most five things.
        static bool Furniture(DungeonGen.Item i) =>
            !i.Prefab.StartsWith("stone_wall", StringComparison.Ordinal) && !i.Prefab.StartsWith("blackmarble", StringComparison.Ordinal) && i.Prefab != "stone_pillar"
            && !i.Prefab.StartsWith("dungeon_", StringComparison.Ordinal) && !i.Prefab.Contains("torch", StringComparison.OrdinalIgnoreCase) && !i.Prefab.Contains("brazier", StringComparison.OrdinalIgnoreCase)
            && !i.Prefab.Contains("web", StringComparison.Ordinal) && !i.Prefab.Contains("Rubble", StringComparison.Ordinal) && !i.Prefab.StartsWith("Spawner_", StringComparison.Ordinal);
        var ordinary = crypts.SelectMany(r => r.Map!.Where(m => !m.Corridor && m.Name is "Great hall" or "Gallery" or "Burial chamber" or "Ossuary" or "Tomb").Select(m => In(r, m).Count(Furniture))).ToList();
        Assert.True(ordinary.Count(n => n <= 5) >= 2, $"bare rooms: {string.Join(",", ordinary.Order().Take(10))}");
        Assert.True(ordinary.Count(n => n > 8) >= ordinary.Count / 2, "most rooms are full");
    }

    [Fact]
    public void SideRoomsGrowInWingsOfTheirKinAndDeeperLevelsHoldMore()
    {
        // Over a few keeps: side rooms stand beside their kin (a kitchen by the feast hall, cells by the guard room)
        // far more often than chance would put them; and a level down holds more loot and foes than the one above.
        var keeps = Enumerable.Range(1, 12).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Style: "Fortress", Size: 3, Levels: 2, Seed: seed))).ToList();
        var kinds = DungeonKit.Rooms.ToDictionary(r => r.Name, r => r.Room);
        int kin = 0, total = 0;
        foreach (var r in keeps)
        {
            var map = r.Map!.ToList();
            foreach (var d in r.Doors!.Where(d => d.Kind != "Stairs"))
            {
                var (a, b) = (map[d.A], map[d.B]);
                if (a.Corridor || b.Corridor || a.Main || b.Main)
                {
                    continue;
                }
                total++;
                if (DungeonPlanner.Kin(kinds[a.Name]).Contains(kinds[b.Name]) || DungeonPlanner.Kin(kinds[b.Name]).Contains(kinds[a.Name]))
                {
                    kin++;
                }
            }
        }
        // Loops and twins join rooms as they lie; the wings still make kin of a good third of these doorways.
        Assert.True(total >= 10 && kin * 3 >= total, $"{kin} of {total} side-to-side doorways join kin");
        // Things on the level's own floor (or a loft over it): below the floor of the level above, 6 m up, so a room
        // under another level's does not count that level's things.
        static float PerRoom(IEnumerable<DungeonGen.Result> rs, int level, Func<DungeonGen.Item, bool> what) =>
            (float)rs.Sum(r => r.Items.Count(i => what(i) && r.Map!.Any(m => m.Level == level && i.Position.X > m.X0 && i.Position.X < m.X1 && i.Position.Z > m.Z0 && i.Position.Z < m.Z1 && i.Position.Y >= m.Y - 0.5f && i.Position.Y < m.Y + 5.5f)))
            / Math.Max(1, rs.Sum(r => r.Map!.Count(m => m.Level == level && !m.Corridor)));
        Assert.True(PerRoom(keeps, 1, i => i.Prefab.StartsWith("Pickable_", StringComparison.Ordinal)) > PerRoom(keeps, 0, i => i.Prefab.StartsWith("Pickable_", StringComparison.Ordinal)), "deeper levels hold more loot");
    }

    [Fact]
    public void OneArrivesOnTheEntrancesFloorFinish()
    {
        // The arrival was 5 cm over the slab whatever lay on it: a portal there stood in the boards or marble tiles.
        // Wooden walls come with boards 0.1 m thick everywhere.
        Assert.Equal(0.15f, DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Walls: "Wood", Seed: 1)).Arrival.Y, 0.001f);
        // Stone has none, but grand rooms get marble tiles (0.19 m) half the time.
        float[] heights = [.. Enumerable.Range(1, 8).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Size: 1, Seed: seed)).Arrival.Y)];
        Assert.All(heights, y => Assert.True(MathF.Abs(y - 0.05f) < 0.001f || MathF.Abs(y - 0.24f) < 0.001f, $"arrival at {y}"));
        Assert.Contains(heights, y => MathF.Abs(y - 0.24f) < 0.001f);
    }
}
