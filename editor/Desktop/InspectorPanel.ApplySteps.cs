using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>
    /// The object's data with the typed changes applied, and the notes ObjectData wrote about them; null when the
    /// change cannot be made (the message says why).
    /// </summary>
    /// <param name="set">The field changes typed.</param>
    /// <param name="inv">The contents as typed, or null when they are not changed.</param>
    /// <returns>The edited data and its notes, or null.</returns>
    private (ZdoData Z, List<string> Notes)? BuildEdit(List<FieldChange> set, List<ItemUpload>? inv)
    {
        var notes = new List<string>();
        try
        {
            return (ObjectData.Edited(_bytes!, set, inv, notes), notes);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            Log.CaughtBrief(ex, "applying the values typed in the Inspector");
            Message?.Invoke(Said.Of("inspect.msg.couldNotChange", ex.Message));
            return null;
        }
    }

    /// <summary>
    /// The object's data as it is in the area now, read again before a change is written.
    /// </summary>
    /// <param name="s">The edit session.</param>
    /// <param name="t">The object.</param>
    /// <param name="now">The data, or null when the object is gone.</param>
    /// <returns>False when the data could not be read (the message says why).</returns>
    private bool TryReadNow(EditSession s, WorldScene.Thing t, out byte[]? now)
    {
        try
        {
            now = t.Gone ? null : ObjectData.Bytes(s.Scene.World, s.Edits, t.Id);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "reading an object's data again before changing it");
            Message?.Invoke(Said.Of("inspect.msg.cannotReadNow", ex.Message));
            now = null;
            return false;
        }
    }

    /// <summary>
    /// Whether the object's data as it is now is the data the panel opened with.
    /// </summary>
    /// <param name="now">The data now, or null.</param>
    /// <param name="opened">The data the panel opened with.</param>
    /// <returns>True when both are the same bytes.</returns>
    internal static bool SameBytes(byte[]? now, byte[] opened) => now?.AsSpan().SequenceEqual(opened) ?? false;

    /// <summary>
    /// Tells the player that the object went or changed in the game since the panel opened: a gone object closes the
    /// panel, a changed one is opened again as it is now, to change again.
    /// </summary>
    /// <param name="s">The edit session.</param>
    /// <param name="t">The object.</param>
    /// <param name="index">Its place in the area's things.</param>
    private void ShowChangedSinceOpen(EditSession s, WorldScene.Thing t, int index)
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
    }

    /// <summary>
    /// Replaces the object by a copy with the edited data, in one undo step, then says so and opens the copy.
    /// </summary>
    /// <param name="s">The edit session.</param>
    /// <param name="index">The object's place in the area's things.</param>
    /// <param name="t">The object.</param>
    /// <param name="z">The edited data.</param>
    /// <param name="notes">What ObjectData wrote about the changes, in English.</param>
    private void CommitEdit(EditSession s, int index, WorldScene.Thing t, ZdoData z, List<string> notes)
    {
        var name = PrefabCatalog.DisplayName(z.Prefab) is string shown ? Said.Raw(shown) : Said.Of("inspect.object");
        var copies = s.Commit(Lang.T("inspect.undo.edited", name.Shown), null, [index], [(new NewObject(0, z.Prefab, z.Position, z.Rotation, 0, null, false, z.Serialize()), t.Piece)]);
        // The notes come from ObjectData in English, each its own sentence.
        Message?.Invoke(Said.Of("inspect.msg.changed", name) + Said.Join(" ", notes.Select(n => Said.Raw(n + "."))));
        Open(copies[0]);
        Replaced?.Invoke(copies[0]);
    }
}
