using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The readout for a point of the 3D view (null: the pointer left it).
    /// </summary>
    internal void ShowCursor(Point? at)
    {
        _cursorAt = at;
        CursorText.Text = at is Point p && View.Scene is { } s && Surface is { } surface && View.GridAt(p, surface.Bounds.Size) is { } g ? CursorReadout.Text(s, g.X, g.Y) : "";
    }

    /// <summary>
    /// The status bar (bottom): the cursor, the last message, the selection, walking or flying, and the controls.
    /// </summary>
    private Border StatusBar()
    {
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"), ColumnSpacing = 18 };
        var tips = new TextBlock { Text = Lang.T("help.statusTips"), FontSize = 12, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center };
        _selection.VerticalAlignment = _eye.VerticalAlignment = VerticalAlignment.Center;
        _selection.TextWrapping = _eye.TextWrapping = TextWrapping.NoWrap;
        _selection.MaxWidth = 380;
        _selection.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(MessageText, 1);
        Grid.SetColumn(_selection, 2);
        Grid.SetColumn(_eye, 3);
        Grid.SetColumn(tips, 4);
        bar.Children.Add(CursorText);
        bar.Children.Add(MessageText);
        bar.Children.Add(_selection);
        bar.Children.Add(_eye);
        bar.Children.Add(tips);
        var card = Ui.Card(bar);
        card.Margin = new Thickness(10, 0, 10, 10);
        card.VerticalAlignment = VerticalAlignment.Bottom;
        // A message on two lines makes the bar taller: the panels stop above it still.
        card.SizeChanged += (_, e) => KeepAboveStatusBar(e.NewSize.Height);
        return card;
    }

    /// <summary>
    /// Where the panels over the view stop at the bottom: above the status bar (58 px with its usual one line).
    /// </summary>
    private double _panelsBottom = 58;

    /// <summary>
    /// The tool column, the right-hand panels and the locations note end above the status bar, as tall as it is now.
    /// </summary>
    /// <param name="barHeight">The status bar's height.</param>
    private void KeepAboveStatusBar(double barHeight)
    {
        _panelsBottom = Math.Max(58, barHeight + 18);
        foreach (var c in new[] { Tools.Rail.Parent as Control, ViewPanelCard, History.Card, Inspector.Card, HelpCard, LocationNote }.OfType<Control>())
        {
            c.Margin = new Thickness(c.Margin.Left, c.Margin.Top, c.Margin.Right, _panelsBottom);
        }
    }

    /// <summary>
    /// Help: the controls (the web editor's list), then what the program knows about this computer.
    /// </summary>
    /// <param name="record">The Record frame rates switch, shown at its end.</param>
    private Border Help(CheckBox record)
    {
        var list = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12, RowSpacing = 5 };
        var rows = new (string Keys, string What)[]
        {
            (Lang.T("help.leftDrag.keys"), Lang.T("help.leftDrag.what")), (Lang.T("help.rightDrag.keys"), Lang.T("help.rightDrag.what")),
            (Lang.T("help.wheel.keys"), Lang.T("help.wheel.what")), (Lang.T("help.middleDrag.keys"), Lang.T("help.middleDrag.what")),
            (Lang.T("help.move.keys"), Lang.T("help.move.what")), (Lang.T("help.walk.keys"), Lang.T("help.walk.what")),
            (Lang.T("help.sculpt.keys"), Lang.T("help.sculpt.what")), (Lang.T("help.tools.keys"), Lang.T("help.tools.what")),
            (Lang.T("help.select.keys"), Lang.T("help.select.what")), (Lang.T("help.brushSize.keys"), Lang.T("help.brushSize.what")),
            (Lang.T("help.copy.keys"), Lang.T("help.copy.what")), (Lang.T("help.turn.keys"), Lang.T("help.turn.what")),
            (Lang.T("help.layout.keys"), Lang.T("help.layout.what")), (Lang.T("help.pickHeight.keys"), Lang.T("help.pickHeight.what")),
            (Lang.T("help.delete.keys"), Lang.T("help.delete.what")), (Lang.T("help.inspect.keys"), Lang.T("help.inspect.what")),
            (Lang.T("help.lift.keys"), Lang.T("help.lift.what")), (Lang.T("help.undo.keys"), Lang.T("help.undo.what")),
            (Lang.T("help.save.keys"), Lang.T("common.save")), (Lang.T("help.panels.keys"), Lang.T("help.panels.what")),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            list.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var k = new TextBlock { Text = rows[i].Keys, FontSize = 12.5 };
            var w = new TextBlock { Text = rows[i].What, FontSize = 12.5, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
            Grid.SetRow(k, i);
            Grid.SetRow(w, i);
            Grid.SetColumn(w, 1);
            list.Children.Add(k);
            list.Children.Add(w);
        }
        var close = new Button { Content = Icons.Make("close", 14), Padding = new Thickness(5) }.Classed("ghost").Tip("card.close");
        close.Click += (_, _) => ShowRight(null);
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { Ui.Title(Lang.T("help.head.controls"), HelpTopics.Controls) } };
        Grid.SetColumn(close, 1);
        head.Children.Add(close);
        record.Classes.Add("switch");
        var card = Ui.Card(new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    head, list,
                    Ui.Hint(Lang.T("help.hint.limit")),
                    Ui.Hint(Lang.T("help.hint.edges")),
                    Ui.Heading(Lang.T("help.head.computer")),
                    _fps, LoadedInfo, record,
                },
            },
        });
        card.Width = 330;
        card.IsVisible = false;
        return card;
    }
}
