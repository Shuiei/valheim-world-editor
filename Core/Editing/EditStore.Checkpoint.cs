
namespace TerrainEditor.Editing;

public sealed partial class EditStore
{
    /// <summary>The working zones and the object lists as they were at <see cref="Mark"/>, for <see cref="Restore"/>.</summary>
    /// <param name="Zones">Copies of the working zones.</param>
    /// <param name="Deleted">The saved objects left out.</param>
    /// <param name="Added">The objects added.</param>
    /// <param name="Trash">The objects added and deleted again.</param>
    public sealed record Checkpoint(Dictionary<(int, int), ZoneEdit> Zones, HashSet<int> Deleted, Dictionary<int, NewObject> Added, Dictionary<int, NewObject> Trash);

    /// <summary>
    /// The working zones and the object lists as they are now, to put back with <see cref="Restore"/> (zone resets,
    /// baselines and the game's ground are not in it).
    /// </summary>
    public Checkpoint Mark()
    {
        lock (_lock)
        {
            return new Checkpoint(_zones.ToDictionary(kv => kv.Key, kv => kv.Value.Clone()), [.. _deleted], new(_added), new(_addedTrash));
        }
    }

    /// <summary>Puts the working zones and the object lists back as they were at <see cref="Mark"/>.</summary>
    public void Restore(Checkpoint mark)
    {
        lock (_lock)
        {
            _zones.Clear();
            foreach (var (key, zone) in mark.Zones)
            {
                _zones[key] = zone.Clone();
            }
            _deleted.Clear();
            _deleted.UnionWith(mark.Deleted);
            _added.Clear();
            foreach (var (id, o) in mark.Added)
            {
                _added[id] = o;
            }
            _addedTrash.Clear();
            foreach (var (id, o) in mark.Trash)
            {
                _addedTrash[id] = o;
            }
            Version++;
            ObjectsVersion++;
        }
    }
}
