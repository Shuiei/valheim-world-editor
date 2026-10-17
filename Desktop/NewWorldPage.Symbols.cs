using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

/// <summary>
/// The landmarks' switches, colours and marks, as drawn on the map and in the lists.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// A switch for one kind of mark, with its mark beside the name.
    /// </summary>
    private static CheckBox Toggle(string kind, string name, string text) => new CheckBox
    {
        IsChecked = true,
        FontSize = 12.5,
        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Mark(kind, name), new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } } },
    }.Classed("switch");

    /// <summary>
    /// The landmark kinds' looks: the start red, bosses purple diamonds, each trader a colour of its own.
    /// </summary>
    private static SolidColorBrush Fill(string kind, string name = "") => new(kind switch
    {
        "start" => Color.FromRgb(230, 40, 40),
        "boss" => Color.FromRgb(176, 96, 232),
        _ => name switch
        {
            "The Bog Witch" => Color.FromRgb(120, 206, 92),
            "Hildir" => Color.FromRgb(244, 120, 176),
            _ => Color.FromRgb(244, 196, 64),
        },
    });

    /// <summary>
    /// A landmark's mark: a diamond for a boss, a dot otherwise (the start's larger, outlined white).
    /// </summary>
    private static Control Mark(string kind, string name = "")
    {
        var outline = new SolidColorBrush(Color.FromRgb(20, 20, 24));
        return kind == "boss"
            ? new Border { Width = 9, Height = 9, Background = Fill(kind), BorderBrush = outline, BorderThickness = new Thickness(1), RenderTransform = new RotateTransform(45) }
            : new Avalonia.Controls.Shapes.Ellipse { Width = kind == "start" ? 11 : 9, Height = kind == "start" ? 11 : 9, Fill = Fill(kind, name), Stroke = kind == "start" ? Brushes.White : outline, StrokeThickness = kind == "start" ? 2 : 1 };
    }
}
