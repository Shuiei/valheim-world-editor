using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimGen;
using Xunit;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

namespace WorldEditor.Tests;

/// <summary>
/// New world's build of a location whose contents Expand World Data's file changes: its own objects swapped, removed
/// and given data, the added objects, the offset and the scale, all drawn from the zone's seed as the mod draws them.
/// </summary>
[Collection("World files")]
public class WorldCreatorContentsTests
{
    /// <summary>The start temple's prefab of the test data.</summary>
    private static LocationPrefabs.Prefab Temple => LocationPrefabs.Get("StartTemple")!;

    /// <summary>
    /// Its objects built with these contents at (100, 30, 200), unturned, from seed 7, its pieces woken with this damage,
    /// as (prefab, place, bytes).
    /// </summary>
    private static List<(string Prefab, Vector3 At, byte[] Zdo)> Built(TempWorld w, LocationContents contents, WorldCreator.Damage damage = WorldCreator.Damage.None) =>
        [.. PrivateMember.Call<List<(Vector3 At, byte[] Zdo)>>(typeof(WorldCreator), "Built", null, Temple, contents, new WorldCreator.Placing(new Vector3(100, 30, 200), Quaternion.Identity, 7, damage), new TerrainService(w.Load(), null))
            .Select(o => (TerrainEditor.Terrain.PrefabCatalog.NameOf(BitConverter.ToInt32(o.Zdo, 14))!, o.At, o.Zdo))];

    [Fact]
    public void WithoutChangesTheLocationIsTheGames()
    {
        using var w = new TempWorld();
        // The objects of kinds the world can make, as the game rolls them.
        var own = LocationPrefabs.Made(Temple, new Vector3(100, 30, 200), Quaternion.Identity, 7).Where(m => TerrainEditor.Terrain.PrefabCatalog.Get(StableHash.Of(m.Prefab)) != null);
        Assert.Equal(own.Select(m => (m.Prefab, m.Position)), Built(w, LocationContents.None).Select(o => (o.Prefab, o.At)));
    }

    [Fact]
    public void ItsOwnObjectsAreSwappedRemovedRaisedAndGivenData()
    {
        using var w = new TempWorld();
        var own = Built(w, LocationContents.None);
        string first = own[0].Prefab;
        int mark = StableHash.Of("mark");
        var changed = Built(w, new LocationContents
        {
            Swaps = new Dictionary<string, IReadOnlyList<Weighted<string>>> { [first] = [new(1f, "Pickable_Stone")], [own[^1].Prefab] = [new(1f, "")] },
            Data = new Dictionary<string, IReadOnlyList<Weighted<IReadOnlyList<ContentField>?>>> { ["all"] = [new(1f, [new ContentField("ints", mark, "5")])] },
            Offset = 2f,
        });
        Assert.DoesNotContain(changed, o => o.Prefab == first || o.Prefab == own[^1].Prefab);
        Assert.Equal(own.Count(o => o.Prefab == first || o.Prefab == "Pickable_Stone"), changed.Count(o => o.Prefab == "Pickable_Stone"));
        Assert.All(changed, o => Assert.Contains(own, m => Vector3.Distance(m.At + new Vector3(0, 2, 0), o.At) < 1e-3f));
        Assert.All(changed, o => Assert.Contains((mark, 5), ZdoData.Parse(o.Zdo).IntList));
    }

    [Fact]
    public void AddedObjectsComeByChanceWithTheirScaleWhereTheirNetworkViewKeepsIt()
    {
        using var w = new TempWorld();
        int own = Built(w, LocationContents.None).Count;
        var added = Built(w, new LocationContents
        {
            Objects =
            [
                new ContentObject("Pickable_Stone", new Vector3(1, 0, 2), Vector3.Zero, Vector3.One, null, 1f, false),
                new ContentObject("Bush01", Vector3.Zero, Vector3.Zero, Vector3.One, null, 0f, false),
                new ContentObject("Bush01", new Vector3(-3, 0, 0), Vector3.Zero, new Vector3(1, 2, 1), null, 1f, false),
            ],
            ScaleMin = new Vector3(2),
            ScaleMax = new Vector3(3),
        }).Skip(own).ToList();
        // A chance of 0 never comes; a chance of 1 always does, where the file puts it.
        Assert.Equal([("Pickable_Stone", new Vector3(101, 30, 202)), ("Bush01", new Vector3(97, 30, 200))], added.Select(o => (o.Prefab, o.At)));
        // The bush keeps its scale (drawn between the file's two, times its own); the stone's network view does not.
        Assert.Empty(ZdoData.Parse(added[0].Zdo).Vec3List);
        var scale = Assert.Single(ZdoData.Parse(added[1].Zdo).Vec3List).Value;
        Assert.InRange(scale.X, 2f, 3f);
        Assert.Equal(scale.X * 2, scale.Y, 4);
    }

