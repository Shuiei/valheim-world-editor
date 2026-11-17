using System.Numerics;
using ValheimWorldEditor.Sdk;
using Xunit;
using static ExpandWorld.WorldEditor.Tests.ExpandWorldEntries;

namespace ExpandWorld.WorldEditor.Tests;

/// <summary>
/// Expand World Data's persistent events file read for the Persistent event tool (ExpandWorldMod.Events), and the
/// objects an event makes when it starts placed round its middle (PersistentEventTool.Scatter).
/// </summary>
public class PersistentEventToolTests
{
    /// <summary>An area of ground at one height, with a strip of sea (ground under the water) west of x = 0.</summary>
    private sealed class Shore(float size) : IAreaView
    {
        public float MinX => -size;
        public float MinZ => -size;
        public float MaxX => size;
        public float MaxZ => size;
        public IReadOnlyList<IObjectView> ObjectsOf(string prefab) => [];
        public float GroundHeight(float x, float z) => x < 0 ? 20f : 35f;
        public int Biome(float x, float z) => 1;
        public bool IsDeepNorth(float x, float z) => false;
        public bool IsAshlands(float x, float z) => false;
        public uint BiomeColor(int x, int z) => 0;
        public uint GroundMask(int x, int z) => 0;
    }

    private static Dictionary<string, object?> Map(params (string Key, object? Value)[] fields) => fields.ToDictionary(f => f.Key, f => f.Value);

    private static ExpandWorldMod.PersistentEvent Event(params ExpandWorldMod.EventObjects[] objects) =>
        new(0, "test", true, 100, 300, 0, 0, "", objects);

    private static ExpandWorldMod.EventObjects Group(string[] prefabs, int min, int max, ExpandWorldMod.EventWater water = ExpandWorldMod.EventWater.Anywhere, float spacing = 0, float reach = 40) =>
        new(prefabs, false, true, min, max, spacing, reach, 90, water, false);

    [Fact]
    public void TheFilesEventsAreReadInItsOrderWithTheModsDefaults()
    {
        List<string> notes = [];
        var events = ExpandWorldMod.Events(
        [
            E(("name", "jotun_invasion"), ("maxConcurrent", "3"), ("objects", new List<object?>
            {
                Map(("prefabs", new List<object?> { "FimbulLocation01" }), ("important", "true"), ("randomRotation", "false"), ("minCount", "1"), ("maxCount", "1"),
                    ("minDistanceFromOtherObjects", "0"), ("maxDistance", "10")),
                Map(("prefabs", new List<object?> { "IceShelf_01", "IceShelf_02" }), ("minCount", "250"), ("maxCount", "800"), ("maxDistance", "0"), ("water", "OnlySpawnInWater"), ("data", "ice")),
                "not a map",
            })),
            E(("name", "Off"), ("enabled", "false"), ("minRadius", "150"), ("maxRadius", "120"), ("minDuration", "3600"), ("maxDuration", "7200"), ("location", "Runestone_Meadows")),
            E(("minRadius", "wide")),
        ], notes);

        Assert.Equal([(0, "jotun_invasion"), (1, "Off"), (2, "event 2")], events.Select(e => (e.Index, e.Name)));
        var jotun = events[0];
        Assert.Equal((true, 100f, 300f, 0f, 0f, ""), (jotun.Enabled, jotun.MinRadius, jotun.MaxRadius, jotun.MinDuration, jotun.MaxDuration, jotun.Location));
        Assert.Equal(2, jotun.Objects.Count);
        var fimbul = jotun.Objects[0];
        Assert.Equal((true, false, 1, 1, 0f, 10f, 90f, ExpandWorldMod.EventWater.Anywhere, false),
            (fimbul.Important, fimbul.RandomRotation, fimbul.MinCount, fimbul.MaxCount, fimbul.Spacing, fimbul.MaxDistance, fimbul.MaxSlope, fimbul.Water, fimbul.HasData));
        var ice = jotun.Objects[1];
        Assert.Equal(["IceShelf_01", "IceShelf_02"], ice.Prefabs);
        Assert.Equal((10f, 0f, ExpandWorldMod.EventWater.WaterOnly, true), (ice.Spacing, ice.MaxDistance, ice.Water, ice.HasData));
        // A largest radius under the smallest is the smallest; a disabled event keeps its place in the file.
        Assert.Equal((false, 150f, 150f, 3600f, 7200f, "Runestone_Meadows"), (events[1].Enabled, events[1].MinRadius, events[1].MaxRadius, events[1].MinDuration, events[1].MaxDuration, events[1].Location));
        Assert.Contains(notes, n => n.Contains("minRadius", StringComparison.Ordinal));
    }

