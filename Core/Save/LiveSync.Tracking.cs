
namespace TerrainEditor.Save;

public sealed partial class LiveSync
{
    /// <summary>Sets an id's object in the game, keeping <see cref="_ours"/> in step. Under the lock.</summary>
    private void SetLive(int id, LiveTarget target)
    {
        DropLive(id);
        _liveIds[id] = target;
        var zdo = (target.User, target.Id);
        _ours[zdo] = _ours.GetValueOrDefault(zdo) + 1;
    }

    /// <summary>Forgets an id's object in the game, keeping <see cref="_ours"/> in step. Under the lock.</summary>
    private void DropLive(int id)
    {
        if (!_liveIds.Remove(id, out LiveTarget target))
        {
            return;
        }
        var zdo = (target.User, target.Id);
        if (_ours[zdo] == 1)
        {
            _ours.Remove(zdo);
        }
        else
        {
            _ours[zdo]--;
        }
    }

    /// <summary>
    /// Snapshot objects (in <see cref="_destroyed"/>) whose game object now stands for one of the editor's new objects,
    /// moved there: snapshot id to new object id (its ZDO in <see cref="_liveIds"/>). Moved back, or on, from there.
    /// </summary>
    private readonly Dictionary<int, int> _movedTo = [];

    /// <summary>
    /// How many snapshot objects' bytes the move search read and parsed, over this sync's life (a test seam: only an
    /// object that may move is read).
    /// </summary>
    internal int SourceReads { get; private set; }

    /// <summary>Forgets everything applied, once the world was read again or its pending changes dropped.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _destroyed.Clear();
            _liveIds.Clear();
            _ours.Clear();
            _movedTo.Clear();
            _unanswered = null;
        }
    }
}
