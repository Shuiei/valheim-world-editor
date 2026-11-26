using System.Numerics;
using ValheimGen;
using Vector3 = System.Numerics.Vector3;
using ValheimWorldEditor.Sdk;
using Xunit;
using static ExpandWorld.WorldEditor.Tests.ExpandWorldEntries;

namespace ExpandWorld.WorldEditor.Tests;

/// <summary>
/// Expand World Prefabs' rules read for the Prefab rules tool (ExpandWorldMod.PrefabRulesOf): what starts them, what
/// they do, and which objects of an area they act on (ExpandWorldMod.Match, PrefabRuleTool.Marked).
/// </summary>
public class PrefabRuleToolTests
{
    /// <summary>An object at a place, with some int and string values.</summary>
    private sealed class Obj(string prefab, Vector3 at, Dictionary<string, object>? values = null) : IObjectView
    {
        public string Prefab => prefab;
        public Vector3 Position => at;
        public Vector3 Rotation => default;
        public Quaternion Orientation => Quaternion.Identity;
        public float Scale => 1;
        public bool TryGetInt(string key, out int value) => Get(key, out value);
        public bool TryGetLong(string key, out long value) => Get(key, out value);
        public bool TryGetFloat(string key, out float value) => Get(key, out value);
        public bool TryGetVector3(string key, out Vector3 value) => Get(key, out value);
        public bool TryGetQuaternion(string key, out Quaternion value) => Get(key, out value);
        public bool TryGetString(string key, out string value) => Get(key, out value!);
        public bool TryGetBytes(string key, out byte[] value) => Get(key, out value!);

        private bool Get<T>(string key, out T value)
        {
            if (values?.GetValueOrDefault(key) is T v)
            {
                value = v;
                return true;
            }
            value = default!;
            return false;
        }
    }

    /// <summary>An area of the given objects, all in one biome.</summary>
    private sealed class Area(int biome, params IObjectView[] objects) : IAreaView
    {
        public float MinX => -1000;
        public float MinZ => -1000;
        public float MaxX => 1000;
        public float MaxZ => 1000;
        public IReadOnlyList<IObjectView> ObjectsOf(string prefab) => [.. objects.Where(o => o.Prefab == prefab)];
        public float GroundHeight(float x, float z) => 30;
        public int Biome(float x, float z) => biome;
        public bool IsDeepNorth(float x, float z) => false;
        public bool IsAshlands(float x, float z) => false;
        public uint BiomeColor(int x, int z) => 0;
        public uint GroundMask(int x, int z) => 0;
    }

    private static readonly string[] Game = ["Boar", "Wolf", "Bonemass", "vfx_spawn", "blackmarble_1x1"];

    private static List<object?> L(params object?[] rows) => [.. rows];

    private static List<ExpandWorldMod.PrefabRule> Rules(params ExpandWorldMod.Entry[] entries) =>
        ExpandWorldMod.PrefabRulesOf(entries, Game.Contains, BiomeTable.Vanilla);

    private const int Meadows = (int)Heightmap.Biome.Meadows, Swamp = (int)Heightmap.Biome.Swamp;

    [Fact]
    public void ARuleSaysWhatStartsItAndWhatItDoes()
    {
        var rule = Assert.Single(Rules(E(("prefab", "Boar"), ("type", "create"), ("swap", "Bonemass"), ("spawn", L("vfx_spawn")), ("data", "angry"))));

        Assert.True(rule.OnCreate);
        Assert.Equal(["Boar"], rule.Prefabs);
        Assert.Equal(["swaps it for Bonemass", "spawns vfx_spawn", "sets data angry"], rule.Actions);
        Assert.Empty(rule.GameOnly);
        Assert.Equal("#1 create: Boar", rule.Title);
    }

    [Fact]
    public void DataEntriesAmongTheRulesAreNoRule()
    {
        Assert.Single(Rules(E(("name", "dichchuyen"), ("ints", L("a, 1"))), E(("prefab", "Wolf"), ("type", "destroy"), ("remove", "true"))));
    }

    [Fact]
    public void ARuleOnAGameEventIsMarkedAsTheGames()
    {
        var rule = Assert.Single(Rules(E(("prefab", "Player"), ("type", "say, check"), ("spawn", L("vfx_spawn")))));

        Assert.False(rule.OnCreate);
        Assert.Contains("in the game only", PrefabRuleTool.Describe(rule));
        Assert.Equal(["Player"], rule.Unlisted);
    }

