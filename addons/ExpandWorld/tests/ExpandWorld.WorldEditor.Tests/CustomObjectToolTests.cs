using System.Numerics;
using ValheimWorldEditor.Sdk;
using Xunit;
using static ExpandWorld.WorldEditor.Tests.ExpandWorldEntries;

namespace ExpandWorld.WorldEditor.Tests;

/// <summary>
/// The Expand World mods' data entries read for the Custom object tool (ExpandWorldMod.CustomDataOf): the values the
/// editor sets, those it leaves to the mod, the objects Expand World Prefabs' rules use an entry on, and the values set
/// on an object (CustomObjectTool.Apply).
/// </summary>
public class CustomObjectToolTests
{
    /// <summary>An object that keeps what is set on it, by key.</summary>
    private sealed class Recorder : ICopy
    {
        public Dictionary<string, object?> Set { get; } = [];
        public string Prefab => "blackmarble_1x1";
        public Vector3 Position => default;
        public Vector3 Rotation => default;
        public Quaternion Orientation => Quaternion.Identity;
        public float Scale => 1;
        public bool TryGetInt(string key, out int value) => None(out value);
        public bool TryGetLong(string key, out long value) => None(out value);
        public bool TryGetFloat(string key, out float value) => None(out value);
        public bool TryGetVector3(string key, out Vector3 value) => None(out value);
        public bool TryGetQuaternion(string key, out Quaternion value) => None(out value);
        public bool TryGetString(string key, out string value)
        {
            value = "";
            return false;
        }

        public bool TryGetBytes(string key, out byte[] value)
        {
            value = [];
            return false;
        }

        /// <summary>It holds no value to read: the default, and false.</summary>
        private static bool None<T>(out T value) where T : struct
        {
            value = default;
            return false;
        }

        public void SetInt(string key, int? value) => Set[key] = value;
        public void SetLong(string key, long? value) => Set[key] = value;
        public void SetFloat(string key, float? value) => Set[key] = value;
        public void SetVector3(string key, Vector3? value) => Set[key] = value;
        public void SetQuaternion(string key, Quaternion? value) => Set[key] = value;
        public void SetString(string key, string? value) => Set[key] = value;
        public void SetBytes(string key, byte[]? value) => Set[key] = value;
        public void SetTurn(Quaternion turn) => Set["turn"] = turn;
    }

    private static Dictionary<string, object?> Map(params (string Key, object? Value)[] fields) => fields.ToDictionary(f => f.Key, f => f.Value);

    private static List<object?> L(params object?[] rows) => [.. rows];

    private static readonly string[] Game = ["blackmarble_1x1", "vfx_spawn", "Bonemass", "Chest", "Boar", "Player"];

    private static bool Known(string name) => Game.Contains(name);

    /// <summary>A flattened, unbreakable, renamed marble block a rule looks for near a player who says "check".</summary>
    private static ExpandWorldMod.Entry Teleporter() => E(("name", "dichchuyen"), ("ints", L("sandichchuyen, 1")), ("strings", L("Piece.m_name, Dịch Chuyển")),
        ("floats", L("WearNTear.m_health, -1", "health, -1")), ("vecs", L("scale, 0.01,1,1")));

    [Fact]
    public void AnEntrysValuesAreSetAsTheGameReadsThem()
    {
        var data = Assert.Single(ExpandWorldMod.CustomDataOf([Teleporter()], [], Known));
        var obj = new Recorder();

        CustomObjectTool.Apply(data, obj);

        Assert.Equal(("dichchuyen", 5), (data.Name, data.Values.Count));
        Assert.Empty(data.Skipped);
        Assert.Equal(1, obj.Set["sandichchuyen"]);
        Assert.Equal("Dịch Chuyển", obj.Set["Piece.m_name"]);
        Assert.Equal((-1f, -1f), (obj.Set["WearNTear.m_health"], obj.Set["health"]));
        // The mods write a vector x, z, y.
        Assert.Equal(new Vector3(0.01f, 1, 1), obj.Set["scale"]);
        // A component's field is read by the game only from an object marked as having fields, and that component's.
        Assert.Equal((1, 1, 1), (obj.Set["HasFields"], obj.Set["HasFieldsPiece"], obj.Set["HasFieldsWearNTear"]));
    }

