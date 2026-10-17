using TerrainEditor.Save;
using TerrainEditor.Terrain;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The terrain's zone caches: <see cref="TerrainService.Warm"/> makes a zone's ground and paint mask in one generator
/// pass, the same as <see cref="TerrainService.BaseZone"/> and <see cref="TerrainService.BaseMask"/> make them apart; the
/// mask has the locations' cleared paint.
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
    public void WarmGivesThePlayersModifiersToTheGroundAndTheMaskAsReadingThemDoes()
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
        Assert.Equal(new TerrainService(world, mods).BaseMask(3, -2), warmed.BaseMask(3, -2));
        Assert.Equal(1, warmed.ZoneBuilds);
    }

    [Fact]
    public void TheMaskHasALocationsClearedPaintWithinItsRadiusOnly()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        // Eikthyr's altar (dirt over 4.14 m) at vertex 37, 34 of zone 3, -2.
        var altar = new PlacedObject(0, StableHash.Of("Eikthyrnir"), new System.Numerics.Vector3(3 * 64 + 5, 40, -2 * 64 + 2), System.Numerics.Vector3.Zero, 0);
        var mods = new TerrainModifiers([altar]);
        float[] bare = new TerrainService(world, null).BaseMask(3, -2);
        float[] painted = new TerrainService(world, mods).BaseMask(3, -2);
        for (int i = 0; i < 65; i++)
        {
            for (int j = 0; j < 65; j++)
            {
                int o = (i * 65 + j) * 4;
                float d = MathF.Sqrt((j - 37) * (j - 37) + (i - 34) * (i - 34));
                if (d > 4.14f)
                {
                    Assert.Equal(bare[o..(o + 4)], painted[o..(o + 4)]);
                }
                else
                {
                    Assert.True(painted[o] > bare[o] || bare[o] == 1f, $"vertex {j}, {i} has no dirt");
                    Assert.Equal(bare[o + 3], painted[o + 3]);
                }
            }
        }
        Assert.Equal(1f, painted[(34 * 65 + 37) * 4]);
        // Warm paints the same.
        var warmed = new TerrainService(world, mods);
        warmed.Warm(3, -2);
        Assert.Equal(painted, warmed.BaseMask(3, -2));
        Assert.Equal(1, warmed.ZoneBuilds);
    }
}
