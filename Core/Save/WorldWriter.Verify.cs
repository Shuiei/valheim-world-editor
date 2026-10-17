using System.Numerics;
using TerrainEditor.Editing;

namespace TerrainEditor.Save;

public static partial class WorldWriter
{
    /// <summary>
    /// Reads the new save back and checks it against what was meant: the save format kept, reset zones no longer
    /// generated, the object count, the saved zones' ground, and every other zone's ground untouched.
    /// </summary>
    /// <returns>What is wrong, or null when the save is right.</returns>
    internal static string? Verify(string dir, WorldSave before, IReadOnlyList<ZoneEdit> saved, int created, int removed, HashSet<(int, int)> groundReset, IReadOnlyList<ZoneReset> resets) =>
        Check(WorldSave.Load(dir), before, saved, created, removed, groundReset, resets);

    /// <summary><see cref="Verify"/> on the new save already read back (<paramref name="after"/>).</summary>
    private static string? Check(WorldSave after, WorldSave before, IReadOnlyList<ZoneEdit> saved, int created, int removed, HashSet<(int, int)> groundReset, IReadOnlyList<ZoneReset> resets)
    {
        if (after.SaveVersion != before.SaveVersion)
        {
            return $"the index is in save format {after.SaveVersion}, not the world's {before.SaveVersion}";
        }
        foreach (ZoneReset r in resets)
        {
            if (after.Zones == null || after.Zones.Generated.Contains(((short)r.X, (short)r.Z)))
            {
                return $"zone {r.X}, {r.Z} is still marked as generated";
            }
        }
        if (after.ObjectCount != before.ObjectCount + created - removed)
        {
            return $"{after.ObjectCount} objects, expected {before.ObjectCount + created - removed}";
        }
        var afterZones = after.TerrainZones.ToLookup(t => (t.ZoneX, t.ZoneZ));
        foreach (ZoneEdit e in saved)
        {
            if (!afterZones.Contains((e.ZoneX, e.ZoneZ)))
            {
                return $"zone {e.ZoneX}, {e.ZoneZ} is missing";
            }
            foreach (TerrainZone z in afterZones[(e.ZoneX, e.ZoneZ)])
            {
                for (int i = 0; i < EditStore.Cells; i++)
                {
                    if (z.ModifiedHeight[i] != e.Modified[i] || (e.Modified[i] && (z.LevelDelta[i] != e.Level[i] || z.SmoothDelta[i] != e.Smooth[i])) || z.ModifiedPaint[i] != e.PaintModified[i])
                    {
                        return $"zone {e.ZoneX}, {e.ZoneZ} differs at point {i}";
                    }
                    if (e.PaintModified[i] && (z.Paint[i] != new Vector4(e.Paint[i * 4], e.Paint[i * 4 + 1], e.Paint[i * 4 + 2], e.Paint[i * 4 + 3])))
                    {
                        return $"zone {e.ZoneX}, {e.ZoneZ} paint differs at point {i}";
                    }
                }
            }
        }
        var changed = saved.Select(e => (e.ZoneX, e.ZoneZ)).ToHashSet();
        // Every other terrain zone must be untouched.
        // (Two terrain objects in one zone: compared in order, the writer keeps the objects' order.)
        foreach (var group in before.TerrainZones.Where(t => !changed.Contains((t.ZoneX, t.ZoneZ)) && !groundReset.Contains((t.ZoneX, t.ZoneZ))).GroupBy(t => (t.ZoneX, t.ZoneZ)))
        {
            var now = afterZones[group.Key].ToList();
            int n = 0;
            foreach (TerrainZone z in group)
            {
                TerrainZone? a = n < now.Count ? now[n] : null;
                n++;
                if (a == null || !a.LevelDelta.SequenceEqual(z.LevelDelta) || !a.SmoothDelta.SequenceEqual(z.SmoothDelta) || !a.ModifiedHeight.SequenceEqual(z.ModifiedHeight)
                    || !a.ModifiedPaint.SequenceEqual(z.ModifiedPaint) || !a.Paint.SequenceEqual(z.Paint))
                {
                    return $"unchanged zone {z.ZoneX}, {z.ZoneZ} was altered";
                }
            }
        }
        // A zone whose ground is reset may lose its terrain objects (one or two each), no other.
        int resetObjects = before.TerrainZones.Count(t => groundReset.Contains((t.ZoneX, t.ZoneZ)));
        return after.TerrainZones.Count >= before.TerrainZones.Count - resetObjects ? null : "terrain zones were lost";
    }
}
