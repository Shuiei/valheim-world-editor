using TerrainEditor.Editing;
using ValheimGen;
using Xunit;
using static WorldEditor.Tests.MapDataTestWorld;

namespace WorldEditor.Tests;

/// <summary>
/// The 1 m close-up of the world map (MapData): the base ground where nothing is edited, the saved and pending edits within the
/// game's 8 m limit with paint, and windows across zone borders read from each zone.
/// </summary>
[Collection("World files")]
public class MapDataCloseUpTests
{
    [Fact]
    public void TheCloseUpIsTheBaseGroundWhereNothingIsEdited()
    {
        using var w = new TempWorld();
        var (_, terrain, edits) = Open(w);
        var map = new MapData(terrain, edits);
        // A zone the test world never edited: zone 5, 5 (x and z from 288 to 352).
        var l = map.Detail(288, 288, 64, edits.Version);
        float[] b = terrain.BaseZone(5, 5);
        for (int row = 0; row < 64; row += 7)
        {
            for (int col = 0; col < 64; col += 7)
            {
                Assert.Equal((float)(Half)b[row * EditStore.Grid + col], H(l, col, row));
            }
        }
        // Nothing painted: the paint layer is empty.
        Assert.All(Enumerable.Range(0, 64 * 64), i => Assert.Equal(0, l.Paint[i * 4 + 3]));
        // Every texel has a biome colour and a mask, opaque.
        Assert.All(Enumerable.Range(0, 64 * 64), i => Assert.Equal(255, l.Biome[i * 4 + 3]));
    }

    [Fact]
    public void TheCloseUpShowsEditsWithinTheGamesLimitAndPaint()
    {
        using var w = new TempWorld();
        var (_, terrain, edits) = Open(w);
        var e = new ZoneEdit(5, 5);
        int a = 10 * EditStore.Grid + 10, c = 20 * EditStore.Grid + 30, d = 40 * EditStore.Grid + 40;
        e.Modified[a] = true;
        e.Level[a] = 3f;
        // Level and smoothing together beyond 8 m: the game stops at 8 m.
        e.Modified[c] = true;
        e.Level[c] = 7f;
        e.Smooth[c] = 3f;
        e.Modified[d] = true;
        e.Level[d] = -7.5f;
        e.Smooth[d] = -1f;
        e.PaintModified[a] = true;
        e.Paint[a * 4] = 1f;
        e.Paint[a * 4 + 3] = 0.5f;
        edits.Put(e);
        float[] b = terrain.BaseZone(5, 5);
        var l = new MapData(terrain, edits).Detail(288, 288, 64, edits.Version);
        Assert.Equal((float)(Half)(b[a] + 3f), H(l, 10, 10));
        Assert.Equal((float)(Half)(b[c] + 8f), H(l, 30, 20));
        Assert.Equal((float)(Half)(b[d] - 8f), H(l, 40, 40));
        int p = (10 * 64 + 10) * 4;
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, l.Paint[p..(p + 4)]);
        Assert.Equal(0, l.Paint[(11 * 64 + 10) * 4 + 3]);
    }

    [Fact]
    public void AWindowAcrossZoneBordersReadsEachZone()
    {
        using var w = new TempWorld();
        var (_, terrain, edits) = Open(w);
        // From x, z = 300 to 427: zones 5 and 6 on both axes (the border at 352).
        var l = new MapData(terrain, edits).Detail(300, 300, 128, 0);
        foreach (var (wx, wz) in new[] { (300, 300), (351, 351), (352, 352), (420, 310), (310, 420), (427, 427) })
        {
            int zx = (int)Math.Floor((wx + 32) / 64.0), zz = (int)Math.Floor((wz + 32) / 64.0);
            float[] b = terrain.BaseZone(zx, zz);
            int k = wz - (zz * 64 - 32), m = wx - (zx * 64 - 32);
            Assert.Equal((float)(Half)b[k * EditStore.Grid + m], H(l, wx - 300, wz - 300));
        }
        // Negative coordinates: zone -1 starts at -96.
        var neg = new MapData(terrain, edits).Detail(-96, -96, 64, 0);
        Assert.Equal((float)(Half)terrain.BaseZone(-1, -1)[0], H(neg, 0, 0));
    }
}