    [Fact]
    public void AChoiceIsDrawnByWeightAndAnEmptyOneRemoves()
    {
        using var w = new TempWorld();
        var own = Built(w, LocationContents.None);
        var drawn = Built(w, new LocationContents
        {
            Swaps = new Dictionary<string, IReadOnlyList<Weighted<string>>> { [own[0].Prefab] = [new(0.5f, "Pickable_Stone"), new(0.5f, "Bush01")], [own[1].Prefab] = [] },
        });
        // Where the first of its objects stood, one of the two choices; the second is gone.
        var at = own.Where(o => o.Prefab == own[0].Prefab).Select(o => o.At).ToHashSet();
        Assert.All(drawn.Where(o => at.Contains(o.At)), o => Assert.True(o.Prefab is "Pickable_Stone" or "Bush01", o.Prefab));
        Assert.Equal(at.Count, drawn.Count(o => at.Contains(o.At)));
        Assert.DoesNotContain(drawn, o => o.Prefab == own[1].Prefab);
        // The same draws every time.
        Assert.Equal(drawn.Select(o => o.Prefab), Built(w, new LocationContents { Swaps = new Dictionary<string, IReadOnlyList<Weighted<string>>> { [own[0].Prefab] = [new(0.5f, "Pickable_Stone"), new(0.5f, "Bush01")], [own[1].Prefab] = [] } }).Select(o => o.Prefab));
    }

    /// <summary>Two fences added to the start temple, the second with a health of 50 in its data.</summary>
    private static readonly LocationContents Fences = new()
    {
        Objects =
        [
            new ContentObject("wood_fence", new Vector3(1, 0, 2), Vector3.Zero, Vector3.One, null, 1f, false),
            new ContentObject("wood_fence", new Vector3(-3, 0, 0), Vector3.Zero, Vector3.One, [new ContentField("floats", StableHash.Of("health"), "50")], 1f, false),
        ],
    };

    /// <summary>The healths the two added fences were built with (null: none in their data).</summary>
    private static float?[] FenceHealths(TempWorld w, WorldCreator.Damage damage) =>
        [.. Built(w, Fences, damage).Where(o => o.Prefab == "wood_fence").Select(o => ZdoData.Parse(o.Zdo).FloatList.Where(v => v.Key == StableHash.Of("health")).Select(v => (float?)v.Value).FirstOrDefault())];

    [Fact]
    public void PiecesWakeAtARandomHealthWhenTheLocationDamagesThem()
    {
        using var w = new TempWorld();
        // No damage: the first at full health (none in its data), the second as its data says.
        Assert.Equal([null, 50f], FenceHealths(w, WorldCreator.Damage.None));
        // Damage: the first between 10% and 60% of its 400; the second keeps its data's.
        float?[] damaged = FenceHealths(w, WorldCreator.Damage.Random);
        Assert.InRange(damaged[0]!.Value, 40f, 240f);
        Assert.Equal(50f, damaged[1]);
        // Damage over data ("all"): both drawn, the same draws every time.
        float?[] all = FenceHealths(w, WorldCreator.Damage.RandomOverData);
        Assert.All(all, h => Assert.InRange(h!.Value, 40f, 240f));
        Assert.Equal(all, FenceHealths(w, WorldCreator.Damage.RandomOverData));
    }

    [Fact]
    public void APieceAtFullHealthInItsDataIsDamagedToo()
    {
        using var w = new TempWorld();
        var full = Fences with { Objects = [Fences.Objects[0] with { Data = [new ContentField("floats", StableHash.Of("health"), "400")] }] };
        var (_, _, zdo) = Assert.Single(Built(w, full, WorldCreator.Damage.Random), o => o.Prefab == "wood_fence");
        Assert.InRange(ZdoData.Parse(zdo).FloatList.Single(v => v.Key == StableHash.Of("health")).Value, 40f, 240f);
    }

    [Fact]
    public void DamageOverDataTakesTheHealthOfEveryObjectsData()
    {
        using var w = new TempWorld();
        var bush = new LocationContents { Objects = [new ContentObject("Bush01", new Vector3(2, 0, 2), Vector3.Zero, Vector3.One, [new ContentField("floats", StableHash.Of("health"), "50")], 1f, false)] };
        // A bush wears nothing, but "all" takes its data's health as it does a piece's.
        Assert.Contains(StableHash.Of("health"), ZdoData.Parse(Built(w, bush, WorldCreator.Damage.Random)[^1].Zdo).FloatList.Select(v => v.Key));
        Assert.DoesNotContain(StableHash.Of("health"), ZdoData.Parse(Built(w, bush, WorldCreator.Damage.RandomOverData)[^1].Zdo).FloatList.Select(v => v.Key));
    }

    [Theory]
    [InlineData(null, false, "None")]
    [InlineData(null, true, "Random")]
    [InlineData("true", false, "Random")]
    [InlineData("all", false, "RandomOverData")]
    [InlineData("false", true, "None")]
    public void TheDamageIsTheFilesElseThePrefabsOwn(string? file, bool prefab, string expected) =>
        Assert.Equal(Enum.Parse<WorldCreator.Damage>(expected), PrivateMember.Call<WorldCreator.Damage>(typeof(WorldCreator), "DamageOf", null, new SeedLocations.Rule { RandomDamage = file }, Temple with { RandomDamage = prefab }));

    [Fact]
    public void AGrownSpotKeepsItsData()
    {
        int mark = StableHash.Of("mark");
        var batch = new WorldCreator.ChunkBatch();
        PrivateMember.Call(typeof(WorldCreator), "PlaceSpots", null, new List<TerrainEditor.App.Regrow.Spot> { new("Bush01", 10, 30, 20, 0, 90, 0, 1f, null, [new ContentField("ints", mark, "4")]) }, batch);
        byte[] zdo = Assert.Single(Assert.Single(batch.Chunks).Objects);
        Assert.Contains((mark, 4), ZdoData.Parse(zdo).IntList);
    }
}
