using System.Numerics;
using ValheimGen;
using ValheimWorldEditor.Sdk;
using Xunit;
using static ExpandWorld.WorldEditor.Tests.ExpandWorldEntries;
using Vector3 = System.Numerics.Vector3;

namespace ExpandWorld.WorldEditor.Tests;

/// <summary>
/// Expand World Prefabs' "create" rules applied as objects are made (ExpandWorldMod.PlanOf through PrefabRulesOf,
/// ApplyCreateRules): which rules the editor applies and why not, swaps, spawns, removals and data, the draws among
/// rules, and that a batch made again comes out the same.
/// </summary>
public class PrefabCreationRulesTests
{
    /// <summary>An object being made: its values by key, its place and turn.</summary>
    private sealed class Made(string prefab, Vector3 at) : ICopy
    {
        public Dictionary<string, object?> Values { get; } = [];
        public string Prefab => prefab;
        public Vector3 Position => at;
        public Vector3 Rotation => default;
        public Quaternion Orientation { get; private set; } = Quaternion.Identity;
        public float Scale => 1;
        public bool TryGetInt(string key, out int value) => Get(key, out value);
        public bool TryGetLong(string key, out long value) => Get(key, out value);
        public bool TryGetFloat(string key, out float value) => Get(key, out value);
        public bool TryGetVector3(string key, out Vector3 value) => Get(key, out value);
        public bool TryGetQuaternion(string key, out Quaternion value) => Get(key, out value);
        public bool TryGetString(string key, out string value) => Get(key, out value!);
        public bool TryGetBytes(string key, out byte[] value) => Get(key, out value!);
        public void SetInt(string key, int? value) => Values[key] = value;
        public void SetLong(string key, long? value) => Values[key] = value;
        public void SetFloat(string key, float? value) => Values[key] = value;
        public void SetVector3(string key, Vector3? value) => Values[key] = value;
        public void SetQuaternion(string key, Quaternion? value) => Values[key] = value;
        public void SetString(string key, string? value) => Values[key] = value;
        public void SetBytes(string key, byte[]? value) => Values[key] = value;
        public void SetTurn(Quaternion turn) => Orientation = turn;

        private bool Get<T>(string key, out T value)
        {
            if (Values.GetValueOrDefault(key) is T v)
            {
                value = v;
                return true;
            }
            value = default!;
            return false;
        }
    }

    /// <summary>A batch in Meadows on flat ground at 30 m.</summary>
    private sealed class Batch(int seed, params Made[] objects) : ICreation
    {
        public List<Made> Left { get; } = [];
        public List<Made> Added { get; } = [];
        public IReadOnlyList<ICopy> Objects => objects;
        public int Seed => seed;
        public void Remove(ICopy obj) => Left.Add((Made)obj);

        public ICopy Add(string prefab, Vector3 position, Vector3 rotation)
        {
            var made = new Made(prefab, position);
            Added.Add(made);
            return made;
        }

        public int Biome(float x, float z) => (int)Heightmap.Biome.Meadows;
        public float GroundHeight(float x, float z) => 30;
    }

    private static readonly string[] Game = ["Beech1", "Oak1", "Rock_4", "vfx_spawn", "Birch1"];

    private static List<object?> L(params object?[] rows) => [.. rows];

    private static List<ExpandWorldMod.PrefabRule> Rules(params ExpandWorldMod.Entry[] entries) =>
        ExpandWorldMod.PrefabRulesOf(entries, Game.Contains, BiomeTable.Vanilla);

    /// <summary>A data entry "marked": an int "marked" of 1.</summary>
    private static Dictionary<string, ExpandWorldMod.CustomData> Entries() =>
        ExpandWorldMod.CustomDataOf([E(("name", "marked"), ("ints", L("marked, 1")))], [], Game.Contains).ToDictionary(e => e.Name);

    private static Batch Apply(List<ExpandWorldMod.PrefabRule> rules, int seed = 1, params Made[] objects)
    {
        var batch = new Batch(seed, objects);
        ExpandWorldMod.ApplyCreateRules(batch, rules, Entries());
        return batch;
    }

