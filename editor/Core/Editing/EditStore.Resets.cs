
namespace TerrainEditor.Editing;

public sealed partial class EditStore
{
    /// <summary>Zones to give back to the world generator.</summary>
    private readonly Dictionary<(int, int), ZoneReset> _resets = [];

    /// <summary>A copy of the objects added and not deleted again.</summary>
    public List<NewObject> Added
    {
        get
        {
            lock (_lock)
            {
                return [.. _added.Values];
            }
        }
    }

    /// <summary>How many objects are added (not counting those deleted again).</summary>
    public int AddedCount
    {
        get
        {
            lock (_lock)
            {
                return _added.Count;
            }
        }
    }
}
