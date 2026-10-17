using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A new world's zones: the zones around the middle generated with the game's vegetation, a bare zone with nothing on it, and the
/// locations and boss stones of the generated zones placed as the game places them.
/// </summary>
[Collection("World files")]
public sealed class WorldCreatorZonesTests : WorldCreatorTestBase
{
    [Fact]
    public void ABareZoneIsTheMostEvenDryMeadowsWithNothingOnItAndItsGroundUntouched()
    {
        // With the game's location rules, so the zones holding a location are known and kept off.
        Fixtures.UseLocations();
        var made = WorldCreator.Create(Folder("Bare"), "Bare", Seed, radius: 4, flat: true);
        var (fx, fz) = Assert.NotNull(made.Flat);
        var w = WorldSave.Load(Folder("Bare"));
        var terrain = new ValheimGen.TerrainService(w);
        // Inside the middle (not on its edge), all above the water, all Meadows.
        Assert.InRange(fx, -3, 3);
        Assert.InRange(fz, -3, 3);
        float[] h = terrain.BaseZone(fx, fz);
        Assert.True(h.Min() >= ValheimGen.TerrainService.WaterLevel + 1.5f, $"lowest {h.Min()}");
        Assert.Equal(h.Max() - h.Min(), made.FlatSpan, 3);
        Assert.Equal(ValheimGen.Heightmap.Biome.Meadows, terrain.BiomeAt(fx * 64, fz * 64));
        // The most even such zone holding no location.
        var taken = WorldCreator.Sites(terrain, w.Seed, 4).Select(s => s.Zone).ToHashSet();
        Assert.Equal((fx, fz, made.FlatSpan), WorldCreator.FlattestZone(terrain, 4, taken));
        // Nothing on it but the game's own zone control (the vegetation grew everywhere else), no location in it, and
        // its ground has no edit.
        Assert.DoesNotContain(w.Objects, o => (int)MathF.Floor((o.Position.X + 32) / 64) == fx && (int)MathF.Floor((o.Position.Z + 32) / 64) == fz && o.Prefab != StableHash.Of("_TerrainCompiler") && o.Prefab != StableHash.Of("_ZoneCtrl"));
        Assert.DoesNotContain(w.Locations, l => (int)MathF.Floor((l.Position.X + 32) / 64) == fx && (int)MathF.Floor((l.Position.Z + 32) / 64) == fz);
        Assert.True(w.ObjectCount > 100);
        Assert.All(w.TerrainZones, z => Assert.DoesNotContain(true, z.ModifiedHeight));
        // Without generated zones around it, no bare zone (and nothing written).
        Assert.Throws<ArgumentException>(() => WorldCreator.Create(Folder("TooSmall"), "Small", Seed, radius: 1, flat: true));
        Assert.False(Directory.Exists(Folder("TooSmall")) && Directory.EnumerateFileSystemEntries(Folder("TooSmall")).Any());
    }

    [Fact]
    public void TheMiddleIsGeneratedWithTheGamesVegetation()
    {
        var made = WorldCreator.Create(Folder("Grown"), "Grown", Seed, radius: 1);
        Assert.Equal(9, made.Zones);
        Assert.True(made.Objects > 50, $"{made.Objects} objects");
        var w = WorldSave.Load(Folder("Grown"));
        Assert.Equal(made.Objects, w.ObjectCount);
        Assert.Equal(made.Objects, w.Chunks.Sum(c => c.IndexCount));
        Assert.Equal(Enumerable.Range(-1, 3).SelectMany(z => Enumerable.Range(-1, 3).Select(x => ((short)x, (short)z))).Order(), w.Zones!.Generated.Order());
        // Base chunk files (size 0, version 1), never the portals' chunk.
        Assert.All(w.Chunks, c => Assert.Equal((0, 1u), (c.Size, c.Version)));
        Assert.DoesNotContain(w.Chunks, c => c.Chunk == ChunkMath.PortalChunk);
        // Known kinds only, in or just around the generated zones (groups reach 20 m past theirs).
        Assert.All(w.Objects, o => Assert.NotNull(TerrainEditor.Terrain.PrefabCatalog.Get(o.Prefab)));
        Assert.All(w.Objects, o => Assert.InRange(o.Position.X, -96 - 20, 96 + 20));
        Assert.All(w.Objects, o => Assert.InRange(o.Position.Z, -96 - 20, 96 + 20));
        // The same seed grows the same world.
        WorldCreator.Create(Folder("Again"), "Grown", Seed, radius: 1, uid: 1);
        foreach (var c in w.Chunks)
        {
            Assert.Equal(File.ReadAllBytes(Path.Combine(Folder("Grown"), c.FileName)), File.ReadAllBytes(Path.Combine(Folder("Again"), c.FileName)));
        }
    }

