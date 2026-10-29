using TerrainEditor.Editing;

namespace TerrainEditor.Save;

public sealed partial class LiveSync
{
    /// <summary>The editor's new objects the game has, by id (ids &lt; 0).</summary>
    public List<(int Id, LiveTarget Target)> NewObjects()
    {
        lock (_lock)
        {
            return [.. _liveIds.Where(kv => kv.Key < 0).Select(kv => (kv.Key, kv.Value))];
        }
    }

    /// <summary>Whether this ZDOID is one the editor made (a new object, or a removed one brought back).</summary>
    public bool IsOurs((long User, uint Id) zdo)
    {
        lock (_lock)
        {
            return _ours.ContainsKey(zdo);
        }
    }

    /// <summary>
    /// How many object deletions and additions the next Apply would send: edits not in the game yet plus applied ones
    /// since undone (an applied placement undone is destroyed in the game, a deletion undone is made again there).
    /// </summary>
    public (int Deleted, int Added) Pending(EditStore edits)
    {
        lock (_lock)
        {
            return edits.WithObjects((deleted, added) =>
            {
                int d = deleted.Count(id => id >= 0 && !_destroyed.Contains(id)) + _liveIds.Keys.Count(id => id < 0 && !added.ContainsKey(id));
                int a = added.Keys.Count(id => !_liveIds.ContainsKey(id)) + _destroyed.Count(id => !deleted.Contains(id));
                return (d, a);
            });
        }
    }
}
