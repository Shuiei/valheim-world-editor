using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>
    /// Writes the changes: the object is replaced by a copy with the new data, in one undo step, and the copy
    /// opened. Nothing is done when the object changed or went in the game since it was opened.
    /// </summary>
    internal async Task Apply()
    {
        if (_session() is not { } s || Index is not int index || _bytes == null)
        {
            return;
        }
        var (set, inv) = Changes();
        if (_standError != null)
        {
            Message?.Invoke(_standError);
            return;
        }
        var (w, h) = _grid;
        if (inv != null && w > 0 && inv.Any(i => i.X >= w || i.Y >= h)
            && !await Confirm(Lang.T("inspect.data.items"), Lang.T("inspect.ask.outside.text", w, h), Lang.T("inspect.ask.outside.yes")))
        {
            return;
        }
        if (BuildEdit(set, inv) is not { } edit)
        {
            return;
        }
        // Another area open since (its things are others): nothing to change here.
        if (index >= s.Scene.Things.Count)
        {
            Message?.Invoke(Said.Of("inspect.msg.notInArea"));
            Close();
            return;
        }
        var t = s.Scene.Things[index];
        if (!TryReadNow(s, t, out byte[]? now))
        {
            return;
        }
        // Live: changed or removed in the game since it was opened (a player used the chest): the copy
        // would bring back its old data. Shown as it is now, to change again.
        if (t.Gone || !SameBytes(now, _bytes))
        {
            ShowChangedSinceOpen(s, t, index);
            return;
        }
        CommitEdit(s, index, t, edit.Z, edit.Notes);
    }
}
