using TerrainEditor.Save;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A new world's location proxies: the location they spawn and the zone's seed; with Expand World Data, a clone's proxy
/// spawns the game prefab it is made of and keeps its own name as its reference.
/// </summary>
public class WorldCreatorProxyTests
{
    /// <summary>The proxy's ints for a kind of this name made of this prefab.</summary>
    private static List<(int Key, int Value)> Ints(string prefab, string? basePrefab, bool data) =>
        PrivateMember.Call<List<(int Key, int Value)>>(typeof(WorldCreator), "ProxyInts", null, new SeedLocations.Rule { Prefab = prefab, BasePrefab = basePrefab }, 77, data);

    [Fact]
    public void AProxySpawnsItsLocationWithTheZonesSeed() =>
        Assert.Equal([(WorldSave.LocationKey, StableHash.Of("StartTemple")), (StableHash.Of("seed"), 77)], Ints("StartTemple", null, false));

    [Fact]
    public void WithTheModACloneSpawnsItsGamePrefabAndKeepsItsName() =>
        Assert.Equal(
            [(StableHash.Of("locationreference"), StableHash.Of("StartTemple:Copy")), (WorldSave.LocationKey, StableHash.Of("StartTemple")), (StableHash.Of("seed"), 77)],
            Ints("StartTemple:Copy", "StartTemple", true));

    [Fact]
    public void ABlueprintLocationsProxyIsTheModsOwn()
    {
        var rule = new SeedLocations.Rule { Prefab = "MyHut", Contents = new LocationContents { Blueprint = "MyHut", ProxyData = [new ContentField("ints", 5, "1")] } };
        var z = ZdoData.Parse(PrivateMember.Call<byte[]>(typeof(WorldCreator), "Proxy", null, rule, (ushort)0x0B00, new System.Numerics.Vector3(1, 2, 3), 90f, 77, true));
        // Unturned, with only its name as its reference: no location of the game, no seed, no data.
        Assert.Equal([(StableHash.Of("locationreference"), StableHash.Of("MyHut"))], z.IntList);
        Assert.Equal(System.Numerics.Vector3.Zero, z.Rotation);
    }
}
