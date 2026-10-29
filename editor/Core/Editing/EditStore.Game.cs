namespace TerrainEditor.Editing;

public sealed partial class EditStore
{
    /// <summary>
    /// Live: zones whose ground the game changed (a player digging, a hoe) while the editor had changes of
    /// its own there, not applied yet, with the game's ground. Applying them would write over what was done
    /// in the game, so the editor asks first; once the editor's changes there are undone the game's ground
    /// comes in (TakeWaitingGround); applying clears it (or discarding).
    /// </summary>
    private readonly Dictionary<(int, int), ZoneEdit> _gameChanged = [];

    /// <summary>The zones whose game ground is waiting (see <c>_gameChanged</c>), as a copy.</summary>
    public IReadOnlyList<(int X, int Z)> GameChanged
    {
        get
        {
            lock (_lock)
            {
                return _gameChanged.Keys.ToList();
            }
        }
    }

    /// <summary>
    /// The zones the game changed under changes of the editor's not applied yet: what an apply would write
    /// over (asked first).
    /// </summary>
    public List<(int X, int Z)> PendingOverGame()
    {
        lock (_lock)
        {
            return [.. _gameChanged.Keys.Where(k => _zones.TryGetValue(k, out ZoneEdit? e) && e.Changed)];
        }
    }

    /// <summary>
    /// Makes the game's ground a zone's baseline and working copy (the working copy dropped when the game has no
    /// terrain data there).
    /// </summary>
    private void Take((int, int) key, ZoneEdit fresh, bool exists)
    {
        _baseline[key] = fresh.Clone();
        _gameChanged.Remove(key);
        // The game's ground in place of No limit ground over discs (players dug there): not that ground any more.
        _planned.Remove(key);
        if (exists)
        {
            _zones[key] = fresh;
        }
        else
        {
            _zones.Remove(key);
        }
    }

    /// <summary>
    /// Live: zones whose changes of the editor's were undone since the game changed their ground: they
    /// take the game's ground now. Returns them (their ground is to be shown again).
    /// </summary>
    public List<(int X, int Z)> TakeWaitingGround()
    {
        lock (_lock)
        {
            var ready = _gameChanged.Where(kv => !(_zones.TryGetValue(kv.Key, out ZoneEdit? e) && e.Changed)).ToList();
            foreach (var (key, fresh) in ready)
            {
                Take(key, fresh, fresh.ExistsInWorld);
            }
            if (ready.Count > 0)
            {
                Version++;
            }
            return [.. ready.Select(kv => kv.Key)];
        }
    }
}
