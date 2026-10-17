using System.Globalization;
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
    /// The container's slots as in game, w × h: each shows its item and how many. Click one to choose
    /// what it holds (or empty it); Move, then another slot, moves the item there (or swaps the two).
    /// </summary>
    private void SlotGrid(int w, int h)
    {
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        for (int x = 0; x < w; x++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Math.Min(46, 376.0 / w))));
        }
        for (int y = 0; y < h; y++)
        {
            grid.RowDefinitions.Add(new RowDefinition(new GridLength(46)));
        }
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var at = (x, y);
                var it = Items!.FirstOrDefault(i => i.X == x && i.Y == y);
                var cell = new Button
                {
                    Margin = new Thickness(1),
                    Padding = new Thickness(2),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch,
                    Background = it != null ? Ui.Panel2 : Ui.Bg,
                    BorderThickness = new Thickness(_slot == at ? 2 : 1),
                    BorderBrush = _slot == at ? new SolidColorBrush(Ui.AccentColor) : Ui.Line,
                    Content = it == null ? null : new Grid
                    {
                        Children =
                        {
                            new TextBlock { Text = it.Name, FontSize = 9, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2 },
                            new TextBlock { Text = it.Stack > 1 ? it.Stack.ToString(CultureInfo.InvariantCulture) : "", FontSize = 11, FontWeight = FontWeight.SemiBold,
                                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom },
                        },
                    },
                }.Tip("inspect.slot");
                if (it != null)
                {
                    ToolTip.SetTip(cell, $"{it.Name} × {it.Stack} (slot {x + 1}, {y + 1})");
                }
                cell.Click += (_, _) =>
                {
                    if (_moving && _slot is var (fx, fy) && _slot != at)
                    {
                        var from = Items!.FirstOrDefault(i => i.X == fx && i.Y == fy);
                        var to = Items!.FirstOrDefault(i => i.X == at.x && i.Y == at.y);
                        if (from != null)
                        {
                            (from.X, from.Y) = at;
                        }
                        if (to != null)
                        {
                            (to.X, to.Y) = (fx, fy);
                        }
                        Dirty();
                    }
                    _moving = false;
                    _slot = at;
                    RenderItems();
                };
                Grid.SetColumn(cell, x);
                Grid.SetRow(cell, y);
                grid.Children.Add(cell);
            }
        }
        ItemsBox.Children.Add(grid);
        if (_slot is not var (sx, sy) || sx >= w || sy >= h)
        {
            ItemsBox.Children.Add(new TextBlock { Text = Lang.T("inspect.hint.clickSlot"), FontSize = 11, Foreground = Ui.Muted });
            return;
        }
        // The picked slot: its item (any of the game's), how many, how good.
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
        void Chosen()
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
        }
        pick.SelectionChanged += (_, _) => Chosen();
        pick.LostFocus += (_, _) => Chosen();
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
