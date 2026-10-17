using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// Objects: removing, moving and copying them as undo steps, and taking them away or back in the scene.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Removes things from the world (on the next save) as one undo step; those already gone are skipped. A
    /// linked group's root takes its parts with it (ObjectLinks).
    /// </summary>
    /// <param name="indices">Indices into the scene's things.</param>
    public void Delete(IReadOnlyCollection<int> indices)
    {
        int[] live = [.. WithLinked(indices, anyPart: false).Indices.Where(i => !Scene.Things[i].Gone).Distinct()];
        if (live.Length == 0)
        {
            return;
        }
        SetGone(live.Select(i => (i, true)));
        AddChange(new Change(Lang.T("history.undo.deleted", live.Length), [], NoPoints, NoPoints, []) { Things = [.. live.Select(i => (i, false, true))] });
        Notify(live);
        Edited();
    }

    /// <summary>
    /// Moves (and turns) things as one undo step: each is replaced by a copy at its new place that keeps its own data
    /// (chest contents, builder...), like the web editor's moves. Rotations are Euler angles in degrees.
    /// </summary>
    /// <returns>The copies' indices.</returns>
    public List<int> Move(IReadOnlyList<(int Index, System.Numerics.Vector3 Position, System.Numerics.Vector3 Rotation)> moves, string? label = null) =>
        Transform([.. moves.Select(m => (m.Index, m.Position, m.Rotation, Scene.Things[m.Index].Scale))], label);

    /// <summary>
    /// Moves, turns and sizes things as one undo step, as Move does, each with its own scale (zero: the prefab's own).
    /// A part of a linked group never moves alone: its whole group moves (see WholeGroupMoves).
    /// </summary>
    /// <returns>
    /// The copies' indices, in the order of the things not gone; then those of the groups' other parts moved with them.
    /// </returns>
    public List<int> Transform(IReadOnlyList<(int Index, System.Numerics.Vector3 Position, System.Numerics.Vector3 Rotation, float Scale)> moves, string? label = null)
    {
        var live = WholeGroupMoves([.. moves.Where(m => !Scene.Things[m.Index].Gone)]);
        var adds = live.Select(m => MovedCopy(m.Index, m.Position, m.Rotation, m.Scale)).ToList();
        return Commit(label ?? Lang.T("history.undo.moved", live.Count), null, [.. live.Select(m => m.Index)], adds);
    }

    /// <summary>
    /// Takes things away or brings them back, in the scene and in the edit store (an object added in this session goes
    /// to its trash, so it can come back).
    /// </summary>
    private void SetGone(IEnumerable<(int Index, bool Gone)> things)
    {
        var list = Scene.Things;
        lock (list)
        {
            foreach (var (i, gone) in things)
            {
                // Removed in the game since: neither undo nor redo touches it (it is not there to bring back
                // or to delete; a deletion of it would remove another object under its ZDOID).
                if (list[i].Id >= 0 && Scene.World.Vanished.Contains(list[i].Id))
                {
                    continue;
                }
                list[i] = list[i] with { Gone = gone };
                Edits.SetDeleted([list[i].Id], gone);
            }
        }
    }

    /// <summary>
    /// Takes the things away and adds the new objects of a step, in the scene, under its things' lock.
    /// </summary>
    /// <param name="remove">Things to take away (bad or already gone ones are skipped).</param>
    /// <param name="add">New objects with whether each is a piece.</param>
    /// <returns>The indices of the things added, the things' changes (index, gone before, gone after) and the objects
    /// as they were given their ids.</returns>
    private (List<int> Indices, List<(int, bool, bool)> Changes, List<NewObject> Added) ChangeThings(
        IReadOnlyCollection<int> remove, IReadOnlyList<(NewObject Object, bool Piece)> add)
    {
        var list = Scene.Things;
        var added = new List<NewObject>();
        var indices = new List<int>();
        var changes = new List<(int, bool, bool)>();
        lock (list)
        {
            // (Indices from elsewhere, a script's, are checked: a bad one changed the ground with no undo step.)
            foreach (int i in remove.Distinct())
            {
                if (i >= 0 && i < list.Count && !list[i].Gone)
                {
                    changes.Add((i, false, true));
                }
            }
            foreach (var (o, piece) in add)
            {
                var n = o with { Id = Scene.Owner?.NextId() ?? _nextId-- };
                added.Add(n);
                list.Add(new WorldScene.Thing(n.Id, n.Prefab, n.Position, n.Rotation, n.Scale, piece) { Tamed = WorldScene.TamedOf(Scene.World, n) });
                indices.Add(list.Count - 1);
                changes.Add((list.Count - 1, true, false));
            }
        }
        return (indices, changes, added);
    }
}
