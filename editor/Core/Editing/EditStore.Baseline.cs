
namespace TerrainEditor.Editing;

public sealed partial class EditStore
{
    /// <summary>
    /// Each zone as saved / applied: a zone is pending only while it differs from this, so undoing back
    /// to it clears the change.
    /// </summary>
    private readonly Dictionary<(int, int), ZoneEdit> _baseline = [];

    /// <summary>A copy of a zone as last saved or applied (its baseline), or null when it had no terrain data then.</summary>
    public ZoneEdit? BaselineOf(int zx, int zz)
    {
        lock (_lock)
        {
            return _baseline.TryGetValue((zx, zz), out ZoneEdit? e) ? e.Clone() : null;
        }
    }

    /// <summary>A copy of a zone's working edit, or null when the zone has no terrain data.</summary>
    public ZoneEdit? Get(int zx, int zz)
    {
        lock (_lock)
        {
            return _zones.TryGetValue((zx, zz), out ZoneEdit? e) ? e.Clone() : null;
        }
    }

    /// <summary>Copies of every zone's working edit.</summary>
    public List<ZoneEdit> All()
    {
        lock (_lock)
        {
            return [.. _zones.Values.Select(e => e.Clone())];
        }
    }

    /// <summary>
    /// Copies of the zones' working edits that pass a test, tried on the edits themselves: only those are copied, each
    /// copy being a zone's whole ground (about 127 KB).
    /// </summary>
    /// <param name="keep">The test; it only reads the edit, under the store's lock.</param>
    public List<ZoneEdit> Copies(Func<ZoneEdit, bool> keep)
    {
        lock (_lock)
        {
            return [.. _zones.Values.Where(keep).Select(e => e.Clone())];
        }
    }

    /// <summary>
    /// Where the zones with height or paint changes, or changed since the last save or apply, are, and which of them
    /// changed: what the map outlines, read without copying the edits (it is drawn often).
    /// </summary>
    public List<(int ZoneX, int ZoneZ, bool Changed)> EditedZones()
    {
        lock (_lock)
        {
            return [.. _zones.Values.Where(e => e.Changed || e.HeightCount + e.PaintCount > 0).Select(e => (e.ZoneX, e.ZoneZ, e.Changed))];
        }
    }

    /// <summary>Objects (by WorldSave id) to leave out of the next save.</summary>
    private readonly HashSet<int> _deleted = [];

    /// <summary>A copy of the saved objects to leave out (by WorldSave id).</summary>
    public HashSet<int> Deleted
    {
        get
        {
            lock (_lock)
            {
                return [.. _deleted];
            }
        }
    }

    /// <summary>How many saved objects are to be left out.</summary>
    public int DeletedCount
    {
        get
        {
            lock (_lock)
            {
                return _deleted.Count;
            }
        }
    }

    /// <summary>New objects placed in the editor (planted, pasted, replacements), by the negative id the browser gave them.</summary>
    private readonly Dictionary<int, NewObject> _added = [];

    /// <summary>Added objects that were deleted again (kept so undo can bring them back).</summary>
    private readonly Dictionary<int, NewObject> _addedTrash = [];
}
