using TerrainEditor.Save;
using TerrainEditor.Terrain;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The terrain's zone caches: <see cref="TerrainService.Warm"/> makes a zone's ground and paint mask in one generator
/// pass, the same as <see cref="TerrainService.BaseZone"/> and <see cref="TerrainService.BaseMask"/> make them apart.
/// </summary>
[Collection("World files")]
public class TerrainServiceTests
{
    [Fact]
    public void WarmMakesTheGroundAndTheMaskInOnePass()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var warmed = new TerrainService(world, null);
        warmed.Warm(3, -2);
        Assert.Equal(1, warmed.ZoneBuilds);
        float[] heights = warmed.BaseZone(3, -2), mask = warmed.BaseMask(3, -2);
        Assert.Equal(1, warmed.ZoneBuilds);
        // Warm again: both kept.
        warmed.Warm(3, -2);
        Assert.Equal(1, warmed.ZoneBuilds);
        var apart = new TerrainService(world, null);
        Assert.Equal(apart.BaseZone(3, -2), heights);
        Assert.Equal(apart.BaseMask(3, -2), mask);
        Assert.Equal(2, apart.ZoneBuilds);
    }

    [Fact]
    public void WarmMakesOnlyWhatIsMissing()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var t = new TerrainService(world, null);
        float[] mask = t.BaseMask(0, 0);
        t.Warm(0, 0);
        Assert.Equal(2, t.ZoneBuilds);
        Assert.Same(mask, t.BaseMask(0, 0));
        float[] heights = t.BaseZone(1, 0);
        t.Warm(1, 0);
        Assert.Equal(4, t.ZoneBuilds);
        Assert.Same(heights, t.BaseZone(1, 0));
        Assert.Equal(4, t.ZoneBuilds);
    }

    [Fact]
    public void WarmGivesThePlayersModifiersToTheGroundAndNotToTheMask()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var plain = new TerrainService(world, null);
        const int Centre = 32 * 65 + 32;
        float generated = plain.BaseZone(3, -2)[Centre];
        // The hoe's raise at the zone's centre, a metre above the ground: it levels half a metre above that.
        var mods = new TerrainModifiers([new PlacedObject(StableHash.Of("raise"), 0, new System.Numerics.Vector3(3 * 64, generated + 1, -2 * 64), System.Numerics.Vector3.Zero, 0)]);
        var warmed = new TerrainService(world, mods);
        warmed.Warm(3, -2);
        float[] heights = warmed.BaseZone(3, -2);
        Assert.True(heights[Centre] > generated + 1);
        Assert.Equal(new TerrainService(world, mods).BaseZone(3, -2), heights);
        Assert.Equal(plain.BaseMask(3, -2), warmed.BaseMask(3, -2));
        Assert.Equal(1, warmed.ZoneBuilds);
    }
}
