using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>
    /// An item stand's item and orientation, or an armour stand's slots and pose; each list offers only
    /// what the game lets that stand or slot take.
    /// </summary>
    private void RenderStand()
    {
        if (Stand == null || _z == null)
        {
            return;
        }
        int prefab = _z.Prefab;
        ItemsBox.Children.Add(new TextBlock { Text = _armourStand ? Lang.T("inspect.head.wears") : Lang.T("inspect.head.holds"), FontSize = 10, Foreground = Ui.Muted });
        ItemsBox.Children.Add(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,44,44,26"),
            ColumnSpacing = 3,
            Children = { Head(Lang.T("inspect.data.item"), 0), Head(Lang.T("inspect.head.quality"), 1), Head(Lang.T("inspect.head.style"), 2) },
        });
        foreach (var row in Stand)
        {
            var choices = PrefabCatalog.Items.Where(n => _armourStand
                ? PrefabCatalog.ItemKindOf(StableHash.Of(n)) is { } k && StandData.FitsArmourSlot(row.Types, k)
                : StandData.ItemStandRefusal(prefab, n) == null).ToList();
            string slotName = row.Types.Length == 0 ? Lang.T("inspect.anything") : string.Join(", ", row.Types.Select(t => StandData.TypeWords(t).Shown).Distinct());
            var name = new AutoCompleteBox
            {
                Text = row.Name,
                ItemsSource = choices,
                FilterMode = AutoCompleteFilterMode.ContainsOrdinal,
                FontSize = 11,
                MinimumPrefixLength = 1,
                PlaceholderText = _armourStand ? slotName : Lang.T("inspect.hint.findItem"),
            }.Tip("inspect.standItem");
            name.PropertyChanged += (_, e) =>
            {
                if (e.Property == AutoCompleteBox.TextProperty)
                {
                    row.Name = name.Text?.Trim() ?? "";
                    Dirty();
                }
            };
            var empty = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(4, 0), IsEnabled = row.Name != "" }.Tip("inspect.emptyStand");
            empty.Click += (_, _) =>
            {
                row.Name = "";
                Dirty();
                RenderItems();
                RenderStand();
            };
            if (_armourStand)
            {
                ItemsBox.Children.Add(new TextBlock { Text = slotName, FontSize = 10, Foreground = Ui.Muted });
            }
            ItemsBox.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,44,44,26"),
                ColumnSpacing = 3,
                Children =
                {
                    name,
                    Ui.Col(StandNum(row.Quality, 1, 10, v => row.Quality = v, "inspect.quality"), 1),
                    Ui.Col(StandNum(row.Variant, 0, 99, v => row.Variant = v, "inspect.variant"), 2),
                    Ui.Col(empty, 3),
                },
            });
        }
        ItemsBox.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = _armourStand ? Lang.T("inspect.label.pose") : Lang.T("inspect.label.hangs"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center },
                StandNum(StandWay, 0, 99, v => StandWay = v, _armourStand ? "inspect.pose" : "inspect.orientation"),
            },
        });
        if (_standError != null)
        {
            ItemsBox.Children.Add(new TextBlock { Text = _standError.Shown, FontSize = 11, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap });
        }
    }

    /// <summary>A number box for the stand section that marks the card changed.</summary>
    private NumericUpDown StandNum(int v, int min, int max, Action<int> set, string tip)
    {
        var n = new NumericUpDown { Value = v, Minimum = min, Maximum = max, Increment = 1, FormatString = "0", FontSize = 11, ShowButtonSpinner = false, Width = 60 }.Tip(tip);
        n.ValueChanged += (_, e) =>
        {
            set((int)(e.NewValue ?? min));
            Dirty();
        };
        return n;
    }
}
