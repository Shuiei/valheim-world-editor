using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// One undo step that changes the ground, the things and the zone resets together.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// One undo step that may change the ground, take things away, add new objects and mark zones for reset. Nothing
    /// goes on the history when it changes nothing.
    /// </summary>
    /// <param name="label">The history's name for the step.</param>
    /// <param name="ground">As EditGround's change; null leaves the ground alone.</param>
    /// <param name="remove">Things to take away, indices into the scene's things (bad or already gone ones are
    /// skipped).</param>
    /// <param name="add">New objects (their ids are given here; Piece: a player-built piece; read after the ground
    /// step, which may fill it).</param>
    /// <param name="resets">Zones marked for reset or unmarked, with their state before and after.</param>
    /// <param name="removeHidden">Hidden objects to take away, by object id (gone or unknown ones are skipped; read
    /// after the ground step).</param>
    /// <param name="addHidden">New hidden objects (their ids are given here; read after the ground step).</param>
    /// <returns>The indices of the things added.</returns>
    public List<int> Commit(string label, Func<Ground, (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect)>? ground,
        IReadOnlyCollection<int> remove, IReadOnlyList<(NewObject Object, bool Piece)> add, (ZoneReset Reset, bool Before, bool After)[]? resets = null,
        IReadOnlyCollection<int>? removeHidden = null, IReadOnlyList<NewObject>? addHidden = null)
    {
        resets ??= [];
        var (points, before, after, zones) = ChangeGround(ground);
        // After the ground step, which may fill the list (a paste's objects stand on the ground it leaves).
        add = Placed(remove, add);
        var (indices, changes, added) = ChangeThings(remove, add);
        var (hidden, newHidden) = ChangeHidden(removeHidden ?? [], addHidden ?? []);
        if (added.Count + newHidden.Count > 0)
        {
            Edits.AddObjects([.. added, .. newHidden]);
        }
        SetGone(changes.Where(c => c.Item3).Select(c => (c.Item1, true)));
        SetHiddenGone(hidden.Where(h => h.After).Select(h => (h.Id, true)));
        SetResets(resets, after: true);
        if (points.Length == 0 && changes.Count == 0 && hidden.Count == 0 && resets.Length == 0)
        {
            return indices;
        }
        AddChange(new Change(label, points, before, after, zones) { Things = [.. changes], Resets = resets, Hidden = [.. hidden] });
        if (changes.Count > 0)
        {
            Notify(changes.Select(c => c.Item1));
        }
        if (indices.Count > 0)
        {
            ThingsAdded?.Invoke(indices);
        }
        Edited();
        return indices;
    }
}
