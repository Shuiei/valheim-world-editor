using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// Objects the scene does not hold as things (mods' data holders, ModManifests.IsData), changed with the history: taken
/// away and brought back by object id, in the edit store only.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// One undo step that takes things and hidden objects away and adds new ones of both, as a mod's tool's edit does
    /// (IToolContext.Commit). Nothing goes on the history when it changes nothing.
    /// </summary>
    /// <param name="label">The history's name for the step.</param>
    /// <param name="remove">Things to take away, indices into the scene's things.</param>
    /// <param name="add">New objects shown as things (Piece: a player-built piece).</param>
    /// <param name="removeHidden">Hidden objects to take away, by object id (gone or unknown ones are skipped).</param>
    /// <param name="addHidden">New hidden objects (their ids are given here).</param>
    /// <returns>The indices of the things added.</returns>
    public List<int> CommitObjects(string label, IReadOnlyCollection<int> remove, IReadOnlyList<(NewObject Object, bool Piece)> add,
        IReadOnlyCollection<int> removeHidden, IReadOnlyList<NewObject> addHidden)
    {
        var (points, before, after, zones) = ChangeGround(null);
        var (indices, changes, added) = ChangeThings(remove, add);
        var hidden = new List<(int Id, bool Before, bool After)>();
        foreach (int id in removeHidden.Distinct().Where(id => !Edits.IsGone(id) && !Scene.World.Vanished.Contains(id)))
        {
            hidden.Add((id, false, true));
        }
        var newHidden = addHidden.Select(o => o with { Id = Scene.Owner?.NextId() ?? _nextId-- }).ToList();
        hidden.AddRange(newHidden.Select(o => (o.Id, true, false)));
        if (added.Count + newHidden.Count > 0)
        {
            Edits.AddObjects([.. added, .. newHidden]);
        }
        SetGone(changes.Where(c => c.Item3).Select(c => (c.Item1, true)));
        SetHiddenGone(hidden.Where(h => h.After).Select(h => (h.Id, true)));
        if (changes.Count == 0 && hidden.Count == 0)
        {
            return indices;
        }
        AddChange(new Change(label, points, before, after, zones) { Things = [.. changes], Hidden = [.. hidden] });
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

    /// <summary>
    /// Takes hidden objects away or brings them back, in the edit store (one added in this session goes to its trash);
    /// objects removed in the game since are left alone, as SetGone leaves things.
    /// </summary>
    internal void SetHiddenGone(IEnumerable<(int Id, bool Gone)> objects)
    {
        foreach (var (id, gone) in objects)
        {
            if (!(id >= 0 && Scene.World.Vanished.Contains(id)))
            {
                Edits.SetDeleted([id], gone);
            }
        }
    }
}
