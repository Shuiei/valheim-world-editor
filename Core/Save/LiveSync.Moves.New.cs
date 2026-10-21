using TerrainEditor.Editing;

namespace TerrainEditor.Save;

public sealed partial class LiveSync
{
    /// <summary>
    /// For a plugin that changes data in place: each of the editor's new objects in the game that a copy of it replaced
    /// (a mod's tool's change of an object it added) takes that copy's place, turn and data under its own ZDOID, instead
    /// of being destroyed and the copy made.
    /// </summary>
    /// <param name="world">The world the ids are of.</param>
    /// <param name="added">The editor's new objects, by id.</param>
    /// <param name="moves">Where the moves go.</param>
    /// <param name="moveFor">Where each move's object, the id it moves from and the one it moves to go.</param>
    private void FindNewUpdates(WorldSave world, Dictionary<int, NewObject> added, List<LiveMove> moves, List<(int Source, int From, int To)> moveFor)
    {
        // Game objects already moved this call (a saved object's copy) are left to that move.
        HashSet<int> taken = [.. moveFor.Select(m => m.From), .. moveFor.Select(m => m.To)];
        foreach (NewObject n in added.Values.Where(n => n.SourceId is < 0 && !_liveIds.ContainsKey(n.Id) && !taken.Contains(n.Id)).ToList())
        {
            int from = n.SourceId!.Value;
            if (added.ContainsKey(from) || !_liveIds.TryGetValue(from, out LiveTarget at) || at.Prefab != n.Prefab || !taken.Add(from)
                || world.NewObjectBytes(n, world.SourceReader()) is not { } bytes)
            {
                continue;
            }
            moves.Add(new LiveMove(at, n.Position, n.Rotation, bytes, Data: true));
            moveFor.Add((from, from, n.Id));
            taken.Add(n.Id);
        }
    }
}
