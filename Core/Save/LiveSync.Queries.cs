
namespace TerrainEditor.Save;

public sealed partial class LiveSync
{
    /// <summary>Whether the game has this new object already (it was sent and accepted).</summary>
    public bool IsLive(int id)
    {
        lock (_lock)
        {
            return _liveIds.ContainsKey(id);
        }
    }

    /// <summary>Whether the game no longer has this object because the editor removed it (applied).</summary>
    public bool IsDestroyed(int id)
    {
        lock (_lock)
        {
            return _destroyed.Contains(id);
        }
    }

    /// <summary>
    /// The game removed an object the editor made (a player picked or destroyed it): no longer the editor's to remove
    /// or to count.
    /// </summary>
    public void Forget(int id)
    {
        lock (_lock)
        {
            DropLive(id);
            ForgetMovesTo(id);
        }
    }

    /// <summary>Drops the moves made to an object: a copy the game removed is no longer one the editor moved.</summary>
    private void ForgetMovesTo(int id)
    {
        foreach (int source in _movedTo.Where(m => m.Value == id).Select(m => m.Key).ToList())
        {
            _movedTo.Remove(source);
            _dataChanged.Remove(source);
        }
    }
}
