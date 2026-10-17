namespace TerrainEditor.Save;

public static partial class WorldWriter
{
    /// <summary>Whether every terrain zone that was not edited or reset reads back exactly as it was.</summary>
    /// <returns>What is wrong, or null when the untouched zones are the same.</returns>
    private static string? CheckUntouchedZones(WorldSave before, ILookup<(int, int), TerrainZone> afterZones, HashSet<(int, int)> changed, HashSet<(int, int)> groundReset)
    {
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
        return null;
    }
}
