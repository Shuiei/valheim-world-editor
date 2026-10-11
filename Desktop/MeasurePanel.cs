using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Measure tool's panel: what the tape measures, and Clear.
/// </summary>
public sealed class MeasurePanel
{
    /// <summary>
    /// The panel's card.
    /// </summary>
    public Control Card { get; }
    /// <summary>
    /// The tape's hint: what to click next.
    /// </summary>
    private readonly TextBlock _hint = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// The measured values, a label and a value per row.
    /// </summary>
    private readonly Grid _rows = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12, RowSpacing = 3 };
    /// <summary>
    /// The 3D view whose tape the panel shows.
    /// </summary>
    private readonly GlView _view;
    /// <summary>
    /// Clears the tape.
    /// </summary>
    internal Button ClearButton { get; } = new() { Content = "Clear (Esc)", FontSize = 12 };
    /// <summary>
    /// For tests: the values shown, by label.
    /// </summary>
    internal Dictionary<string, string> Shown { get; } = [];

    /// <summary>
    /// Builds the card for the view's tape; it refreshes whenever the tape changes.
    /// </summary>
    public MeasurePanel(GlView view)
    {
        _view = view;
        ClearButton.Click += (_, _) => view.Tape.Clear();
        ClearButton.Tip("measure.clear");
        view.Tape.Changed += Refresh;
        Card = new Border
        {
            Background = Ui.Panel,
            BorderBrush = Ui.Line,
            BoxShadow = BoxShadows.Parse("0 6 24 0 #59000000"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = Ui.Pad,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Width = 280,
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = "Measure", FontSize = 14, FontWeight = FontWeight.SemiBold },
                    _hint, _rows, ClearButton,
                    new TextBlock { Text = "Slope colours and height lines are in the View panel (Look).", FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap },
                },
            },
        };
        Refresh();
    }

    /// <summary>
    /// Shows the tape's hint and values again, against the ground as it is now (water at 30 m when no world is open).
    /// </summary>
    public void Refresh()
    {
        float water = _view.Scene?.Water ?? 30;
        var (hint, rows) = _view.Tape.Describe(_view.GroundHeight, water);
        _hint.Text = hint;
        _rows.Children.Clear();
        _rows.RowDefinitions.Clear();
        Shown.Clear();
        foreach (var (label, value) in rows)
        {
            _rows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            int r = _rows.RowDefinitions.Count - 1;
            var l = new TextBlock { Text = label, FontSize = 12, Foreground = Ui.Muted };
            var v = new SelectableTextBlock { Text = value, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetRow(l, r);
            Grid.SetRow(v, r);
            Grid.SetColumn(v, 1);
            _rows.Children.Add(l);
            _rows.Children.Add(v);
            Shown[label] = value;
        }
    }
}
