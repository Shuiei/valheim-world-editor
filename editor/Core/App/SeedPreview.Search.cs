using ValheimGen;

namespace TerrainEditor.App;

public static partial class SeedPreview
{
    /// <summary>
    /// How many seeds a search looks at at once: all but one of the processors; two with a world generation mod that
    /// lays its worlds out whole first (Riverheim: a couple of hundred megabytes each, and it works on several threads
    /// itself).
    /// </summary>
    /// <param name="rules">What the worlds follow.</param>
    internal static int SeedsAtOnce(WorldRules? rules) => rules?.Generator?.Slow == true ? 2 : Math.Max(1, Environment.ProcessorCount - 1);

    /// <summary>
    /// Random seeds tried in parallel (count of them), the best kept (keep), scored by the wishes; progress
    /// is told after each seed (done, total). Previews of size cells (small: many seeds quickly). Cancelling
    /// returns what was found so far; the previews come without their generator and layout (kept light).
    /// </summary>
    /// <returns>The best previews, best first.</returns>
    public static List<(Preview Preview, float Score)> Search(Wishes wishes, int count, int keep, int size = 96, Action<int, int>? progress = null, WorldRules? rules = null, CancellationToken cancel = default)
    {
        var found = new List<(Preview, float)>();
        int done = 0;
        var options = new ParallelOptions { CancellationToken = cancel, MaxDegreeOfParallelism = SeedsAtOnce(rules) };
        try
        {
            Parallel.For(0, count, options, _ =>
            {
                var made = Make(RandomSeed(), size, landmarks: wishes.NeedsLandmarks, rules: rules, cancel: cancel);
                float score = Score(made.Stats, wishes);
                // Kept light: the generator and its layout (the biome map: tens of MB) are made again when
                // a result is looked at.
                var p = made with { Generator = null, Layout = null };
                lock (found)
                {
                    found.Add((p, score));
                    found.Sort((a, b) => b.Item2.CompareTo(a.Item2));
                    if (found.Count > keep)
                    {
                        found.RemoveAt(found.Count - 1);
                    }
                }
                progress?.Invoke(Interlocked.Increment(ref done), count);
            });
        }
        catch (OperationCanceledException)
        {
            // Stopped: what was found so far. Not an error: a plain line.
            Console.WriteLine($"Seed search: stopped after {Volatile.Read(ref done)} of {count} seeds.");
        }
        lock (found)
        {
            return [.. found];
        }
    }
}
