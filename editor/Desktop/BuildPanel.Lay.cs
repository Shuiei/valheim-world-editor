using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

/// <summary>
/// Lay, at the dock's foot: how the chosen piece is put down. One piece at a time under the cursor (as the hammer), or
/// end to end along a line of clicked points, round a circle or round a rectangle drawn on the ground, each piece
/// meeting the last at its snap points, stacked so many pieces high (Layers), the line closed back to its first point
/// (Closed); or in a grid over a box dragged on the ground, so far apart (Spacing: a floor of 2 m pieces). Enter (or
/// Place) puts the pieces down as one undo step. Kept between runs.
/// </summary>
public sealed partial class BuildPanel
{
    /// <summary>How the chosen piece is put down.</summary>
    internal enum Lays
    {
        /// <summary>One piece at a time under the cursor, as the hammer.</summary>
        One,
        /// <summary>End to end along a line of clicked points.</summary>
        Line,
        /// <summary>End to end round a circle (drag from its centre).</summary>
        Circle,
        /// <summary>End to end round a rectangle (drag from corner to corner).</summary>
        Rect,
        /// <summary>In a grid over a box (drag from corner to corner): a floor.</summary>
        Grid,
    }

    /// <summary>How the chosen piece is put down (the Place tool takes it when building starts).</summary>
    internal Lays Lay { get; private set; }

    /// <summary>
    /// Whether lines are offered: not building in a dungeon, whose rooms are far above the ground a line is drawn on.
    /// </summary>
    internal bool LinesOffered
    {
        get;
        private set
        {
            field = value;
            LayRow.IsVisible = value;
        }
    } = true;

