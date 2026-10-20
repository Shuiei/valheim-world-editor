using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Terrain data with the older 64 × 64 paint grid (Expand World Data writes one for a zone it paints first) is read
/// onto the 65 × 65 grid as the game reads it, and the zone is taken as any other.
/// </summary>
public class WorldSaveLegacyPaintTests
{
    /// <summary>A zone's terrain data, unchanged heights and a 64 × 64 paint grid with these points painted.</summary>
    private static byte[] Legacy(params (int Index, Vector4 Paint)[] painted)
    {
        using MemoryStream raw = new();
        using (BinaryWriter w = new(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(1);
            w.Write(1);
            w.Write(0f);
            w.Write(0f);
            w.Write(0f);
            w.Write(64f);
            w.Write(65 * 65);
            for (int i = 0; i < 65 * 65; i++)
            {
                w.Write(false);
            }
            w.Write(64 * 64);
            for (int i = 0; i < 64 * 64; i++)
            {
                var hit = painted.Where(p => p.Index == i).Select(p => (Vector4?)p.Paint).FirstOrDefault();
                w.Write(hit != null);
                if (hit is { } c)
                {
                    w.Write(c.X);
                    w.Write(c.Y);
                    w.Write(c.Z);
                    w.Write(c.W);
                }
            }
        }
        return SaveGzipHelpers.Gzip(raw.ToArray());
    }

    [Fact]
    public void AnOlderPaintGridIsReadAsTheGameReadsIt()
    {
        Vector4 dirt = new(1, 0, 0, 1), cultivated = new(0, 1, 0, 1);
        var zone = TerrainZone.Decode(Vector3.Zero, StableHash.Of("_TerrainCompiler"), Legacy((0, cultivated), (63 * 64 + 63, dirt)));
        Assert.Equal(65 * 65, zone.ModifiedPaint.Length);
        Assert.Equal(cultivated, zone.Paint[0]);
        // The last row and column repeat the grid's last: its corner is the old corner.
        Assert.Equal(dirt, zone.Paint[64 * 65 + 64]);
        Assert.Equal(dirt, zone.Paint[63 * 65 + 63]);
        Assert.True(zone.ModifiedPaint[64 * 65 + 63]);
        Assert.False(zone.ModifiedPaint[64]);
        // The store takes it as any zone.
        Assert.NotNull(EditStore.Of(zone));
    }
}
