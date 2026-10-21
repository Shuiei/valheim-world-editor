using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>
    /// The container's slots as in game, w × h: each shows its item and how many. Click one to choose
    /// what it holds (or empty it); Move, then another slot, moves the item there (or swaps the two).
    /// </summary>
    private void SlotGrid(int w, int h)
    {
        ItemsBox.Children.Add(SlotGridPanel(w, h));
        // The picked slot: its item (any of the game's), how many, how good.
        if (_slot is not var (sx, sy) || sx >= w || sy >= h)
        {
            ItemsBox.Children.Add(new TextBlock { Text = Lang.T("inspect.hint.clickSlot"), FontSize = 11, Foreground = Ui.Muted });
            return;
        }
        PickedSlot(sx, sy);
    }

    /// <summary>
    /// The grid of slot buttons, w columns by h rows, each holding the item in it.
    /// </summary>
    /// <param name="w">The columns.</param>
    /// <param name="h">The rows.</param>
    /// <returns>The grid.</returns>
    private Grid SlotGridPanel(int w, int h)
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
                var cell = SlotCell(x, y);
                Grid.SetColumn(cell, x);
                Grid.SetRow(cell, y);
                grid.Children.Add(cell);
            }
        }
        return grid;
    }
}
