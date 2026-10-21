using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// One catalogue entry's rules, added to the catalogue being built: its item rules, its spawner, and what a sapling grows
/// into (keeping the largest grow radius).
/// </summary>
public class PrefabCatalogEntriesTests
{
    /// <summary>An empty catalogue being built.</summary>
    private static PrefabCatalog.Data Empty() => new([], [], [], [], [], [], [], [], [], [], []);

    /// <summary>An entry with only the settings the test names; the rest left as the catalogue has them when absent.</summary>
    private static PrefabCatalog.Entry Entry(int p = 1, int d = 0, int t = 0, double gr = 0, string[]? grows = null, int it = -1, int at = 0, int ats = 0,
        double dur = 0, double durl = 0, int ms = 1, int[]? @is = null, string[]? isi = null, int[][]? @as = null, string[]? cs = null, double rt = 0,
        int[]? lv = null, int day = 1, int night = 1) =>
        new(p, d, t, gr: gr, grows: grows, it: it, at: at, ats: ats, dur: dur, durl: durl, ms: ms, @is: @is, isi: isi, @as: @as, cs: cs, rt: rt, lv: lv, day: day, night: night);

    [Fact]
    public void AnItemIsItsKindWithItsWearAndStack()
    {
        var d = Empty();

        PrefabCatalog.AddItemRules(d, 7, Entry(it: 2, at: 1, dur: 100, durl: 10, ms: 20));

        var kind = d.ItemKinds[7];
        Assert.Equal(2, kind.Type);
        Assert.True(kind.Attach);
        Assert.False(kind.AttachSkin);
        Assert.Equal(100f, kind.Durability);
        Assert.Equal(20, kind.MaxStack);
    }

    [Fact]
    public void AnItemStandTakesItsTypesAndItsNamedItems()
    {
        var d = Empty();

        PrefabCatalog.AddItemRules(d, 8, Entry(@is: [4, 5], isi: ["Coins"]));

        var rule = d.ItemStands[8];
        Assert.Equal(new HashSet<int>([4, 5]), rule.Types);
        Assert.Equal(new HashSet<string>(["Coins"]), rule.Takes);
        Assert.Empty(rule.Refuses);
    }

    [Fact]
    public void AnArmourStandKeepsItsSlots()
    {
        var d = Empty();

        PrefabCatalog.AddItemRules(d, 9, Entry(@as: [[1, 2], []]));

        int[][] expected = [[1, 2], []];
        Assert.Equal(expected, d.ArmourStands[9]);
    }

    [Fact]
    public void AnEntryWithoutItemRulesAddsNone()
    {
        var d = Empty();

        PrefabCatalog.AddItemRules(d, 10, Entry());

        Assert.Empty(d.ItemKinds);
        Assert.Empty(d.ItemStands);
        Assert.Empty(d.ArmourStands);
    }

    [Fact]
    public void ASpawnPointIsRecordedWithItsCreatureAndRespawn()
    {
        var d = Empty();

        PrefabCatalog.AddSpawn(d, 11, Entry(cs: ["Boar"], rt: 60, lv: [1, 3]));

        var spawn = d.Spawns[11];
        Assert.Equal(["Boar"], spawn.Creatures);
        Assert.Equal(60f, spawn.RespawnMinutes);
        Assert.Equal((1, 3), spawn.Levels);
        Assert.False(spawn.Nest);
    }

    [Fact]
    public void ANestIsRecordedAsANestWithItsCountAndRadius()
    {
        var d = Empty();

        PrefabCatalog.AddSpawn(d, 12, new PrefabCatalog.Entry(1, 0, 0, sa: ["Greydwarf"], sr: 5, max: 3));

        var nest = d.Spawns[12];
        Assert.True(nest.Nest);
        Assert.Equal(3, nest.MaxNear);
        Assert.Equal(5f, nest.Radius);
    }

    [Fact]
    public void AnEntryWithNoSpawnerAddsNone()
    {
        var d = Empty();

        PrefabCatalog.AddSpawn(d, 13, Entry());

        Assert.Empty(d.Spawns);
    }

    [Fact]
    public void ASaplingsGrownKindKeepsTheLargestGrowRadius()
    {
        var d = Empty();

        PrefabCatalog.AddGrownFrom(d, "Sapling", Entry(gr: 2, grows: ["Tree"]));
        PrefabCatalog.AddGrownFrom(d, "Smaller", Entry(gr: 1, grows: ["Tree"]));
        PrefabCatalog.AddGrownFrom(d, "Larger", Entry(gr: 3, grows: ["Tree"]));

        Assert.Equal((3f, "Larger"), d.GrownFrom["Tree"]);
    }

    [Fact]
    public void AnEntryWithNoGrownKindAddsNone()
    {
        var d = Empty();

        PrefabCatalog.AddGrownFrom(d, "Rock", Entry(gr: 2));

        Assert.Empty(d.GrownFrom);
    }
}
