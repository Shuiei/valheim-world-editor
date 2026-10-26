using Avalonia;
using Avalonia.Controls;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>
    /// One item's row in the contents section: its name, stack, quality, durability, and slot, then its ✕.
    /// </summary>
    /// <param name="it">The item.</param>
    /// <param name="w">The slot grid's columns (0 when unknown).</param>
    /// <param name="h">The slot grid's rows (0 when unknown).</param>
    /// <returns>The row.</returns>
    private Grid ItemRowGrid(ItemRow it, int w, int h)
    {
        var name = new AutoCompleteBox { Text = it.Name, ItemsSource = PrefabCatalog.Items, FilterMode = AutoCompleteFilterMode.ContainsOrdinal, FontSize = 11, MinimumPrefixLength = 2 }.Tip("inspect.item");
        name.PropertyChanged += (_, e) => { if (e.Property == AutoCompleteBox.TextProperty) { it.Name = name.Text?.Trim() ?? ""; Dirty(); } };
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
        x.Click += (_, _) => { Items!.Remove(it); RenderItems(); Dirty(); };
        bool bad = w > 0 && (it.X >= w || it.Y >= h);
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,54,44,50,40,40,26"),
            ColumnSpacing = 3,
            Children =
            {
                name,
                Ui.Col(NumericBox(it.Stack, 1, MaxStack, v => it.Stack = v, "inspect.count"), 1),
                Ui.Col(NumericBox(it.Quality, 1, 10, v => it.Quality = v, "inspect.quality"), 2),
                Ui.Col(dur, 3),
                Ui.Col(WithBorder(NumericBox(it.X, 0, 255, v => it.X = v, "inspect.slotX"), bad), 4),
                Ui.Col(WithBorder(NumericBox(it.Y, 0, 255, v => it.Y = v, "inspect.slotY"), bad), 5),
                Ui.Col(x, 6),
            },
        };
    }

    /// <summary>
    /// A whole-number box that passes each new value to <paramref name="set"/> and marks the panel dirty.
    /// </summary>
    /// <param name="v">The starting value.</param>
    /// <param name="min">The lowest value.</param>
    /// <param name="max">The highest value.</param>
    /// <param name="set">Takes the new value.</param>
    /// <param name="tip">The tip's key.</param>
    /// <returns>The box.</returns>
    private NumericUpDown NumericBox(int v, int min, int max, Action<int> set, string tip)
    {
        var n = new NumericUpDown { Value = v, Minimum = min, Maximum = max, Increment = 1, FormatString = "0", FontSize = 11, ShowButtonSpinner = false }.Tip(tip);
        n.ValueChanged += (_, e) => { set((int)(e.NewValue ?? min)); Dirty(); };
        return n;
    }

    /// <summary>
    /// The Add item button: it takes the first free slot, or says there is none.
    /// </summary>
    /// <returns>The button.</returns>
    private Button AddItemButton()
    {
        var add = new Button { Content = Lang.T("inspect.label.addItem"), FontSize = 11 }.Tip("inspect.addItem");
        add.Click += (_, _) =>
        {
            if (FreeSlot() is not var (fx, fy))
            {
                Message?.Invoke(Said.Of("inspect.msg.noFreeSlot"));
                return;
            }
            Items!.Add(new ItemRow { X = fx, Y = fy });
            RenderItems();
            Dirty();
        };
        return add;
    }

    /// <summary>
    /// The Tidy slots button: lays the items out in rows of the grid's width, in their list order.
    /// </summary>
    /// <param name="w">The slot grid's columns (0 when unknown).</param>
    /// <returns>The button.</returns>
    private Button TidyButton(int w)
    {
        var tidy = new Button { Content = Lang.T("inspect.label.tidy"), FontSize = 11 };
        tidy.Tip("inspect.tidy");
        tidy.Click += (_, _) =>
        {
            int width = TidyWidth(w);
            for (int i = 0; i < Items!.Count; i++)
            {
                (Items[i].X, Items[i].Y) = TidiedSlot(i, width);
            }
            RenderItems();
            Dirty();
        };
        return tidy;
    }

    /// <summary>
    /// The row width Tidy slots lays the items out in: the grid's columns, or 4 when the grid's size is unknown.
    /// </summary>
    /// <param name="w">The slot grid's columns (0 when unknown).</param>
    /// <returns>The width.</returns>
    internal static int TidyWidth(int w) => w > 0 ? w : 4;

    /// <summary>
    /// The slot an item at <paramref name="index"/> of the list moves to when the items are laid out in rows.
    /// </summary>
    /// <param name="index">The item's place in the list.</param>
    /// <param name="width">The row width.</param>
    /// <returns>The column and row.</returns>
    internal static (int X, int Y) TidiedSlot(int index, int width) => (index % width, index / width);
}
