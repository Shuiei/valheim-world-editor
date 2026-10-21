using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The saved selections: selecting one, and forgetting one once confirmed.
public sealed partial class SelectPanel
{
    /// <summary>
    /// Selects the chosen saved selection's objects (with <c>add</c>, as well as what is selected), and says how many
    /// were not found.
    /// </summary>
    internal void UseSaved(bool add)
    {
        int i = SavedBox.SelectedIndex;
        var things = Things();
        if (i < 0 || i >= _saved.Count || things == null)
        {
            return;
        }
        var s = _saved[i];
        var (ids, missing) = SavedSelections.Find(s, things);
        SelectIds(add ? [.. SelectedIds().Concat(ids).Distinct()] : ids);
        Message?.Invoke(missing > 0 ? Said.Of("select.msg.savedMissing", ids.Count, s.Name, missing) : Said.Of("select.msg.saved", ids.Count, s.Name));
    }

    /// <summary>Forgets the chosen saved selection, once confirmed.</summary>
    internal async Task Forget()
    {
        int i = SavedBox.SelectedIndex;
        string? world = WorldName();
        if (world == null || i < 0 || i >= _saved.Count || !await Confirm(Lang.T("select.ask.forget.text", _saved[i].Name)))
        {
            return;
        }
        if (!SavedSelections.Forget(world, _saved[i].Name))
        {
            Message?.Invoke(Said.Of("select.msg.forgetFailed"));
            return;
        }
        SavedBox.SelectedIndex = -1;
        FillSaved();
    }
}
