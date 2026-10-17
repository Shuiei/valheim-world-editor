using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Terrain;

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
            var name = new AutoCompleteBox { Text = it.Name, ItemsSource = PrefabCatalog.Items, FilterMode = AutoCompleteFilterMode.ContainsOrdinal, FontSize = 11, MinimumPrefixLength = 2 }.Tip("inspect.item");
            name.PropertyChanged += (_, e) => { if (e.Property == AutoCompleteBox.TextProperty) { it.Name = name.Text?.Trim() ?? ""; Dirty(); } };
            NumericUpDown Num(int v, int min, int max, Action<int> set, string tip)
            {
                var n = new NumericUpDown { Value = v, Minimum = min, Maximum = max, Increment = 1, FormatString = "0", FontSize = 11, ShowButtonSpinner = false }.Tip(tip);
                n.ValueChanged += (_, e) => { set((int)(e.NewValue ?? min)); Dirty(); };
                return n;
            }
            // Durability points as the game saves them (not a percentage), fractions kept.
            var dur = new NumericUpDown
            {
                Value = float.IsFinite(it.Durability) ? (decimal)Math.Clamp(it.Durability, 0, MaxDurability) : 0,
                Minimum = 0,
                Maximum = MaxDurability,
                Increment = 1,
                FormatString = "0.##",
                FontSize = 11,
                ShowButtonSpinner = false,
            }.Tip("inspect.durability");
            dur.ValueChanged += (_, e) => { it.Durability = (float)(e.NewValue ?? 0); Dirty(); };
            var x = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(4, 0) };
            x.Tip("inspect.removeItem");
            x.Click += (_, _) => { Items.Remove(it); RenderItems(); Dirty(); };
            bool bad = w > 0 && (it.X >= w || it.Y >= h);
            rows.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,54,44,50,40,40,26"),
                ColumnSpacing = 3,
                Children =
                {
                    name,
                    Ui.Col(Num(it.Stack, 1, MaxStack, v => it.Stack = v, "inspect.count"), 1),
                    Ui.Col(Num(it.Quality, 1, 10, v => it.Quality = v, "inspect.quality"), 2),
                    Ui.Col(dur, 3),
                    Ui.Col(WithBorder(Num(it.X, 0, 255, v => it.X = v, "inspect.slotX"), bad), 4),
                    Ui.Col(WithBorder(Num(it.Y, 0, 255, v => it.Y = v, "inspect.slotY"), bad), 5),
                    Ui.Col(x, 6),
                },
            });
        }
        var add = new Button { Content = Lang.T("inspect.label.addItem"), FontSize = 11 }.Tip("inspect.addItem");
        add.Click += (_, _) =>
        {
            if (FreeSlot() is not var (fx, fy))
            {
                Message?.Invoke(Said.Of("inspect.msg.noFreeSlot"));
                return;
            }
            Items.Add(new ItemRow { X = fx, Y = fy });
            RenderItems();
            Dirty();
        };
        var tidy = new Button { Content = Lang.T("inspect.label.tidy"), FontSize = 11 };
        tidy.Tip("inspect.tidy");
        tidy.Click += (_, _) =>
        {
            int width = w > 0 ? w : 4;
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].X = i % width;
                Items[i].Y = i / width;
            }
            RenderItems();
            Dirty();
        };
        rows.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { add, tidy } });
    }
}
