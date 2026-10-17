using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;
using static WorldEditor.Tests.DungeonGenSupport;

namespace WorldEditor.Tests;

/// <summary>
/// Generated dungeons (DungeonGen) over the samples of every style and biome (the sample settings are the data the
/// DungeonGenHolds* classes check): the same settings make the same dungeon, every room can be reached and the boss's key
/// before the boss's gate, and doorways join their rooms, not walls. The objects hold in the sky and are the game's own.
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
}
