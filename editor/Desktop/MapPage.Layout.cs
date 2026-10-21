using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

/// <summary>
/// Small layout helpers: the collapsible section, a button row's spacing, and a grid column.
/// </summary>
public sealed partial class MapPage
{
    /// <summary>
    /// A collapsible section of the side panel.
    /// </summary>
    /// <param name="title">Its header.</param>
    /// <param name="open">Whether it starts open.</param>
    /// <param name="children">Its contents, top to bottom.</param>
    private static Expander Section(string title, bool open, params Control[] children)
    {
        var panel = new StackPanel { Spacing = 6 };
        foreach (var c in children)
        {
            panel.Children.Add(c);
        }
        return new Expander { Header = new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeight.SemiBold }, IsExpanded = open, Content = panel, HorizontalAlignment = HorizontalAlignment.Stretch };
    }

    /// <summary>
    /// <paramref name="c"/> with a margin to its right and below, for a wrapping row of buttons.
    /// </summary>
    private static Control Spaced(Control c)
    {
        c.Margin = new Thickness(0, 0, 6, 4);
        return c;
    }

    /// <summary>
    /// <paramref name="c"/> set in a grid column; column 2 is right-aligned, others take <paramref name="align"/>.
    /// </summary>
    private static Control Col(Control c, int col, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        Grid.SetColumn(c, col);
        c.HorizontalAlignment = col == 2 ? HorizontalAlignment.Right : align;
        return c;
    }
}