    /// <summary>One piece, Line, Circle, Rectangle, Grid: a button each, tagged with its <see cref="Lays"/>.</summary>
    internal StackPanel LayButtons { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Along a line: how many pieces high each one is stacked, 1 to 20.</summary>
    internal NumericUpDown LayersBox { get; } = new() { Value = 1, Minimum = 1, Maximum = 20, Increment = 1, FormatString = "0", FontSize = 12, MinWidth = 96, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>A grid: the distance between pieces, centre to centre, in metres (2 m for the game's 2 m floors).</summary>
    internal NumericUpDown SpacingBox { get; } = new() { Value = 2, Minimum = 0.5m, Maximum = (decimal)PlaceTool.MaxCell, Increment = 0.5m, FormatString = "0.0#", FontSize = 12, MinWidth = 104, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>A line of points: closed back to its first point (with 3 or more).</summary>
    internal CheckBox ClosedBox { get; } = new() { Content = Lang.T("build.label.closed"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Puts down what the line shows (Enter).</summary>
    internal Button LayPlaceButton { get; } = new Button { Content = Lang.T("place.label.place"), FontSize = 11.5, Padding = new Thickness(8, 2), VerticalAlignment = VerticalAlignment.Center }.Classed("primary");

    /// <summary>Clears the line drawn (Esc).</summary>
    internal Button LayClearButton { get; } = new() { Content = Lang.T("place.label.clear"), FontSize = 11.5, Padding = new Thickness(8, 2), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The whole row at the dock's foot: its label, the buttons and the line's own settings.</summary>
    internal StackPanel LayRow { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The line's or grid's own settings (Layers, Closed or Spacing; Place, Clear), shown while laying one.</summary>
    private readonly StackPanel _lineParts = new() { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };

    /// <summary>Layers with its label (lines), Spacing with its label (a grid).</summary>
    private readonly StackPanel _layersPart = new() { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center },
        _spacingPart = new() { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Builds the Lay row and wires it.</summary>
    private void WireLay()
    {
        foreach (var lay in Enum.GetValues<Lays>())
        {
            var (label, tip) = lay switch
            {
                Lays.Line => ("build.lay.line", "build.layLine"),
                Lays.Circle => ("place.shape.circle", "build.layCircle"),
                Lays.Rect => ("place.shape.rect", "build.layRect"),
                Lays.Grid => ("place.mode.grid", "build.layGrid"),
                _ => ("build.lay.one", "build.layOne"),
            };
            var b = new Button { Content = Lang.T(label), FontSize = 11.5, Padding = new Thickness(8, 2), Tag = lay };
            b.Tip(tip);
            b.Click += (_, _) => SetLay(lay);
            LayButtons.Children.Add(b);
        }
        LayersBox.Tip("place.layers");
        ClosedBox.Tip("place.loop");
        LayPlaceButton.Tip("place.place");
        LayClearButton.Tip("place.clearShape");
        SpacingBox.Tip("place.cell");
        LayersBox.ValueChanged += (_, _) => { _prefs.Set("build.layers", Layers); TakeLay(); };
        SpacingBox.ValueChanged += (_, _) => { _prefs.Set("build.spacing", (double)Spacing); TakeLay(); };
        ClosedBox.IsCheckedChanged += (_, _) => TakeLay();
        LayPlaceButton.Click += (_, _) => _input.PlaceShape();
        LayClearButton.Click += (_, _) => Tool.ClearShape();
        _layersPart.Children.Add(new TextBlock { Text = Lang.T("place.label.layers"), FontSize = 12, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center });
        _layersPart.Children.Add(LayersBox);
        _spacingPart.Children.Add(new TextBlock { Text = Lang.T("place.label.spacing"), FontSize = 12, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center });
        _spacingPart.Children.Add(SpacingBox);
        _lineParts.Children.Add(_layersPart);
        _lineParts.Children.Add(_spacingPart);
        _lineParts.Children.Add(ClosedBox);
        _lineParts.Children.Add(LayPlaceButton);
        _lineParts.Children.Add(LayClearButton);
        LayRow.Children.Add(new TextBlock { Text = Lang.T("build.label.lay"), FontSize = 11, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center });
        LayRow.Children.Add(LayButtons);
        LayRow.Children.Add(_lineParts);
        LayRow.Children.Add(new Border { Width = 1, Height = 22, Background = Ui.Line, VerticalAlignment = VerticalAlignment.Center });
        ShowLay();
    }

    /// <summary>How many pieces high a line's pieces are stacked.</summary>
    internal int Layers => (int)Math.Clamp(LayersBox.Value ?? 1, 1, 20);

    /// <summary>A grid's spacing, centre to centre, in metres.</summary>
    internal float Spacing => (float)Math.Clamp(SpacingBox.Value ?? 2, 0.5m, (decimal)PlaceTool.MaxCell);

    /// <summary>Takes how pieces were laid last and how high from the window's settings (kept there from now on).</summary>
    /// <param name="prefs">The window's settings.</param>
    private void RememberLay(Prefs prefs)
    {
        LayersBox.Value = Math.Clamp(prefs.Get("build.layers", 1), 1, 20);
        SpacingBox.Value = (decimal)Math.Clamp(prefs.Get("build.spacing", 2.0), 0.5, PlaceTool.MaxCell);
        SetLay(Enum.TryParse(prefs.Get("build.lay", ""), out Lays lay) ? lay : Lays.One);
    }

    /// <summary>Puts pieces down this way from now on (and the next time), its button lit; a line half drawn is cleared.</summary>
    /// <param name="lay">One piece, or along a line, circle or rectangle.</param>
    internal void SetLay(Lays lay)
    {
        Lay = lay;
        _prefs.Set("build.lay", lay.ToString());
        if (Tool.Building)
        {
            Tool.ClearShape();
        }
        TakeLay();
    }

    /// <summary>The buttons lit and the line's own settings shown as <see cref="Lay"/> has them.</summary>
    private void ShowLay()
    {
        var lay = LinesOffered ? Lay : Lays.One;
        foreach (var b in LayButtons.Children.OfType<Button>())
        {
            b.Classes.Set("on", (Lays)b.Tag! == lay);
        }
        _lineParts.IsVisible = lay != Lays.One;
        _layersPart.IsVisible = lay is Lays.Line or Lays.Circle or Lays.Rect;
        _spacingPart.IsVisible = lay == Lays.Grid;
        ClosedBox.IsVisible = lay == Lays.Line;
    }

    /// <summary>
    /// Gives the Place tool how pieces are laid, while building: one at a time under the cursor, end to end along
    /// straight stretches between the points (a circle, a rectangle), stacked and closed as chosen, or in a grid.
    /// </summary>
    private void TakeLay()
    {
        ShowLay();
        var t = Tool;
        if (!t.Building)
        {
            return;
        }
        var lay = LinesOffered ? Lay : Lays.One;
        t.Mode = lay switch
        {
            Lays.One => PlaceTool.Modes.Brush,
            Lays.Grid => PlaceTool.Modes.Grid,
            _ => PlaceTool.Modes.Line,
        };
        t.Cell = Spacing;
        t.LineShape = lay switch
        {
            Lays.Circle => PlaceTool.LineShapes.Circle,
            Lays.Rect => PlaceTool.LineShapes.Rect,
            _ => PlaceTool.LineShapes.Points,
        };
        t.EndToEnd = t.EndToEndByHand = true;
        t.Curve = false;
        t.Layers = Layers;
        t.Loop = lay == Lays.Line && ClosedBox.IsChecked == true;
        _input.Refresh();
        t.Notify();
        ShowSnapPoint();
    }
}
