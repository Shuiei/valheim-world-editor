using System.Numerics;

namespace TerrainEditor.Save;

/// <summary>
/// Objects found again by what they are and where they stand, to the centimetre: a save keeps an object's place
/// exactly, while its id (its order in the save) changes at every save. Two objects of one kind at one place are found
/// one after the other, each once.
/// </summary>
public sealed class ObjectPlaces
{
    /// <summary>The first id noted at each kind and place (most places hold one object: no list for those).</summary>
    private readonly Dictionary<(int Prefab, long X, long Y, long Z), int> _first = [];

    /// <summary>The ids noted at a kind and place after its first, in the order they were noted.</summary>
    private readonly Dictionary<(int Prefab, long X, long Y, long Z), Queue<int>> _more = [];

    /// <summary>The key of a kind at a place: its prefab and its position in whole centimetres (two objects are found at one place when their keys are equal).</summary>
    /// <param name="prefab">Its prefab hash.</param>
    /// <param name="p">Where it stands, in metres.</param>
    public static (int Prefab, long X, long Y, long Z) Key(int prefab, Vector3 p) =>
        (prefab, (long)MathF.Round(p.X * 100), (long)MathF.Round(p.Y * 100), (long)MathF.Round(p.Z * 100));

    /// <summary>Notes an object, to be found by its kind and place (all are noted before any is taken).</summary>
    /// <param name="id">What it is found as.</param>
    /// <param name="prefab">Its prefab hash.</param>
    /// <param name="position">Where it stands, in metres.</param>
    public void Add(int id, int prefab, Vector3 position)
    {
        var key = Key(prefab, position);
        if (_first.TryAdd(key, id))
        {
            return;
        }
        if (!_more.TryGetValue(key, out var ids))
        {
            _more[key] = ids = new Queue<int>();
        }
        ids.Enqueue(id);
    }

    /// <summary>The next object noted of this kind at this place, taken (each is found once); null when none is left.</summary>
    /// <param name="prefab">Its prefab hash.</param>
    /// <param name="position">Where it stands, in metres.</param>
    public int? Take(int prefab, Vector3 position)
    {
        var key = Key(prefab, position);
        if (_first.Remove(key, out int id))
        {
            return id;
        }
        return _more.TryGetValue(key, out var ids) && ids.Count > 0 ? ids.Dequeue() : null;
    }

    /// <summary>Every object of a save, by its id there.</summary>
    /// <param name="world">The save.</param>
    public static ObjectPlaces Of(WorldSave world)
    {
        var places = new ObjectPlaces();
        for (int id = 0; id < world.ObjectRefs.Count; id++)
        {
            places.Add(id, world.ObjectRefs[id].Prefab, world.ObjectRefs[id].Position);
        }
        return places;
    }
}
