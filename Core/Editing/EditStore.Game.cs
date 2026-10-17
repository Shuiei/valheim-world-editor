using TerrainEditor.Save;

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

    /// <summary>What became of a zone's ground sent by the game (see <see cref="TakeGameGround"/>).</summary>
    public enum GameGround
    {
        /// <summary>What the editor has as applied: its own apply coming back.</summary>
        Same,
        /// <summary>A zone without changes of the editor's took the game's ground.</summary>
        Taken,
        /// <summary>A zone with changes kept them; the game's ground waits (GameChanged).</summary>
        Kept,
    }

    /// <summary>
    /// Live: a zone's ground as the game has it now (null: no ground changes there any more). Same: what
    /// the editor has as applied (its own apply coming back); Taken: a zone without changes of the
    /// editor's takes it; Kept: one with changes keeps them, the game's ground waiting (GameChanged).
    /// </summary>
    public GameGround TakeGameGround(int zx, int zz, TerrainZone? z)
    {
        var key = (zx, zz);
        ZoneEdit fresh = z != null && z.ModifiedHeight.Length == Cells && z.ModifiedPaint.Length == Cells ? FromTerrain(z) : new ZoneEdit(zx, zz);
        lock (_lock)
        {
            bool same = _baseline.TryGetValue(key, out ZoneEdit? was) ? was.SameGround(fresh) : fresh.IsEmpty;
            if (same)
            {
                _gameChanged.Remove(key);
                return GameGround.Same;
            }
            Version++;
            if (_zones.TryGetValue(key, out ZoneEdit? e) && e.Changed)
            {
                _gameChanged[key] = fresh;
                return GameGround.Kept;
            }
            Take(key, fresh, z != null);
            return GameGround.Taken;
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
