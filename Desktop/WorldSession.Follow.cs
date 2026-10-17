using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public sealed partial class WorldSession
{
    /// <summary>
    /// The zones that changed since they were last merged, read from the game (null: none, or not live). The
    /// plugin's errors and an unreachable game are the caller's to show.
    /// </summary>
    public async Task<Fetched?> FetchGame(IReadOnlyCollection<(int X, int Z)> zones)
    {
        if (Live is not { } live || zones.Count == 0)
        {
            return null;
        }
        var world = World;
        int applies = Volatile.Read(ref _applies);
        var now = await live.Watch(zones);
        var changed = now.Where(kv => !_digests.TryGetValue(kv.Key, out ulong d) || d != kv.Value).Select(kv => kv.Key).ToList();
        if (changed.Count == 0)
        {
            return null;
        }
        return new Fetched(world, now, changed, await live.ZoneObjects(changed), applies);
    }

    /// <summary>
    /// What was read, merged into the world (on the window's thread, at once: nothing else changes the
    /// world meanwhile). Null: not now (an apply is under way, or the world's lists are being read): merge
    /// it again later. Read for a world read again since, or before or during an Apply live: dropped (empty).
    /// A read older than an apply lacks what it sent (the new objects would count as removed by players);
    /// its zones' digests are not kept, so they are read again.
    /// </summary>
    public Followed? MergeGame(Fetched f)
    {
        if (f.World != World)
        {
            return new Followed(new WorldSave.Merged([], [], [], []), [], [], []);
        }
        // Held while merging: an apply (perhaps on another thread: Claude, a script) can neither begin nor end
        // between the check of the count and the merge.
        if (!_applying.Wait(0))
        {
            return null;
        }
        try
        {
            return f.Applies != Volatile.Read(ref _applies) ? new Followed(new WorldSave.Merged([], [], [], []), [], [], []) : Merge(f);
        }
        finally
        {
            _applying.Release();
        }
    }
}
