using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace WorldEditor.Tests;

/// <summary>
/// The terrain zone tests' helpers: an unedited terrain zone at a zone's position, and one that reads back exactly as an
/// edit of the zone was given.
/// </summary>
public static class TerrainZoneSupport
{
    /// <summary>
    /// A terrain zone with no edits, at zone (zx, zz).
    /// </summary>
    public static TerrainZone Flat(int zx, int zz) => new()
    {
        Center = new Vector3(zx * 64f, 0, zz * 64f),
        ModifiedHeight = new bool[EditStore.Cells],
        LevelDelta = new float[EditStore.Cells],
        SmoothDelta = new float[EditStore.Cells],
        ModifiedPaint = new bool[EditStore.Cells],
        Paint = new Vector4[EditStore.Cells],
    };

    /// <summary>
    /// A terrain zone holding exactly what the edit says, so a check of the edit against it passes.
    /// </summary>
    public static TerrainZone Matching(ZoneEdit e)
    {
        TerrainZone z = Flat(e.ZoneX, e.ZoneZ);
        for (int i = 0; i < EditStore.Cells; i++)
        {
            z.ModifiedHeight[i] = e.Modified[i];
            z.LevelDelta[i] = e.Level[i];
            z.SmoothDelta[i] = e.Smooth[i];
            z.ModifiedPaint[i] = e.PaintModified[i];
            z.Paint[i] = new Vector4(e.Paint[i * 4], e.Paint[i * 4 + 1], e.Paint[i * 4 + 2], e.Paint[i * 4 + 3]);
        }
        return z;
    }
}
