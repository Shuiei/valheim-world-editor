using Avalonia;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using ValheimGen;
using Xunit;
using static TerrainEditor.Desktop.Tests.SeedMapRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A seed's world from above (the New world page's map): the colours of its cells, a picture's pixels, and the view's
/// arithmetic (fit, look at, zoom around a point, kept in the world).
/// </summary>
public class SeedMapTests
{
    [AvaloniaFact]
    public void EachCellIsShadedByBiomeHeightAndDepth()
    {
        float water = WorldRules.Vanilla.WaterLevel;
        // Outside the world: the background.
        Assert.Equal(((byte)18, (byte)22, (byte)28), SeedMap.Shade(SeedPreview.Ocean, float.NegativeInfinity, water));
        // The sea: darker the deeper, down to 60 m.
        var shallow = SeedMap.Shade(SeedPreview.Ocean, water, water);
        var deep = SeedMap.Shade(SeedPreview.Ocean, water - 60, water);
        Assert.Equal(((byte)48, (byte)98, (byte)150), shallow);
        Assert.Equal(((byte)20, (byte)48, (byte)100), deep);
        Assert.Equal(deep, SeedMap.Shade(SeedPreview.Ocean, water - 500, water));
        // Land at the water's level: the game map's colour; higher, lighter (up to 120 m above).
        byte meadows = (byte)Array.IndexOf(SeedPreview.Biomes, Heightmap.Biome.Meadows);
        var (r, g, b) = MapData.PixelColor(Heightmap.Biome.Meadows);
        Assert.Equal((r, g, b), SeedMap.Shade(meadows, water, water));
        var (hr, hg, hb) = SeedMap.Shade(meadows, water + 60, water);
        Assert.True(hr > r && hg > g && hb > b);
        Assert.Equal(SeedMap.Shade(meadows, water + 120, water), SeedMap.Shade(meadows, water + 1000, water));
        // Water on land (a pool, a river): the biome under a blue tint.
        Assert.Equal(((byte)((r + 96) / 3), (byte)((g + 196) / 3), (byte)((b + 300) / 3)), SeedMap.Shade(meadows, water - 1, water));
        // The water line is the one given: with the water at 20 m, 25 m of Meadows is land.
        Assert.Equal((r, g, b), SeedMap.Shade(meadows, 20, 20));
        Assert.Equal(shallow, SeedMap.Shade(SeedPreview.Ocean, 20, 20));
    }

    [AvaloniaFact]
    public void APictureHasACellAPixel()
    {
        byte[] biome = new byte[6];
        float[] height = [.. Enumerable.Repeat(WorldRules.Vanilla.WaterLevel, 6)];
        using var bmp = SeedMap.Picture(biome, height, 3, 2);
        Assert.Equal(new PixelSize(3, 2), bmp.PixelSize);
    }

    [AvaloniaFact]
    public void TheWholeWorldFitsTheViewAndPointsGoBothWays()
    {
        using var open = Open(400, 300);
        var (_, map) = open;
        int changed = 0;
        map.ViewChanged += () => changed++;
        map.Show(Preview());
        Assert.True(changed > 0);
        // The world's diameter across the view's short side, its middle in the middle.
        Assert.Equal(2 * SeedPreview.Radius / 300, map.MetersPerPixel, 3);
        Assert.Equal((0f, 0f), (map.CenterX, map.CenterZ));
        Assert.Equal(new Point(200, 150), map.ScreenOf(0, 0));
        // North up: a point north of the middle is higher on the screen.
        Assert.True(map.ScreenOf(0, 1000).Y < 150);
        var (x, z) = map.WorldAt(map.ScreenOf(1234, -567));
        Assert.Equal(1234, x, 1);
        Assert.Equal(-567, z, 1);
    }

    [AvaloniaFact]
    public void LookingAtAPointIsHeldToTheWorldAndTheZoomsLimits()
    {
        using var open = Open(400, 300);
        var (_, map) = open;
        map.Show(Preview());
        map.LookAt(1000, -2000);
        Assert.Equal((1000f, -2000f, 4f), (map.CenterX, map.CenterZ, map.MetersPerPixel));
        // Not closer than half a metre a pixel, nor farther than the world a little smaller than the view.
        map.LookAt(0, 0, 0.01f);
        Assert.Equal(0.5f, map.MetersPerPixel);
        map.LookAt(0, 0, 1e6f);
        Assert.Equal(2.5f * SeedPreview.Radius / 300, map.MetersPerPixel, 3);
        // The middle stays in the world.
        map.LookAt(1e6f, -1e6f);
        Assert.Equal((SeedPreview.Radius, -SeedPreview.Radius), (map.CenterX, map.CenterZ));
    }

    [AvaloniaFact]
    public void ZoomingKeepsThePointUnderItWhereItWas()
    {
        using var open = Open(400, 300);
        var (_, map) = open;
        map.Show(Preview());
        map.LookAt(500, 500, 20);
        var at = new Point(100, 80);
        var (bx, bz) = map.WorldAt(at);
        map.Zoom(0.5, at);
        Assert.Equal(10, map.MetersPerPixel, 3);
        var (ax, az) = map.WorldAt(at);
        Assert.Equal(bx, ax, 1);
        Assert.Equal(bz, az, 1);
        // Around the middle when no point is given.
        var middle = (map.CenterX, map.CenterZ);
        map.Zoom(2);
        Assert.Equal(20, map.MetersPerPixel, 3);
        Assert.Equal(middle, (map.CenterX, map.CenterZ));
    }
}
