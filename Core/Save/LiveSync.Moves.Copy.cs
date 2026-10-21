using TerrainEditor.Editing;

namespace TerrainEditor.Save;

public sealed partial class LiveSync
{
    /// <summary>
    /// Moves a snapshot object to the first of its copies that is not in the game yet and differs only by place and
    /// turn, or, with <paramref name="canUpdate"/>, to the first of its prefab with its data; that copy is then used up.
    /// </summary>
    /// <param name="world">The world the ids are of.</param>
    /// <param name="copies">The editor's copies of the object, not yet in the game.</param>
    /// <param name="o">The snapshot object's reference.</param>
    /// <param name="original">The snapshot object's data.</param>
    /// <param name="at">The game object standing for the snapshot object.</param>
    /// <param name="ids">The snapshot object's id, and the id the object moves from.</param>
    /// <param name="canUpdate">Whether the plugin changes data in place.</param>
    /// <param name="moves">Where the moves go.</param>
    /// <param name="moveFor">Where each move's snapshot object, the id it moves from and the one it moves to go.</param>
    private void MoveToCopy(WorldSave world, List<NewObject> copies, ObjectRef o, ZdoData original, LiveTarget at, (int Source, int From) ids,
        bool canUpdate, List<LiveMove> moves, List<(int Source, int From, int To)> moveFor)
    {
        foreach (NewObject n in copies)
        {
            if (n.Prefab != o.Prefab || world.NewObjectBytes(n, world.SourceReader()) is not { } bytes)
            {
                continue;
            }
            // A game object that took other data before takes the copy's again, even when it is the object's own.
            bool data = !SameButPlace(original, ZdoData.Parse(bytes, o.File.WorldVersion)) || _dataChanged.Contains(ids.Source);
            if (data && !canUpdate)
            {
                continue;
            }
            moves.Add(new LiveMove(at, n.Position, n.Rotation, bytes, data));
            moveFor.Add((ids.Source, ids.From, n.Id));
            copies.Remove(n);
            break;
        }
    }
}
