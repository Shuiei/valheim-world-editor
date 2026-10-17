using System.Numerics;
using System.Reflection;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Regrow over many zones uses the world generator (static state): one world at a time.
/// </summary>
[Collection("World files")]
public class RegrowTests
{
    private static readonly MethodInfo LookRotation = typeof(Regrow).GetMethod("LookRotation", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Theory]
    [InlineData(1f, 0f, 0f, 0f, 1f, 0f)]
    [InlineData(0f, 0f, -1f, 0f, 1f, 0f)]
    [InlineData(0f, 0f, -1f, 0f, -1f, 0f)]
    [InlineData(0f, 0f, 1f, 0f, -1f, 0f)]
    [InlineData(0.3f, 0.2f, 0.9f, 0.1f, 1f, 0.2f)]
    public void LookRotationTurnsForwardOntoTheGivenDirection(float fx, float fy, float fz, float ux, float uy, float uz)
    {
        var forward = Vector3.Normalize(new Vector3(fx, fy, fz));
        var up = new Vector3(ux, uy, uz);
        var q = (Quaternion)LookRotation.Invoke(null, [forward, up])!;
        Assert.Equal(1f, q.Length(), 3);
        var f = Vector3.Transform(Vector3.UnitZ, q);
        Assert.True(Vector3.Distance(f, forward) < 1e-3f, $"{f} for {forward}");
        // Up stays on the side of the given up (made square to forward).
        Assert.True(Vector3.Dot(Vector3.Transform(Vector3.UnitY, q), up) > 0, $"{q}");
    }

