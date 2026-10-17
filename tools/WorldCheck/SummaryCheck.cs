using System.Diagnostics;
using TerrainEditor.Save;

/// <summary>The summary check: the overview map of a world and its most edited zones.</summary>
internal static class SummaryCheck
{
    /// <summary>Renders the overview map and prints the most edited zones; returns the exit code.</summary>
    /// <param name="world">The world to summarise.</param>
    /// <param name="terrain">The world's terrain, which renders the overview map.</param>
    /// <param name="stopwatch">The timer that the render time is read from; it is restarted here.</param>
    internal static int Run(WorldSave world, ValheimGen.TerrainService terrain, Stopwatch stopwatch)
    {
        stopwatch.Restart();
        int overviewBytes = terrain.OverviewPng.Length;
        Console.WriteLine($"Overview map rendered: {overviewBytes:N0} bytes ({stopwatch.ElapsedMilliseconds} ms).");
        foreach (TerrainZone zone in world.TerrainZones.OrderByDescending(z => z.ModifiedHeight.Count(m => m)).Take(15))
        {
            var deltas = Enumerable.Range(0, zone.LevelDelta.Length).Where(i => zone.ModifiedHeight[i]).Select(i => zone.LevelDelta[i] + zone.SmoothDelta[i]).ToList();
            Console.WriteLine($"  zone ({zone.ZoneX},{zone.ZoneZ}) center ({zone.Center.X:0},{zone.Center.Z:0}): {deltas.Count} of {zone.ModifiedHeight.Length} heights edited" + (deltas.Count > 0 ? $" [{deltas.Min():0.00} .. {deltas.Max():0.00} m]" : "") + $", {zone.ModifiedPaint.Count(p => p)} of {zone.ModifiedPaint.Length} painted, grid {zone.HeightWidth}/{zone.PaintWidth}");
        }
        return 0;
    }
}
