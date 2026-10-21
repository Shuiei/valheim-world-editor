using System.Numerics;
using Xunit;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>
/// The mod's rock paint (RockLook, RockGrid.TopRockAt): the palette by biome, each palette's mask, where a vertex lies
/// on it (dug, painted, natural following the ground), the ground's value from its mask, the topmost rock over a point,
/// and the scene drawing a Deep North chunk with its strip.
/// </summary>
public class RockLookTests
{
    [Fact]
    public void ThePaletteFollowsTheBiomeAshlandsFirst()
    {
        var at = Vector3.Zero;
        Assert.Equal(RockLook.Palette.Default, RockLook.At(new FakeArea(0), at));
        Assert.Equal(RockLook.Palette.Mistlands, RockLook.At(new FakeArea(0) { BiomeValue = 512 }, at));
        Assert.Equal(RockLook.Palette.DeepNorth, RockLook.At(new FakeArea(0) { BiomeValue = 512, DeepNorth = true }, at));
        Assert.Equal(RockLook.Palette.Ashlands, RockLook.At(new FakeArea(0) { DeepNorth = true, Ashlands = true }, at));
    }

    [Fact]
    public void EachPaletteHasItsMask()
    {
        var plain = RockLook.Mask(RockLook.Palette.Default);
        Assert.Equal((2, 2), (plain.Width, plain.Height));
        Assert.Equal([0xFF000000u, 0xFF0000FFu, 0xFF00FF00u, 0xFFFF0000u], plain.Texels);
        // The strips: cultivated, natural, dirt, paved; the natural top row the ground's own.
        var snow = RockLook.Mask(RockLook.Palette.DeepNorth);
        Assert.Equal((4, 2), (snow.Width, snow.Height));
        Assert.Equal((0u, 0x0000FF00u), (snow.Texels[1], snow.Texels[5]));
        Assert.Equal(0xFF000000u, RockLook.Mask(RockLook.Palette.Mistlands).Texels[5]);
        Assert.Equal((0x000000FFu, 0xFF0000FFu), (RockLook.Mask(RockLook.Palette.Ashlands).Texels[2], RockLook.Mask(RockLook.Palette.Ashlands).Texels[6]));
    }

    [Theory]
    [InlineData(RockLook.Palette.Default, RockPaint.None, false, 0f, 0.25f, 0.25f)]
    [InlineData(RockLook.Palette.Default, RockPaint.None, true, 0f, 0.75f, 0.25f)]
    [InlineData(RockLook.Palette.Default, RockPaint.Paved, false, 0f, 0.75f, 0.75f)]
    [InlineData(RockLook.Palette.DeepNorth, RockPaint.None, false, 1f, 0.375f, 0.75f)]
    [InlineData(RockLook.Palette.DeepNorth, RockPaint.None, true, 1f, 0.625f, 0.25f)]
    [InlineData(RockLook.Palette.Mistlands, RockPaint.Cultivated, false, 1f, 0.125f, 0.25f)]
    [InlineData(RockLook.Palette.Mistlands, RockPaint.Natural, false, 1f, 0.375f, 0.25f)]
    [InlineData(RockLook.Palette.Ashlands, RockPaint.Dirt, false, 0.5f, 0.625f, 0.5f)]
    public void AVertexLiesWhereItsPaintSays(RockLook.Palette palette, byte paint, bool dug, float ground, float u, float v) =>
        Assert.Equal(new Vector2(u, v), RockLook.Uv(palette, paint, dug, ground));

    [Fact]
    public void TheGroundsValueIsSnowInTheDeepNorthElseItsAlpha()
    {
        Assert.Equal(1f, RockLook.GroundValue(RockLook.Palette.DeepNorth, 0x0000FF00));
        Assert.Equal(0f, RockLook.GroundValue(RockLook.Palette.Mistlands, 0x0000FF00));
        Assert.Equal(1f, RockLook.GroundValue(RockLook.Palette.Ashlands, 0xFF000000));
    }

    [Fact]
    public void TheTopmostRockIsTheGroundOrWhatWasBuiltOverIt()
    {
        var grid = new RockGrid((_, _) => 10f);
        grid.EnsureColumn(0, 0);
        Assert.Equal(10f, grid.TopRockAt(8, 8), 3);
        // A lump of rock added 6 m up: the topmost rock is its top now.
        grid.Apply(new DigOp(DigKind.Fill, new Vector3(4, 16, 4), 1));
        Assert.InRange(grid.TopRockAt(8, 8), 16.5f, 17.01f);
    }

    [Fact]
    public void ADeepNorthChunkIsDrawnWithItsStripFollowingTheSnow()
    {
        var record = FakeObject.Record(new ColumnRecord(ColumnRecord.Plain, null, 0, [new DigOp(DigKind.Sphere, new Vector3(8, 40, 8), 2f)]), 8, 8);
        var layer = new TunnelScene(new FakeHost(), _ => { }).Build(new FakeArea(40, record) { DeepNorth = true, Mask = 0x0000FF00 })!;
        Assert.All(layer.Meshes, m => Assert.Same(RockLook.Mask(RockLook.Palette.DeepNorth), m.Mask));
        var uvs = layer.Meshes.SelectMany(m => m.MaskUvs!).ToList();
        // The pit's dug rock as dirt; the untouched ground around it, natural, on the snow row.
        Assert.Contains(new Vector2(0.625f, 0.25f), uvs);
        Assert.Contains(new Vector2(0.375f, 0.75f), uvs);
    }
}
