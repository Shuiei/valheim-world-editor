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

    /// <summary>Its objects built with these contents at (100, 30, 200), unturned, from seed 7, as (prefab, place, bytes).</summary>
    private static List<(string Prefab, Vector3 At, byte[] Zdo)> Built(TempWorld w, LocationContents contents) =>
        [.. PrivateMember.Call<List<(Vector3 At, byte[] Zdo)>>(typeof(WorldCreator), "Built", null, Temple, contents, new Vector3(100, 30, 200), Quaternion.Identity, 7, new TerrainService(w.Load(), null))
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
}
