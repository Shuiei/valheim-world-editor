using TerrainEditor.Editing;

namespace TerrainEditor.Save;

public sealed partial class LiveSync
{
    /// <summary>
    /// Moves a snapshot object to the first of its copies that is not in the game yet and differs only by place and
    /// turn; that copy is then used up.
    /// </summary>
    /// <param name="world">The world the ids are of.</param>
    /// <param name="copies">The editor's copies of the object, not yet in the game.</param>
    /// <param name="o">The snapshot object's reference.</param>
    /// <param name="original">The snapshot object's data.</param>
    /// <param name="at">The game object standing for the snapshot object.</param>
    /// <param name="source">The snapshot object's id.</param>
    /// <param name="from">The id the object moves from.</param>
    /// <param name="moves">Where the moves go.</param>
    /// <param name="moveFor">Where each move's snapshot object, the id it moves from and the one it moves to go.</param>
    private static void MoveToCopy(WorldSave world, List<NewObject> copies, ObjectRef o, ZdoData original, LiveTarget at, int source, int from,
        List<LiveMove> moves, List<(int Source, int From, int To)> moveFor)
    {
        foreach (NewObject n in copies)
        {
            if (n.Prefab != o.Prefab || world.NewObjectBytes(n, world.SourceReader()) is not { } bytes)
            {
                continue;
            }
            var copy = ZdoData.Parse(bytes, o.File.WorldVersion);
            if (!SameButPlace(original, copy))
            {
                continue;
            }
            moves.Add(new LiveMove(at, n.Position, n.Rotation, bytes));
            moveFor.Add((source, from, n.Id));
            copies.Remove(n);
            break;
        }
    }
}