    [Fact]
    public void ASwapRemovesTheObjectAndMakesTheOtherTurnedWithIt()
    {
        var rules = Rules(E(("prefab", "Beech1"), ("type", "create"), ("swap", "Oak1, 0,1,2, 90,0,0, marked")));
        var plan = Assert.Single(rules).Create;
        Assert.NotNull(plan);
        Assert.True(plan.Remove);

        var beech = new Made("Beech1", new Vector3(10, 30, 0));
        var batch = Apply(rules, objects: beech);

        Assert.Equal([beech], batch.Left);
        var oak = Assert.Single(batch.Added);
        Assert.Equal("Oak1", oak.Prefab);
        // x, z, y: 0 east, 1 north, 2 up.
        Assert.Equal(new Vector3(10, 32, 1), oak.Position);
        Assert.Equal(1, oak.Values["marked"]);
        Assert.True(Quaternion.Dot(Mathf.Euler(new Vector3(0, 90, 0)), oak.Orientation) > 0.999f);
    }

    [Fact]
    public void ASpawnKeepsTheObjectAndDataIsGivenToIt()
    {
        var rules = Rules(E(("prefab", "Rock_4"), ("type", "create"), ("spawn", L("vfx_spawn")), ("data", "marked")));
        var rock = new Made("Rock_4", new Vector3(5, 30, 5));

        var batch = Apply(rules, objects: rock);

        Assert.Empty(batch.Left);
        Assert.Equal(["vfx_spawn"], batch.Added.Select(a => a.Prefab));
        Assert.Equal(1, rock.Values["marked"]);
    }

    [Fact]
    public void RulesTheEditorCannotCheckOrDoAreNotApplied()
    {
        var rules = Rules(
            E(("prefab", "Beech1"), ("type", "create"), ("environments", "Rain"), ("remove", "true")),
            E(("prefab", "Beech1"), ("type", "destroy"), ("remove", "true")),
            E(("prefab", "Beech1"), ("type", "create"), ("spawn", L("MyBlueprint"))),
            E(("prefab", "Beech1"), ("type", "create"), ("remove", "<par>")));

        Assert.All(rules, r => Assert.Null(r.Create));
        Assert.Equal(["only the game checks environments", "it runs while the game is played",
            "it makes MyBlueprint, no object the editor can place (a blueprint, an effect or a mod's)", "\"remove: <par>\" is the game's to work out"],
            rules.Select(r => r.NotApplied));
        Assert.Empty(Apply(rules, objects: new Made("Beech1", default)).Left);
    }

    [Fact]
    public void AChanceOfNothingDoesNothingAndAChanceIsAppliedRule()
    {
        var never = Rules(E(("prefab", "Beech1"), ("type", "create"), ("remove", "true"), ("chance", "0")));

        Assert.NotNull(Assert.Single(never).Create);
        Assert.Empty(Apply(never, objects: new Made("Beech1", default)).Left);
    }

    [Fact]
    public void OneRuleIsDrawnSeparateOnesAllActAndAFallbackOnlyAlone()
    {
        var rules = Rules(
            E(("prefab", "Beech1"), ("type", "create"), ("swap", "Oak1")),
            E(("prefab", "Beech1"), ("type", "create"), ("swap", "Birch1")),
            E(("prefab", "Beech1"), ("type", "create"), ("spawn", "vfx_spawn"), ("separate", "true")),
            E(("prefab", "Beech1, Rock_4"), ("type", "create"), ("data", "marked"), ("fallback", "true")));
        var beech = new Made("Beech1", new Vector3(3, 30, 4));
        var rock = new Made("Rock_4", new Vector3(6, 30, 4));

        var batch = Apply(rules, 7, beech, rock);

        var added = batch.Added.Select(a => a.Prefab).ToList();
        Assert.Equal(2, added.Count);
        Assert.Contains("vfx_spawn", added);
        Assert.Single(added, a => a is "Oak1" or "Birch1");
        Assert.False(beech.Values.ContainsKey("marked"));
        Assert.Equal(1, rock.Values["marked"]);
        // The same seed and places: the same draw.
        var again = Apply(rules, 7, new Made("Beech1", new Vector3(3, 30, 4)), new Made("Rock_4", new Vector3(6, 30, 4)));
        Assert.Equal(added, again.Added.Select(a => a.Prefab));
    }

    [Fact]
    public void ConditionsAreCheckedBeforeARuleActs()
    {
        var rules = Rules(E(("prefab", "Beech1"), ("type", "create"), ("remove", "true"), ("biomes", "Swamp")));

        Assert.Empty(Apply(rules, objects: new Made("Beech1", default)).Left);
    }
}