    [Fact]
    public void WildcardsAndUnknownNamesAreNotMarked()
    {
        var rule = Assert.Single(Rules(E(("prefab", "Boar, Troll*, Modded"), ("type", "create"))));

        Assert.Equal(["Boar"], rule.Prefabs);
        Assert.Equal(["Troll*", "Modded"], rule.Unlisted);
    }

    [Fact]
    public void TheBiomeDistanceAndAltitudeAreChecked()
    {
        var rule = Assert.Single(Rules(E(("prefab", "Boar"), ("type", "create"), ("biomes", "Meadows"), ("minDistance", "0.05"), ("maxAltitude", "20"))));

        Assert.Equal(ExpandWorldMod.RuleMatch.Yes, ExpandWorldMod.Match(rule, new Obj("Boar", new(600, 40, 0)), Meadows));
        Assert.Equal(ExpandWorldMod.RuleMatch.No, ExpandWorldMod.Match(rule, new Obj("Boar", new(600, 40, 0)), Swamp));
        // 0.05 is a fraction of 10 000 m: 500 m.
        Assert.Equal(ExpandWorldMod.RuleMatch.No, ExpandWorldMod.Match(rule, new Obj("Boar", new(400, 40, 0)), Meadows));
        Assert.Equal(ExpandWorldMod.RuleMatch.No, ExpandWorldMod.Match(rule, new Obj("Boar", new(600, 55, 0)), Meadows));
        Assert.Equal(ExpandWorldMod.RuleMatch.No, ExpandWorldMod.Match(rule, new Obj("Wolf", new(600, 40, 0)), Meadows));
    }

    [Fact]
    public void ConditionsOnlyTheGameKnowsMakeAMaybe()
    {
        var rule = Assert.Single(Rules(E(("prefab", "Boar"), ("type", "create"), ("environments", "Rain"), ("chance", "0.5"), ("minDistance", "<radius>"))));

        Assert.Equal(["environments", "chance", "minDistance"], rule.GameOnly);
        Assert.Equal(ExpandWorldMod.RuleMatch.Maybe, ExpandWorldMod.Match(rule, new Obj("Boar", new(0, 40, 0)), Meadows));
    }

    [Fact]
    public void PlainDataFiltersAreCheckedOnTheObject()
    {
        var rule = Assert.Single(Rules(E(("prefab", "blackmarble_1x1"), ("type", "create"), ("filter", "int, sandichchuyen, 1"), ("bannedFilters", L("string, owner, bob")))));
        var marked = new Obj("blackmarble_1x1", default, new() { ["sandichchuyen"] = 1 });
        var other = new Obj("blackmarble_1x1", default, new() { ["sandichchuyen"] = 2 });
        var banned = new Obj("blackmarble_1x1", default, new() { ["sandichchuyen"] = 1, ["owner"] = "bob" });
        var none = new Obj("blackmarble_1x1", default);

        Assert.Equal(ExpandWorldMod.RuleMatch.Yes, ExpandWorldMod.Match(rule, marked, Meadows));
        Assert.Equal(ExpandWorldMod.RuleMatch.No, ExpandWorldMod.Match(rule, other, Meadows));
        Assert.Equal(ExpandWorldMod.RuleMatch.No, ExpandWorldMod.Match(rule, banned, Meadows));
        Assert.Equal(ExpandWorldMod.RuleMatch.Maybe, ExpandWorldMod.Match(rule, none, Meadows));
    }

    [Fact]
    public void TheAreasObjectsAreMarkedByTheSurestRule()
    {
        var rules = Rules(
            E(("prefab", "Boar"), ("type", "create"), ("chance", "0.5")),
            E(("prefab", "Boar"), ("type", "create"), ("biomes", "Meadows")),
            E(("prefab", "Wolf"), ("type", "create"), ("biomes", "Swamp")));
        var boar = new Obj("Boar", default);
        var wolf = new Obj("Wolf", default);

        var marked = PrefabRuleTool.Marked(new Area(Meadows, boar, wolf), rules);

        Assert.Equal([(boar, ExpandWorldMod.RuleMatch.Yes)], marked.Select(m => ((Obj)m.Object, m.Match)));
    }
}