    [Fact]
    public void RegrowAcrossEveryBiomeIsRepeatableAndOnTheGround()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var terrain = new ValheimGen.TerrainService(world, null);
        var edits = new EditStore(world);
        Assert.True(Regrow.RuleCount > 100);
        // A few zones of each biome on a coarse grid over the map, and some on a biome's edge.
        var picks = new List<(int X, int Z)>();
        var perBiome = new Dictionary<ValheimGen.Heightmap.Biome, int>();
        int edges = 0;
        for (int zz = -150; zz <= 150 && picks.Count < 40; zz += 7)
        {
            for (int zx = -150; zx <= 150 && picks.Count < 40; zx += 7)
            {
                if (zx * zx + zz * zz > 155 * 155)
                {
                    continue;
                }
                int[] corners = terrain.CornerBiomes(zx, zz);
                bool edge = corners.Distinct().Count() > 1;
                var b = terrain.BiomeAt(zx * 64f, zz * 64f);
                if (edge && edges < 6)
                {
                    edges++;
                    picks.Add((zx, zz));
                }
                else if (!edge && perBiome.GetValueOrDefault(b) < 3)
                {
                    perBiome[b] = perBiome.GetValueOrDefault(b) + 1;
                    picks.Add((zx, zz));
                }
            }
        }
        Assert.True(perBiome.Count >= 6, string.Join(", ", perBiome.Keys));
        var state = ValheimGen.UnityEngine.Random.state;
        int total = 0;
        foreach (var (zx, zz) in picks)
        {
            var spots = Regrow.Zones(terrain, edits, world.Seed, zx, zz, zx, zz);
            // Unity's random state is put back for the next user.
            Assert.Equal(state, ValheimGen.UnityEngine.Random.state);
            Assert.Equal(spots, Regrow.Zones(terrain, edits, world.Seed, zx, zz, zx, zz));
            foreach (var s in spots)
            {
                Assert.InRange(s.X, zx * 64f - 32f - 20f, zx * 64f + 32f + 20f);
                Assert.InRange(s.Z, zz * 64f - 32f - 20f, zz * 64f + 32f + 20f);
                Assert.True(s.Scale > 0);
                Assert.All([s.Rx, s.Ry, s.Rz], a => Assert.True(float.IsFinite(a)));
            }
            total += spots.Count;
        }
        Assert.True(total > 100, $"{total} spots in {picks.Count} zones");
        // A different seed gives different nature.
        var (fx, fz) = picks.First(p => Regrow.Zones(terrain, edits, world.Seed, p.X, p.Z, p.X, p.Z).Count > 3);
        Assert.NotEqual(Regrow.Zones(terrain, edits, world.Seed, fx, fz, fx, fz), Regrow.Zones(terrain, edits, world.Seed + 1, fx, fz, fx, fz));
    }

    // Coasts: rocks that only grow in shallow water (when any do: the draws decide), groups spilling
    // over into another biome.
    [Fact]
    public void RegrowOnCoastsKeepsToShallowWaterAndItsBiome()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var terrain = new ValheimGen.TerrainService(world, null);
        var edits = new EditStore(world);
        var coasts = new List<(int X, int Z)>();
        for (int zz = -90; zz <= 90 && coasts.Count < 40; zz += 2)
        {
            for (int zx = -90; zx <= 90 && coasts.Count < 40; zx += 2)
            {
                int[] c = terrain.CornerBiomes(zx, zz);
                if (c.Contains((int)ValheimGen.Heightmap.Biome.Ocean) && c.Any(b => b != (int)ValheimGen.Heightmap.Biome.Ocean))
                {
                    coasts.Add((zx, zz));
                }
            }
        }
        Assert.True(coasts.Count >= 20, $"{coasts.Count} coast zones");
        var all = coasts.SelectMany(z => Regrow.Zones(terrain, edits, world.Seed, z.X, z.Z, z.X, z.Z)).ToList();
        Assert.NotEmpty(all);
        var rocks = all.Where(s => s.Name == "rock4_coast").ToList();
        // Coast rocks stand between 2 m and 0.5 m below the sea.
        Assert.All(rocks, r => Assert.InRange(r.Y - ValheimGen.TerrainService.WaterLevel, -2.01f, -0.49f));
    }

    [Fact]
    public void SeveralZonesAtOnceAreTheZonesOneByOne()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var terrain = new ValheimGen.TerrainService(world, null);
        var edits = new EditStore(world);
        var together = Regrow.Zones(terrain, edits, world.Seed, 0, 0, 1, 0);
        var apart = Regrow.Zones(terrain, edits, world.Seed, 0, 0, 0, 0).Concat(Regrow.Zones(terrain, edits, world.Seed, 1, 0, 1, 0)).ToList();
        // The same spots; heights may differ in the last float digit (the ground is read from another corner).
        Assert.Equal(apart.Count, together.Count);
        for (int i = 0; i < apart.Count; i++)
        {
            Assert.Equal(apart[i] with { Y = 0 }, together[i] with { Y = 0 });
            Assert.Equal(apart[i].Y, together[i].Y, 3);
        }
    }

    [Fact]
    public void EachZoneIsReadOnceForSeveralZones()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var terrain = new ValheimGen.TerrainService(world, null);
        var edits = new EditStore(world);
        // A 4 × 4 block inside the Ashlands, away from the world's edge: groups there are up to 50 m wide and reach
        // into the neighbours, which used to be read again for every zone.
        var (ax, az) = Enumerable.Range(-150, 301).SelectMany(z => Enumerable.Range(-150, 301).Select(x => (X: x, Z: z)))
            .First(p => (p.X + 2) * (p.X + 2) + (p.Z + 2) * (p.Z + 2) < 130 * 130 && Enumerable.Range(-1, 6).All(dz => Enumerable.Range(-1, 6)
                .All(dx => terrain.CornerBiomes(p.X + dx, p.Z + dz).All(b => b == (int)ValheimGen.Heightmap.Biome.AshLands))));
        int before = Regrow.ZoneReads;
        var spots = Regrow.Zones(terrain, edits, world.Seed, ax, az, ax + 3, az + 3);
        Assert.NotEmpty(spots);
        // At most the block and one ring around it, each once.
        Assert.InRange(Regrow.ZoneReads - before, 16, 36);
    }
}