    [Fact]
    public void EveryKindOfValueIsSet()
    {
        var entry = E(("name", "all"), ("bools", L("open, true")), ("hashes", L("item, Wood")), ("longs", L("creator, 123456789012")), ("strings", "text, \"a, b\""),
            ("vecs", L("spawnpoint, 1,2,3", "scale, 2")), ("quats", L("turn, 90,0,0")), ("bytes", L("blob, AQID")));
        var data = Assert.Single(ExpandWorldMod.CustomDataOf([entry], [], Known));
        var obj = new Recorder();

        CustomObjectTool.Apply(data, obj);

        Assert.Empty(data.Skipped);
        Assert.Equal((1, GameHash.Of("Wood"), 123456789012L, "a, b"), (obj.Set["open"], obj.Set["item"], obj.Set["creator"], obj.Set["text"]));
        // x, z, y; one number alone is a size on every axis.
        Assert.Equal((new Vector3(1, 3, 2), new Vector3(2, 2, 2)), (obj.Set["spawnpoint"], obj.Set["scale"]));
        // A turn is written y, x, z: 90° round the vertical.
        var turn = Assert.IsType<Quaternion>(obj.Set["turn"]);
        Assert.Equal(0.7071f, turn.Y, 3);
        Assert.Equal(new byte[] { 1, 2, 3 }, obj.Set["blob"]);
    }

    [Fact]
    public void WhatTheModWorksOutAsItMakesTheObjectIsLeftOut()
    {
        var entry = E(("name", "dynamic"), ("ints", L("level, <level>", "stars, 1, 2, 3", "good, 2", "123, 4")), ("floats", L("health, 10;20")), ("strings", L("$anim, run")),
            ("items", L(Map(("prefab", "Wood")))), ("containerSize", "2,2"));

        var data = Assert.Single(ExpandWorldMod.CustomDataOf([entry], [], Known));

        Assert.Equal(["good"], data.Values.Select(v => v.Key));
        Assert.Equal(["level", "stars", "123", "health", "$anim", "items", "containerSize"], data.Skipped);
    }

    [Fact]
    public void TheRulesNameTheObjectsAnEntryIsUsedOn()
    {
        var chest = E(("name", "rich"), ("ints", L("level, 3")));
        var boss = E(("name", "ultra"), ("floats", L("health, 10000")));
        var unused = E(("name", "unused"), ("ints", L("x, 1")));
        ExpandWorldMod.Entry[] rules =
        [
            // Asks for a nearby object by a value the entry sets: the object the entry is for.
            E(("prefab", "Player"), ("type", "say, check"), ("objects", L(Map(("prefab", "blackmarble_1x1"), ("filter", "int, sandichchuyen, 1"), ("maxDistance", "10")))),
                ("spawn", L(Map(("prefab", "vfx_spawn"))))),
            // Sets an entry on its own objects; a wildcard and a name of no object are none to place.
            E(("prefab", "Bonemass, Trophy*, NoSuchThing"), ("type", "create"), ("data", "ultra")),
            // Spawns an object with an entry, as a map and as a line.
            E(("prefab", "Boar"), ("type", "destroy"), ("spawn", L(Map(("prefab", "Chest"), ("data", "rich")), "Boar, 0,0,0, rich"))),
        ];

        var data = ExpandWorldMod.CustomDataOf([Teleporter(), chest, boss, unused], rules, Known);

        Assert.Equal(["dichchuyen", "rich", "ultra", "unused"], data.Select(d => d.Name));
        Assert.Equal(["blackmarble_1x1"], data[0].Prefabs);
        Assert.Equal(["Boar", "Chest"], data[1].Prefabs);
        Assert.Equal(["Bonemass"], data[2].Prefabs);
        Assert.Empty(data[3].Prefabs);
    }

    [Fact]
    public void ALaterEntryOfANameWins()
    {
        var data = Assert.Single(ExpandWorldMod.CustomDataOf([E(("name", "a"), ("ints", L("x, 1"))), E(("name", "a"), ("ints", L("x, 2"))), E(("ints", L("x, 3")))], [], Known));
        var obj = new Recorder();

        CustomObjectTool.Apply(data, obj);

        Assert.Equal(2, obj.Set["x"]);
    }
}
