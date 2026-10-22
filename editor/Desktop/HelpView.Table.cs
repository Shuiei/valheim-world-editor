using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace TerrainEditor.Desktop;

public sealed partial class HelpView
{
    /// <summary>The narrowest and widest share a table column gets, in characters of its longest cell.</summary>
    private const int MinColumn = 8, MaxColumn = 60;

    /// <summary>
    /// A table: its header row on a darker band, a line under each row, each column as wide as its cells need (in
    /// proportion, between MinColumn and MaxColumn characters), the text wrapped in it.
    /// </summary>
    private Border Table(Table table)
    {
        var rows = table.OfType<TableRow>().ToList();
        int columns = rows.Max(r => r.Count);
        var grid = new Grid();
        for (int c = 0; c < columns; c++)
        {
            int widest = rows.Max(r => c < r.Count ? HelpText.Of(r[c]).Length : 0);
            grid.ColumnDefinitions.Add(new ColumnDefinition(Math.Clamp(widest, MinColumn, MaxColumn), GridUnitType.Star));
        }
        for (int r = 0; r < rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (int c = 0; c < rows[r].Count; c++)
            {
                var cell = Cell((TableCell)rows[r][c], rows[r].IsHeader, r < rows.Count - 1);
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }
        return new Border { Child = grid, BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), ClipToBounds = true };
    }

    /// <summary>A table cell: its text (bold in the header, on a darker band), a line under it but in the last row.</summary>
    private Border Cell(TableCell cell, bool header, bool lineUnder)
    {
        var text = new StackPanel { Spacing = 4 };
        foreach (var p in cell.OfType<LeafBlock>())
        {
            var t = Text(p.Inline, 13, 19);
            if (header)
            {
                t.FontWeight = FontWeight.SemiBold;
            }
            text.Children.Add(t);
        }
        return new Border
        {
            Child = text,
            Background = header ? Ui.Panel2 : null,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(0, 0, 0, lineUnder ? 1 : 0),
            Padding = new Thickness(10, 6),
        };
    }
}
