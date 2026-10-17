using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public sealed partial class EditReplay
{
    /// <summary>
    /// The added objects with no source, waiting for a deleted or replaced object's place: each place takes at most one,
    /// the newest first, and what it takes is attached to that object so it is settled with it.
    /// </summary>
    private sealed class InPlaceAdds
    {
        private readonly Dictionary<(int, long, long, long), Queue<NewObject>> _byPlace = [];

        /// <summary>Starts with the pending added objects that have no source.</summary>
        /// <param name="pending">The added objects not settled as moves or in the game's save.</param>
        public InPlaceAdds(IEnumerable<NewObject> pending)
        {
            foreach (var n in pending.Where(n => n.SourceId == null).OrderByDescending(n => n.Id))
            {
                var key = ObjectPlaces.Key(n.Prefab, n.Position);
                if (!_byPlace.TryGetValue(key, out var q))
                {
                    _byPlace[key] = q = new Queue<NewObject>();
                }
                q.Enqueue(n);
            }
        }

        /// <summary>The added objects taken by a place: settled with the object that took them, not on their own.</summary>
        public HashSet<NewObject> Attached { get; } = [];

        /// <summary>Takes the object waiting in a place, if any: at most one.</summary>
        /// <param name="prefab">The prefab hash of the place's object.</param>
        /// <param name="position">Where it stood.</param>
        /// <returns>The object taken, or an empty list.</returns>
        public List<NewObject> Take(int prefab, Vector3 position)
        {
            List<NewObject> with = _byPlace.TryGetValue(ObjectPlaces.Key(prefab, position), out var q) && q.Count > 0 ? [q.Dequeue()] : [];
            Attached.UnionWith(with);
            return with;
        }
    }
}
