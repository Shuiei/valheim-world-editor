using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The rows of the lists beside the map: a titled section, and one row with its mark, name, value and detail.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// A titled list of rows.
    /// </summary>
    private static StackPanel Section(string title, params Control[] items)
    {
        var list = new StackPanel { Spacing = 2 };
        list.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        foreach (var c in items)
        {
            list.Children.Add(c);
        }
        return list;
    }

    /// <summary>
    /// One row: a mark, the name, the value, a detail under it; show: what clicking it does.
    /// </summary>
    private static Border Item(Control? mark, string name, string value, string detail = "", Action? show = null)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("18,*,Auto") };
        if (mark != null)
        {
            mark.VerticalAlignment = VerticalAlignment.Center;
            mark.HorizontalAlignment = HorizontalAlignment.Left;
            row.Children.Add(mark);
        }
        var label = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = name, FontSize = 12.5 } } };
        if (detail.Length > 0)
        {
            label.Children.Add(new TextBlock { Text = detail, FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap });
        }
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        var v = new TextBlock { Text = value, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        Grid.SetColumn(v, 2);
        row.Children.Add(v);
        var item = new Border { Child = row, Padding = new Thickness(6, 4), CornerRadius = new CornerRadius(5), Background = Brushes.Transparent };
        if (show != null)
        {
            item.Cursor = new Cursor(StandardCursorType.Hand);
            ToolTip.SetTip(item, Lang.T("newWorld.showOnMap"));
            item.PointerEntered += (_, _) => item.Background = Ui.Panel2;
            item.PointerExited += (_, _) => item.Background = Brushes.Transparent;
            item.PointerPressed += (_, _) => show();
        }
        return item;
    }
}
