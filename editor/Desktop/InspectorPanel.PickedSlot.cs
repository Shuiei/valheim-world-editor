using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>The picked slot's item box, as last drawn (null before a slot is picked).</summary>
    private AutoCompleteBox? _slotPick;

    /// <summary>
    /// The picked slot's controls under the grid: its item's name, how many and how good, Empty, and Move.
    /// </summary>
    /// <param name="sx">The picked slot's column.</param>
    /// <param name="sy">The picked slot's row.</param>
    private void PickedSlot(int sx, int sy)
    {
        var items = Items!;
        var item = items.FirstOrDefault(i => i.X == sx && i.Y == sy);
        var pick = new AutoCompleteBox
        {
            Text = item?.Name ?? "",
            ItemsSource = PrefabCatalog.Items,
            FilterMode = AutoCompleteFilterMode.ContainsOrdinal,
            FontSize = 11,
            MinimumPrefixLength = 1,
            PlaceholderText = Lang.T("inspect.hint.findItem"),
        }.Tip("inspect.slotItem");
        _slotPick = pick;
        pick.SelectionChanged += (_, _) => ChooseSlotItem(items, pick, sx, sy);
        pick.LostFocus += (_, _) => ChooseSlotItem(items, pick, sx, sy);
        var count = new NumericUpDown { Value = item?.Stack ?? 1, Minimum = 1, Maximum = MaxStack, Increment = 1, FormatString = "0", FontSize = 11, Width = 90, IsEnabled = item != null }.Tip("inspect.count");
        count.ValueChanged += (_, e) =>
        {
            if (item != null)
            {
                item.Stack = (int)(e.NewValue ?? 1);
                Dirty();
            }
        };
        var quality = new NumericUpDown { Value = item?.Quality ?? 1, Minimum = 1, Maximum = 10, Increment = 1, FormatString = "0", FontSize = 11, Width = 70, IsEnabled = item != null }.Tip("inspect.quality");
        quality.ValueChanged += (_, e) =>
        {
            if (item != null)
            {
                item.Quality = (int)(e.NewValue ?? 1);
                Dirty();
            }
        };
        var empty = new Button { Content = Lang.T("inspect.label.empty"), FontSize = 11, IsEnabled = item != null }.Tip("inspect.emptySlot");
        empty.Click += (_, _) =>
        {
            items.Remove(item!);
            Dirty();
            RenderItems();
        };
        var move = new Button { Content = _moving ? Lang.T("inspect.label.clickSlot") : Lang.T("inspect.label.move"), FontSize = 11, IsEnabled = item != null }.Tip("inspect.moveSlot");
        move.Click += (_, _) =>
        {
            _moving = !_moving;
            RenderItems();
        };
        ItemsBox.Children.Add(new TextBlock { Text = Lang.T("inspect.head.slot", sx + 1, sy + 1), FontSize = 10, Foreground = Ui.Muted });
        ItemsBox.Children.Add(pick);
        ItemsBox.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = Lang.T("inspect.label.howMany"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, count,
                new TextBlock { Text = Lang.T("inspect.data.quality"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, quality, empty, move,
            },
        });
    }
}
