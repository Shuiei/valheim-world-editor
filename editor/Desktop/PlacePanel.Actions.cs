using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The actions: place, clear, undo point, new layout and fit, and the mode and line shape buttons.
public sealed partial class PlacePanel
{
    /// <summary>Places what the line, grid or zone shows (Enter).</summary>
    internal Button PlaceButton { get; } = new Button { Content = Lang.T("place.label.place"), FontSize = 12 }.Classed("primary");

    /// <summary>Clears the line, grid box or zone.</summary>
    internal Button ClearButton { get; } = new() { Content = Lang.T("common.clearEsc"), FontSize = 12 };

    /// <summary>Removes the line's or zone's last point.</summary>
    internal Button UndoPointButton { get; } = new() { Content = Lang.T("place.label.undoPoint"), FontSize = 12 };

    /// <summary>New random choices (R).</summary>
    internal Button NewLayoutButton { get; } = new() { Content = Lang.T("place.label.newLayout"), FontSize = 12 };

    /// <summary>Sets the spacing to the widest chosen kind.</summary>
    internal Button FitButton { get; } = new() { Content = Lang.T("place.label.fit"), FontSize = 11, Padding = new Thickness(6, 2), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Kinds wider than the spacing: says so (the fit button then sets the spacing to them).</summary>
    internal TextBlock FitText { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The fit hint and its button, shown only when a chosen kind is wider than the spacing.</summary>
    private readonly Control _fitRow;

    /// <summary>The place, clear, undo point, new layout and fit actions.</summary>
    private void WireActions(PlaceInput input, PlaceTool t)
    {
        PlaceButton.Click += (_, _) => input.PlaceShape();
        ClearButton.Click += (_, _) => t.ClearShape();
        UndoPointButton.Click += (_, _) => input.RemoveLastPoint();
        NewLayoutButton.Click += (_, _) => t.NewLayout();
        FitButton.Tip("place.fit");
        FitButton.Click += (_, _) =>
        {
            if (t.Fit())
            {
                Fill();
                t.Notify();
            }
        };
    }

    /// <summary>The line shape buttons: points, circle and rectangle.</summary>
    private UniformGrid BuildShapeButtons(PlaceTool t)
    {
        var shapes = new UniformGrid { Rows = 1, ColumnSpacing = 4 };
        foreach (var (ls, label, tip) in new[] { (PlaceTool.LineShapes.Points, Lang.T("place.shape.points"), Lang.T("place.tip.points")),
            (PlaceTool.LineShapes.Circle, Lang.T("place.shape.circle"), Lang.T("place.tip.circle")), (PlaceTool.LineShapes.Rect, Lang.T("place.shape.rect"), Lang.T("place.tip.rect")) })
        {
            var b = new Button { Content = label, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
            ToolTip.SetTip(b, tip);
            b.Click += (_, _) => { t.LineShape = ls; t.ClearShape(); Sync(); };
            ShapeButtons[ls] = b;
            shapes.Children.Add(b);
        }
        return shapes;
    }

    /// <summary>Line mode: one button per shape (Points, Circle, Rectangle).</summary>
    internal Dictionary<PlaceTool.LineShapes, Button> ShapeButtons { get; } = [];

    /// <summary>One button per mode.</summary>
    internal Dictionary<PlaceTool.Modes, Button> ModeButtons { get; } = [];

    /// <summary>The mode buttons, one for each way of laying objects out.</summary>
    private StackPanel BuildModeRow()
    {
        var modes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var m in Enum.GetValues<PlaceTool.Modes>())
        {
            var b = new Button { Content = ModeName(m), FontSize = 12 };
            b.Click += (_, _) => SetMode(m);
            ModeButtons[m] = b;
            modes.Children.Add(b);
        }
        ModeButtons[PlaceTool.Modes.Brush].Tip("place.brush");
        ModeButtons[PlaceTool.Modes.Line].Tip("place.line");
        ModeButtons[PlaceTool.Modes.Grid].Tip("place.grid");
        ModeButtons[PlaceTool.Modes.Zone].Tip("place.zone");
        return modes;
    }
}
