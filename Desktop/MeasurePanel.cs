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
    /// The rows of <see cref="_rows"/> made so far, in order: those past the values shown are kept out of the grid
    /// for the next time.
    /// </summary>
    private readonly List<(TextBlock Label, SelectableTextBlock Value)> _rowsMade = [];
    /// <summary>How many of <see cref="_rowsMade"/>, from the first, are in the grid now.</summary>
    private int _rowsIn;
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
        Card = Ui.AsCard(new Border
        {
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
        });
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
        Shown.Clear();
        int r = 0;
        foreach (var (label, value) in rows)
        {
            // The rows are made once and kept: the tape changes at the mouse's pace.
            if (r == _rows.RowDefinitions.Count)
            {
                _rows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }
            if (r == _rowsMade.Count)
            {
                var l = new TextBlock { FontSize = 12, Foreground = Ui.Muted };
                var v = new SelectableTextBlock { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetRow(l, r);
                Grid.SetRow(v, r);
                Grid.SetColumn(v, 1);
                _rowsMade.Add((l, v));
            }
            var (labelBlock, valueBlock) = _rowsMade[r];
            if (r == _rowsIn)
            {
                _rows.Children.Add(labelBlock);
                _rows.Children.Add(valueBlock);
                _rowsIn++;
            }
            labelBlock.Text = label;
            valueBlock.Text = value;
            Shown[label] = value;
            r++;
        }
        // Rows not needed now leave the grid, kept for the next time, and so do their grid rows (no spacing left for
        // them; a control left on a grid row that is gone breaks the grid's layout).
        while (_rowsIn > r)
        {
            _rowsIn--;
            _rows.Children.Remove(_rowsMade[_rowsIn].Label);
            _rows.Children.Remove(_rowsMade[_rowsIn].Value);
        }
        while (_rows.RowDefinitions.Count > r)
        {
            _rows.RowDefinitions.RemoveAt(_rows.RowDefinitions.Count - 1);
        }
    }
}
