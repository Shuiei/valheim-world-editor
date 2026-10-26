namespace TerrainEditor.Editing;

public sealed partial class EditStore
{

    /// <summary>How many zones have pending changes (ground, paint or lift).</summary>
    public int ChangedZoneCount
    {
        get
        {
            lock (_lock)
            {
                return _zones.Values.Count(e => e.Changed);
            }
        }
    }

    /// <summary>Zones that were just applied to the running game: no longer pending.</summary>
    public void MarkApplied(IEnumerable<(int X, int Z)> zones)
    {
        lock (_lock)
        {
            foreach (var key in zones)
            {
                if (_zones.TryGetValue(key, out ZoneEdit? e))
                {
                    e.Changed = false;
                    e.ExistsInWorld = true;
                    _baseline[key] = e.Clone();
                }
                _gameChanged.Remove(key);
            }
            Version++;
        }
    }

    /// <summary>
    /// Live: the zones as they were sent to the game (copies taken when sending). The game has those;
    /// a zone changed again while they were on the way stays changed, to be sent next.
    /// </summary>
    public void MarkApplied(IReadOnlyList<ZoneEdit> sent)
    {
        lock (_lock)
        {
            foreach (ZoneEdit s in sent)
            {
                var key = (s.ZoneX, s.ZoneZ);
                var applied = s.Clone();
                applied.Changed = false;
                applied.ExistsInWorld = true;
                _baseline[key] = applied;
                _gameChanged.Remove(key);
                if (_zones.TryGetValue(key, out ZoneEdit? e))
                {
                    e.ExistsInWorld = true;
                    e.Changed = !e.SameGround(s);
                }
            }
            Version++;
        }
    }

    /// <summary>Zones the editor has put back to their saved / applied state (discarding pending changes).</summary>
    public void MarkUnchanged(IEnumerable<(int X, int Z)> zones)
    {
        lock (_lock)
        {
            foreach (var key in zones)
            {
                if (_zones.TryGetValue(key, out ZoneEdit? e))
                {
                    e.Changed = false;
                }
            }
            Version++;
        }
    }

    /// <summary>
    /// Takes a zone's edit from the page (sanitised first, and kept, not copied); it is pending while it
    /// differs from the zone's baseline.
    /// </summary>
    /// <exception cref="ArgumentException">The zone's arrays are not 65 × 65.</exception>
    public void Put(ZoneEdit incoming)
    {
        incoming.Sanitize();
        lock (_lock)
        {
            bool exists = _zones.TryGetValue((incoming.ZoneX, incoming.ZoneZ), out ZoneEdit? old);
            incoming.ExistsInWorld = exists && old!.ExistsInWorld;
            // No limit ground still the one the world holds as ground discs is not a change either.
            incoming.Changed = !AsPlannedLocked((incoming.ZoneX, incoming.ZoneZ), incoming)
                && (!_baseline.TryGetValue((incoming.ZoneX, incoming.ZoneZ), out ZoneEdit? baseline) ? !incoming.IsEmpty : !incoming.SameGround(baseline));
            _zones[(incoming.ZoneX, incoming.ZoneZ)] = incoming;
            Version++;
        }
    }
}
