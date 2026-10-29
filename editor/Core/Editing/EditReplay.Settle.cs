using System.Numerics;

namespace TerrainEditor.Editing;

public sealed partial class EditReplay
{
    /// <summary>
    /// An object the editor deleted, with what it put in its place: deleted (and replaced) when the game has it as the
    /// editor knew it; a clash when the game changed it, or removed it while the editor put another in its place.
    /// </summary>
    /// <param name="old">The object as the editor knew it (an id of the save read, or an added object's own id).</param>
    /// <param name="prefab">Its prefab hash.</param>
    /// <param name="position">Where it stood.</param>
    /// <param name="newer">The object in the game's save (null: not there).</param>
    /// <param name="same">Whether the game's object has the data the editor knew (null: cannot tell).</param>
    /// <param name="with">What the editor put in its place.</param>
    /// <param name="goneSaysNothing">
    /// Its absence from the game's save is no clash: the editor's own save removed it, or wrote it (the game may have
    /// saved from the save it had).
    /// </param>
    /// <param name="moving">Copies of it moved elsewhere: kept, they take the game's object along (unless they carry data of their own).</param>
    private void Settle(int old, int prefab, Vector3 position, int? newer, Func<int, bool?> same, List<NewObject> with, bool goneSaysNothing, List<NewObject>? moving = null)
    {
        List<NewObject> moved = moving ?? [];
        List<NewObject> mine = [.. with.Select(Remapped), .. moved.Select(m => newer is int g && m.Raw == null ? m with { SourceId = g, Fresh = false } : Remapped(m))];
        List<NewObject> original = [.. with, .. moved];
        if (newer is int y)
        {
            bool? unchanged = same(y);
            if (unchanged is true)
            {
                _deletes.Add(y);
                _adds.AddRange(mine);
            }
            else
            {
                Clashing(new Item(ReplayClash.Changed, old, y, mine, original), prefab, position, unchanged == null);
            }
        }
        else if (original.Count > 0 && !goneSaysNothing)
        {
            Clashing(new Item(ReplayClash.Removed, old, null, mine, original), prefab, position, false);
        }
        else
        {
            _adds.AddRange(mine);
        }
    }
}
