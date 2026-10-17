
namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>Live: the objects by ZDOID and by zone (not the vanished ones), made at the first merge.</summary>
    private Dictionary<(long, uint), int>? _byLiveId;
    /// <summary>Live: object ids by zone; see <c>_byLiveId</c>.</summary>
    private Dictionary<(int, int), HashSet<int>>? _byZone;

    /// <summary>Builds the ZDOID and zone lookups once, from every object not vanished.</summary>
    private void Index()
    {
        if (_byLiveId != null)
        {
            return;
        }
        _byLiveId = [];
        _byZone = [];
        for (int id = 0; id < ObjectRefs.Count; id++)
        {
            if (!Vanished.Contains(id))
            {
                Track(id);
            }
        }
    }

    /// <summary>Adds an object to the ZDOID and zone lookups.</summary>
    private void Track(int id)
    {
        ObjectRef o = ObjectRefs[id];
        _byLiveId![o.LiveId] = id;
        (_byZone!.TryGetValue(o.Zone, out var set) ? set : _byZone[o.Zone] = []).Add(id);
    }

    /// <summary>Removes an object from the ZDOID and zone lookups.</summary>
    private void Untrack(int id)
    {
        ObjectRef o = ObjectRefs[id];
        _byLiveId!.Remove(o.LiveId);
        if (_byZone!.TryGetValue(o.Zone, out var set))
        {
            set.Remove(id);
        }
    }

    /// <summary>
    /// Live: the object is in the game under another ZDOID now (the editor made it again there: an applied delete
    /// undone); followed by that one from then on.
    /// </summary>
    public void SetLiveId(int id, (long User, uint Id) zdo)
    {
        lock (Sync)
        {
            if (_byLiveId != null && !Vanished.Contains(id))
            {
                Untrack(id);
                ObjectRefs[id].LiveId = zdo;
                Track(id);
            }
            else
            {
                ObjectRefs[id].LiveId = zdo;
            }
        }
    }
}
