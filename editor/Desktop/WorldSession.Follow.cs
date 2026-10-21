using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public sealed partial class WorldSession
{
    /// <summary>
    /// The zones that changed since they were last merged, read from the game (null: none, or not live): whole where
    /// something that stays put was made, removed or changed; only the kinds that move or tick on their own
    /// (creatures, ships, items, fires...) where only those did, so a creature walking does not have its zone read
    /// whole at every look. The plugin's errors and an unreachable game are the caller's to show.
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
        var (changed, moving) = ChangedSince(now);
        if (changed.Count == 0 && moving.Count == 0)
        {
            return null;
        }
        var objects = changed.Count > 0 ? await live.ZoneObjects(changed) : [];
        if (moving.Count > 0)
        {
            objects.AddRange(await live.MovingObjects(moving));
        }
        return new Fetched(world, now, changed, objects, applies) { Moving = moving };
    }

    /// <summary>
    /// The zones whose digests changed since they were last merged: those to read whole (the first digest changed, or
    /// never merged), and those whose kinds that move on their own are read alone (only the second did).
    /// </summary>
    /// <param name="now">Each zone's digests as the game gives them now.</param>
    private (List<(int X, int Z)> Changed, List<(int X, int Z)> Moving) ChangedSince(Dictionary<(int X, int Z), LiveBridge.Digests> now)
    {
        List<(int X, int Z)> changed = [], moving = [];
        foreach (var (zone, digests) in now)
        {
            if (!_digests.TryGetValue(zone, out LiveBridge.Digests had) || had.Still != digests.Still)
            {
                changed.Add(zone);
            }
            else if (had.Moving != digests.Moving)
            {
                moving.Add(zone);
            }
        }
        return (changed, moving);
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
