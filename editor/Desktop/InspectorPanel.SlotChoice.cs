using Avalonia.Controls;
using Avalonia.VisualTree;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>
    /// Puts the name typed or picked in the picked slot's box into the slot: a new item when the slot is empty, or
    /// the existing item renamed. Nothing changes when the name is not a known item or is already the slot's.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="pick">The box the name is read from.</param>
    /// <param name="sx">The slot's column.</param>
    /// <param name="sy">The slot's row.</param>
    private void ChooseSlotItem(List<ItemRow> items, AutoCompleteBox pick, int sx, int sy)
    {
        string name = pick.Text?.Trim() ?? "";
        string? known = PrefabCatalog.Items.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        // What the slot holds now: a pick and the box then losing focus (once the grid is drawn
        // again) both come here, and must not put the item in twice.
        var now = items.FirstOrDefault(i => i.X == sx && i.Y == sy);
        if (known == null || known == now?.Name)
        {
            return;
        }
        // Typed (not picked from the list, nor the box left): the grid drawn again takes the box away, so the new one
        // takes the keys next typed, which would otherwise go to the window's shortcuts (P: Path, S: the camera…).
        bool typing = pick.IsKeyboardFocusWithin;
        if (now == null)
        {
            items.Add(new ItemRow { Name = known, X = sx, Y = sy });
        }
        else
        {
            now.Name = known;
        }
        Dirty();
        RenderItems();
        if (typing)
        {
            KeepTyping();
        }
    }

    /// <summary>
    /// The picked slot's box, drawn again, takes the keyboard back with the caret after its text, so typing goes on in it.
    /// </summary>
    private void KeepTyping()
    {
        if (_slotPick is not { } box)
        {
            return;
        }
        box.ApplyTemplate();
        box.Focus();
        // The box selects all its text as it takes the focus, which the next key would replace.
        if (box.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() is { } text)
        {
            text.ClearSelection();
            text.CaretIndex = text.Text?.Length ?? 0;
        }
    }
}