    [Fact]
    public void ObjectsStandWhereTheirGroupAllows()
    {
        var area = new Shore(200);
        var e = Event(
            Group(["Rock"], 20, 20, ExpandWorldMod.EventWater.LandOnly, reach: 30),
            Group(["Ice"], 20, 20, ExpandWorldMod.EventWater.WaterOnly, reach: 0));

        var placed = PersistentEventTool.Scatter(e, new Vector3(0, 35, 0), 60, area, _ => true, new Random(1));

        var rocks = placed.Objects.Where(o => o.Prefab == "Rock").ToList();
        var ice = placed.Objects.Where(o => o.Prefab == "Ice").ToList();
        Assert.NotEmpty(rocks);
        Assert.NotEmpty(ice);
        Assert.Equal(40, placed.Objects.Count + placed.NoRoom);
        // On land, on the ground, within the group's distance; in the water, at its surface, within the event's radius.
        Assert.All(rocks, o => Assert.True(o.Position.X >= 0 && o.Position.Y == 35f && new Vector2(o.Position.X, o.Position.Z).Length() <= 30.01f));
        Assert.All(ice, o => Assert.True(o.Position.X < 0 && o.Position.Y == PersistentEventTool.Water && new Vector2(o.Position.X, o.Position.Z).Length() <= 60.01f));
        Assert.False(placed.Outside);
    }

    [Fact]
    public void ObjectsKeepTheirSpacingAndTheirCount()
    {
        var e = Event(Group(["Rock"], 3, 6, spacing: 8));

        var placed = PersistentEventTool.Scatter(e, new Vector3(50, 35, 0), 100, new Shore(200), _ => true, new Random(7));

        Assert.InRange(placed.Objects.Count + placed.NoRoom, 3, 6);
        for (int a = 0; a < placed.Objects.Count; a++)
        {
            for (int b = a + 1; b < placed.Objects.Count; b++)
            {
                Assert.True(Vector3.Distance(placed.Objects[a].Position, placed.Objects[b].Position) >= 8f);
            }
        }
    }

    [Fact]
    public void WhatIsNoGameObjectAndWhatLiesOutsideTheAreaIsNotPlaced()
    {
        var e = Event(Group(["FimbulLocation01"], 1, 1), Group(["Rock", "MyBlueprint"], 30, 30, reach: 0));

        // An event far wider than the area open round its middle.
        var placed = PersistentEventTool.Scatter(e, new Vector3(0, 35, 0), 300, new Shore(20), n => n == "Rock", new Random(3));

        Assert.Equal(["FimbulLocation01", "MyBlueprint"], placed.Unknown);
        Assert.All(placed.Objects, o => Assert.True(o.Prefab == "Rock" && Math.Abs(o.Position.X) < 20 && Math.Abs(o.Position.Z) < 20));
        Assert.True(placed.Outside);
        Assert.True(placed.NoRoom > 0);
    }

    [Fact]
    public void ARingIsDrawnOnlyInsideTheArea()
    {
        var area = new Shore(50);

        Assert.Null(PersistentEventTool.Ring(area, new Vector3(0, 0, 0), 500, Vector4.One));
        var whole = PersistentEventTool.Ring(area, new Vector3(10, 0, 0), 20, Vector4.One)!;
        var part = PersistentEventTool.Ring(area, new Vector3(40, 0, 0), 20, Vector4.One)!;
        Assert.True(part.Triangles.Length < whole.Triangles.Length);
        Assert.All(whole.Vertices, v => Assert.True(v.Y is > 20f and < 36f));
    }
}
