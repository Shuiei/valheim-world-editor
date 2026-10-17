using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>
    /// The contents section: the slot grid (size known) and every item's details, with Add item and Tidy slots.
    /// </summary>
    private void RenderItems()
    {
        ItemsBox.Children.Clear();
        // A mod's manifest can ask for the contents (the "items" value) to be shown only.
        ItemsBox.IsEnabled = !ItemsReadOnly;
        if (Items == null)
        {
            return;
        }
        var (w, h) = _grid;
        ItemsBox.Children.Add(new TextBlock { Text = w > 0 ? Lang.T("inspect.head.contents", w, h, Items.Count) : Lang.T("inspect.head.contentsUnknown", Items.Count), FontSize = 10, Foreground = Ui.Muted });
        if (_inventoryError != null)
        {
            ItemsBox.Children.Add(new TextBlock { Text = Lang.T("inspect.error.contents", _inventoryError), FontSize = 11, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap });
            return;
        }
        if (w > 0 && h > 0)
        {
            SlotGrid(w, h);
        }
        var rows = new StackPanel { Spacing = 3 };
        var details = new Expander
        {
            Header = new TextBlock { Text = Lang.T("inspect.label.details"), FontSize = 11 },
            Content = rows,
            IsExpanded = w <= 0 || h <= 0 || Items.Any(i => i.X >= w || i.Y >= h),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(0),
        }.Tip("inspect.details");
        ItemsBox.Children.Add(details);
        rows.Children.Add(ItemHeads());
        foreach (var it in Items.ToList())
        {
            rows.Children.Add(ItemRowGrid(it, w, h));
        }
        rows.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { AddItemButton(), TidyButton(w) } });
    }
}
