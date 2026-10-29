using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Regrow over several zones at once: the zones read one by one and each read once. Regrow uses the world generator
/// (static state): one world at a time.
/// </summary>
[Collection("World files")]
public class RegrowZonesTests
{
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
