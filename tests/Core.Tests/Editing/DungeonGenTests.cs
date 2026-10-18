using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;
using static WorldEditor.Tests.DungeonGenSupport;

namespace WorldEditor.Tests;

/// <summary>
/// Generated dungeons (DungeonGen) over the samples of every style and biome (the sample settings are the data the
/// DungeonGenHolds* classes check): the same settings make the same dungeon, and the holding checks cover every style.
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
}
