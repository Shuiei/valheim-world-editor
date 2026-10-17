using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

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
        ZdoData z;
        var notes = new List<string>();
        try
        {
            z = ObjectData.Edited(_bytes, set, inv, notes);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            Log.CaughtBrief(ex, "applying the values typed in the Inspector");
            Message?.Invoke(Said.Of("inspect.msg.couldNotChange", ex.Message));
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
        byte[]? now;
        try
        {
            now = t.Gone ? null : ObjectData.Bytes(s.Scene.World, s.Edits, t.Id);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "reading an object's data again before changing it");
            Message?.Invoke(Said.Of("inspect.msg.cannotReadNow", ex.Message));
            return;
        }
        // Live: changed or removed in the game since it was opened (a player used the chest): the copy
        // would bring back its old data. Shown as it is now, to change again.
        if (t.Gone || !(now?.AsSpan().SequenceEqual(_bytes) ?? false))
        {
            // Gone offline: deleted, replaced or undone here; live, the game may have taken it too.
            var gone = s.IsLive ? Said.Of("inspect.msg.goneLive") : Said.Of("inspect.msg.gone");
            Message?.Invoke(t.Gone ? gone : Said.Of("inspect.msg.changedInGame"));
            if (t.Gone)
            {
                Close();
            }
            else
            {
                Open(index);
            }
            return;
        }
        var name = PrefabCatalog.DisplayName(z.Prefab) is string shown ? Said.Raw(shown) : Said.Of("inspect.object");
        var copies = s.Commit(Lang.T("inspect.undo.edited", name.Shown), null, [index], [(new NewObject(0, z.Prefab, z.Position, z.Rotation, 0, null, false, z.Serialize()), t.Piece)]);
        // The notes come from ObjectData in English, each its own sentence.
        Message?.Invoke(Said.Of("inspect.msg.changed", name) + Said.Join(" ", notes.Select(n => Said.Raw(n + "."))));
        Open(copies[0]);
        Replaced?.Invoke(copies[0]);
    }
}
