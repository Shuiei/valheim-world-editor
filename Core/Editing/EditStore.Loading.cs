using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public sealed partial class EditStore
{
    /// <summary>
    /// Start over from a (re)loaded world: after saving, or to discard unsaved changes. Drops every pending
    /// change, deletion, addition and reset, and the history.
    /// </summary>
    public void ResetFrom(WorldSave world)
    {
        lock (_lock)
        {
            History = null;
            HistoryGeneration++;
            _zones.Clear();
            _baseline.Clear();
            _deleted.Clear();
            _added.Clear();
            _addedTrash.Clear();
            _resets.Clear();
            _gameChanged.Clear();
            Load(world);
            Version++;
            ObjectsVersion++;
        }
    }

    /// <summary>
    /// Takes the world's terrain zones as the working copy and the baseline; damaged zones are skipped, and of two
    /// terrain objects in one zone only the first is taken.
    /// </summary>
    private void Load(WorldSave world)
    {
        foreach (TerrainZone z in world.TerrainZones)
        {
            // Two terrain objects in one zone: the first is shown (the writer saves the zone into both).
            if (z.ModifiedHeight.Length != Cells || z.ModifiedPaint.Length != Cells || _zones.ContainsKey((z.ZoneX, z.ZoneZ)))
            {
                continue;
            }
            ZoneEdit edit = FromTerrain(z);
            _zones[(z.ZoneX, z.ZoneZ)] = edit;
            _baseline[(z.ZoneX, z.ZoneZ)] = edit.Clone();
        }
    }

    /// <summary>
    /// A zone's terrain data as an edit (marked as in the world), as the store takes it; null for a damaged zone (not
    /// 65 × 65), which the store skips.
    /// </summary>
    public static ZoneEdit? Of(TerrainZone z) => z.ModifiedHeight.Length == Cells && z.ModifiedPaint.Length == Cells ? FromTerrain(z) : null;

    /// <summary>A zone's terrain data as an edit (marked as in the world).</summary>
    private static ZoneEdit FromTerrain(TerrainZone z)
    {
        ZoneEdit edit = new(z.ZoneX, z.ZoneZ) { ExistsInWorld = true };
        for (int i = 0; i < Cells; i++)
        {
            edit.Modified[i] = z.ModifiedHeight[i];
            edit.Level[i] = z.LevelDelta[i];
            edit.Smooth[i] = z.SmoothDelta[i];
            edit.PaintModified[i] = z.ModifiedPaint[i];
            edit.Paint[i * 4] = z.Paint[i].X;
            edit.Paint[i * 4 + 1] = z.Paint[i].Y;
            edit.Paint[i * 4 + 2] = z.Paint[i].Z;
            edit.Paint[i * 4 + 3] = z.Paint[i].W;
        }
        return edit;
    }
}