    [Fact]
    public void TheLocationsOfTheGeneratedZonesAreSpawnedLikeTheGames()
    {
        // The game's location rules, as the editor has them once Valheim is found (CI has no game). They are process-wide
        // and stay installed: putting the rules found before back (empty, when this class was made first) raced the
        // other classes that install them and test a layout of them.
        Fixtures.UseLocations();
        var made = WorldCreator.Create(Folder("Temple"), "Temple", Seed, radius: 1);
        var w = WorldSave.Load(Folder("Temple"));
        var terrain = new ValheimGen.TerrainService(w);
        // The start temple stands within a zone of the middle: its proxy says which location it is and where, its
        // boss stones are objects of their own around it, and the zone file lists it as placed, the rest of the
        // seed's locations left for the game to lay out.
        var start = Assert.Single(ValheimGen.SeedLocations.Place(terrain.Generator, ValheimGen.SeedLocations.Which.Start, cancel: TestContext.Current.CancellationToken));
        var (templeAt, templeHash) = Assert.Single(w.Locations, l => l.Location == StableHash.Of("StartTemple"));
        Assert.Equal(new System.Numerics.Vector3(start.X, start.Y, start.Z), templeAt);
        Assert.Contains(w.Zones!.Locations, l => l.Hash == templeHash && l.Position == templeAt && l.Placed);
        Assert.False(w.Zones.LocationsGenerated);
        Assert.True(made.Locations >= 1, $"{made.Locations} locations");
        var (_, _, stoneAt, _, _) = Assert.Single(w.Objects, o => o.Prefab == StableHash.Of("BossStone_Eikthyr"));
        Assert.InRange(System.Numerics.Vector3.Distance(stoneAt, templeAt), 1f, 30f);
        Assert.Equal(5, w.Objects.Count(o => TerrainEditor.Terrain.PrefabCatalog.Get(o.Prefab)?.Name.StartsWith("BossStone_", StringComparison.Ordinal) == true));
        // Nothing grows on the ground the temple clears: what stands within its radius is its own (and its proxy).
        var temple = LocationPrefabs.Get("StartTemple")!;
        Assert.InRange(w.Objects.Count(o => System.Numerics.Vector3.Distance(o.Position, templeAt) < 25f && o.Prefab != StableHash.Of("_ZoneCtrl")), 2, temple.Contents.Length + 1);
        // One zone control in each generated zone, at its middle, as the game makes (its creatures spawn from it).
        var ctrls = w.Objects.Where(o => o.Prefab == StableHash.Of("_ZoneCtrl")).Select(o => (o.Position.X, o.Position.Y, o.Position.Z)).Order().ToList();
        Assert.Equal(Enumerable.Range(-1, 3).SelectMany(z => Enumerable.Range(-1, 3).Select(x => (x * 64f, 0f, z * 64f))).Order().ToList(), ctrls);
        // Every site of a wider ring is in a generated zone, and has objects just when the editor can make its kind.
        foreach (var site in WorldCreator.Sites(terrain, w.Seed, 16))
        {
            Assert.InRange(site.Zone.X, -16, 16);
            Assert.InRange(site.Zone.Z, -16, 16);
            Assert.Equal(ChunkMath.SectorOf(site.Position.X, site.Position.Z), site.Zone);
            var prefab = LocationPrefabs.Get(site.Name);
            Assert.Equal(prefab is { Interior: false }, site.Objects != null);
            if (site.Objects is { } objects)
            {
                // Its proxy last, where it stands.
                Assert.Equal(site.Position, objects[^1].At);
                Assert.Equal(WorldSave.LocationProxyPrefab, BitConverter.ToInt32(objects[^1].Zdo, 14));
            }
        }
    }
}
