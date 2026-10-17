using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Keeping the selection under a name.
public sealed partial class SelectPanel
{
    /// <summary>Keeps the selected objects under a name asked for (one of that name is replaced).</summary>
    internal async Task Keep()
    {
        string? world = WorldName();
        var items = SelectedItems(Things());
        if (world == null || items.Count == 0)
        {
            Message?.Invoke(Said.Of("select.msg.keepFirst"));
            return;
        }
        string? name = (await AskName(Lang.T("select.savedName", SavedSelections.For(world).Count + 1)))?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }
        if (!SavedSelections.Keep(world, name, items))
        {
            Message?.Invoke(Said.Of("select.msg.keepFailed"));
            return;
        }
        FillSaved();
        SavedBox.SelectedIndex = _saved.FindIndex(s => s.Name == name);
        Message?.Invoke(Said.Of("select.msg.kept", name, items.Count));
    }

    /// <summary>The selected objects that still exist, as saved selection items (none while the world has no things).</summary>
    internal List<SavedSelections.Item> SelectedItems(IReadOnlyList<WorldScene.Thing>? things) =>
        things == null ? [] : [.. SelectedIds().Where(i => i >= 0 && i < things.Count && !things[i].Gone).Select(i => new SavedSelections.Item(things[i].Prefab, things[i].Position.X, things[i].Position.Y, things[i].Position.Z))];
}
