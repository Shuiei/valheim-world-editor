using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>Deletes the selection (Del), as one undo step.</summary>
    public void Delete()
    {
        Commit();
        if (_view.Selected.Count == 0 || Session is not { } session)
        {
            Message?.Invoke(Said.Of("select.msg.deleteFirst"), true);
            return;
        }
        // A linked group's part never goes alone: the whole group does.
        var (sel, _, groups, noun) = session.WithLinked(_view.Selected);
        session.Delete(sel);
        _view.Select([]);
        Message?.Invoke(Said.Of("select.msg.deleted", sel.Count, WholeGroups(groups, noun, deleted: true)), false);
    }

    /// <summary>The indices of the objects drawn and pickable now (not gone, not hidden by View).</summary>
    private IEnumerable<int> Shown()
    {
        var s = Scene!;
        for (int i = 0; i < s.Things.Count; i++)
        {
            if (!s.Things[i].Gone && _view.IsPickable(i))
            {
                yield return i;
            }
        }
    }

    /// <summary>Selects every shown object of the selected objects' kinds (prefabs).</summary>
    public void SameKind()
    {
        var s = Scene;
        if (s == null)
        {
            return;
        }
        var kinds = _view.Selected.Select(i => s.Things[i].Prefab).ToHashSet();
        if (kinds.Count == 0)
        {
            Message?.Invoke(Said.Of("select.msg.sameFirst"), true);
            return;
        }
        var ids = Shown().Where(i => kinds.Contains(s.Things[i].Prefab)).ToList();
        _view.Select(ids);
        Message?.Invoke(Said.Of("select.msg.same", ids.Count), false);
    }

    /// <summary>Selects the shown objects that are not selected, and only those.</summary>
    public void Invert()
    {
        if (Scene == null)
        {
            return;
        }
        var sel = _view.Selected.ToHashSet();
        var ids = Shown().Where(i => !sel.Contains(i)).ToList();
        _view.Select(ids);
        Message?.Invoke(Said.Of("select.msg.inverted", ids.Count), false);
    }
}
