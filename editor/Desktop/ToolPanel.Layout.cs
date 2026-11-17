using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ToolPanel
{
    /// <summary>Builds the rail and the options, with View in use.</summary>
    public ToolPanel()
    {
        var rail = _rail = new StackPanel { Spacing = 0 };
        // A rail button: its picture (the web editor's icon, or the paint's colour), the name and the key.
        Button Add(string label, string key, Control picture)
        {
            var b = RailButton(label, key, picture);
            rail.Children.Add(b);
            return b;
        }
        Button Make(string label, string key, string icon) => Add(label, key, Icons.Make(icon, 16));
        void Cap(string text) => rail.Children.Add(new TextBlock { Text = text.ToUpperInvariant(), FontSize = 9, Foreground = Ui.Muted, LetterSpacing = 0.7, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, rail.Children.Count == 0 ? 0 : 5, 0, 1) });
        // Sculpt (its brushes, then Paint: one button, the paint chosen in its options), then the tools that draw on the
        // world (Area, Path, Place), the tools that make things, and last the tools that only look or pick.
        Cap(Lang.T("rail.cap.sculpt"));
        foreach (var (t, key) in Keys.Where(k => !IsPaint(k.Tool)))
        {
            var b = Make(Brush.Label(t), key, Icon(t));
            ToolTip.SetTip(b, Lang.T("rail.brushTip", Brush.Label(t), key, Brush.Help(t)));
            b.Click += (_, _) => Choose(t);
            _buttons[t] = b;
        }
        _paintSwatch.Background = PaintColour(Paint);
        _paintButton = Add(Lang.T("rail.label.paint"), KeyOf(Paints[0]) + "-" + KeyOf(Paints[^1]), _paintSwatch);
        ToolTip.SetTip(_paintButton, Lang.T("rail.tip.paint"));
        _paintButton.Click += (_, _) => Choose(Paint);
        Cap(Lang.T("rail.cap.edit"));
        _areaButton = Make(Lang.T("rail.label.area"), "B", "area");
        ToolTip.SetTip(_areaButton, Lang.T("rail.tip.area"));
        _areaButton.Click += (_, _) => ChooseMode(ToolMode.Area);
        _pathButton = Make(Lang.T("rail.label.path"), "P", "path");
        ToolTip.SetTip(_pathButton, Lang.T("rail.tip.path"));
        _pathButton.Click += (_, _) => ChooseMode(ToolMode.Path);
        _placeButton = Make(Lang.T("rail.label.place"), "T", "place");
        ToolTip.SetTip(_placeButton, Lang.T("rail.tip.place"));
        _placeButton.Click += (_, _) => ChooseMode(ToolMode.Place);
        _buildButton = Make(Lang.T("rail.label.build"), "U", "build");
        ToolTip.SetTip(_buildButton, Lang.T("rail.tip.build"));
        _buildButton.Click += (_, _) => ChooseBuild();
        Cap(Lang.T("rail.cap.create"));
        _landformButton = Make(Lang.T("rail.label.landform"), "G", "mountain");
        ToolTip.SetTip(_landformButton, Lang.T("rail.tip.landform"));
        _landformButton.Click += (_, _) => ChooseMode(ToolMode.Landform);
        _dungeonButton = Make(Lang.T("rail.label.dungeon"), "", "dungeon");
        _dungeonButton.Tip("tools.dungeon");
        _dungeonButton.Click += (_, _) => ChooseMode(ToolMode.Dungeon);
        _scriptButton = Make(Lang.T("rail.label.script"), "", "script");
        ToolTip.SetTip(_scriptButton, Lang.T("rail.tip.script"));
        _scriptButton.Click += (_, _) => ChooseMode(ToolMode.Script);
        BlueprintsButton = Make(Lang.T("rail.label.blueprints"), "", "paste");
        BlueprintsButton.Tip("tools.blueprints");
        BlueprintsButton.Click += (_, _) => ChooseMode(ToolMode.Blueprints);
        Cap(Lang.T("rail.cap.tools"));
        SelectButton = Make(Lang.T("rail.label.select"), "E", "select");
        ToolTip.SetTip(SelectButton, Lang.T("rail.tip.select"));
        SelectButton.Click += (_, _) => ChooseSelect();
        _measureButton = Make(Lang.T("rail.label.measure"), "M", "measure");
        ToolTip.SetTip(_measureButton, Lang.T("rail.tip.measure"));
        _measureButton.Click += (_, _) => ChooseMode(ToolMode.Measure);
        _viewButton = Make(Lang.T("rail.label.view"), "Esc", "move");
        ToolTip.SetTip(_viewButton, Lang.T("rail.tip.view"));
        _viewButton.Click += (_, _) => Choose(null);
        // Mods' own tools (SetModTools), last; none until a world with them opens.
        rail.Children.Add(_modRail);
        // On a short window the rail scrolls with the wheel; no bar (it would squeeze the buttons).
        Rail = Ui.Card(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Content = new StackPanel { Width = 56, Children = { rail } } });

        // The options: size and strength, the brush shape, and each tool's own.
        var sizeV = new TextBlock();
        SizeSlider = Ui.Slide(1, 30, 0.5, Brush.Radius, sizeV, v => $"{v:0.#} m", v => Brush.Radius = (float)v);
        var strengthV = new TextBlock();
        StrengthSlider = Ui.Slide(0.05, 1, 0.05, Brush.Strength, strengthV, v => $"{v:0.00}", v => Brush.Strength = (float)v);
        ShapeBox = new ComboBox { SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12, MaxDropDownHeight = 400 };
        FillShapes();
        FalloffBox = new ComboBox { ItemsSource = new[] { Lang.T("brush.falloff.smooth"), Lang.T("brush.falloff.linear"), Lang.T("brush.falloff.dome"), Lang.T("brush.falloff.flatTop"), Lang.T("brush.falloff.peak"), Lang.T("brush.falloff.sharpEdge") }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
        var turnV = new TextBlock();
        var turn = Ui.Slide(-180, 180, 1, 0, turnV, v => $"{v:0}°", v => Brush.Turn = (float)v);
        _turnRow = Ui.Row(Lang.T("brush.label.turn"), turn.Tip("brush.turn"), turnV);
        ShapeBox.SelectionChanged += (_, _) => ShapeChosen();
        LoadStampButton.Tip("brush.loadStamp");
        ForgetStampButton.Tip("brush.forgetStamp");
        SizeSlider.Tip("brush.size");
        StrengthSlider.Tip("brush.strength");
        ShapeBox.Tip("brush.shape");
        FalloffBox.Tip("brush.falloff");
        LoadStampButton.Click += (_, _) => LoadStampAsked?.Invoke();
        ForgetStampButton.Click += (_, _) =>
        {
            int i = ShapeBox.SelectedIndex - 4;
            if (i < 0 || i >= StampList.Count || !ForgetStamp(StampList[i]))
            {
                Message?.Invoke(Said.Of("brush.msg.notLoaded"));
            }
        };
        FalloffBox.SelectionChanged += (_, _) => Brush.Falloff = (Falloff)Math.Max(0, FalloffBox.SelectedIndex);
        _turnRow.IsVisible = false;

        TargetFromClickBox = new CheckBox { Content = Lang.T("brush.label.targetFromClick"), IsChecked = Brush.TargetFromClick, FontSize = 12 };
        TargetBox = new NumericUpDown { Value = (decimal)Brush.Target, Increment = 0.1m, FormatString = "0.0", FontSize = 12, IsEnabled = !Brush.TargetFromClick }.Tip("brush.target");
        TargetFromClickBox.Tip("brush.targetFromClick");
        TargetFromClickBox.IsCheckedChanged += (_, _) => { Brush.TargetFromClick = TargetFromClickBox.IsChecked == true; TargetBox.IsEnabled = !Brush.TargetFromClick; };
        TargetBox.ValueChanged += (_, e) => Brush.Target = (float)(e.NewValue ?? 0);
        _flattenRows = new StackPanel { Spacing = 4, Children = { TargetFromClickBox, Ui.Row(Lang.T("brush.label.heightMetres"), TargetBox) } };

        _naturalRows = BrushRows.Natural(Brush, NewPatternButton);
        NewPatternButton.Click += (_, _) => NewPattern();
        NewPatternButton.Tip("natural.seed");

        var restRow = BrushRows.Rest(Brush, out var restSlider);
        RestSlider = restSlider;
        void Mode(bool water)
        {
            Brush.ErodeWater = water;
            ThermalButton.Classes.Set("on", !water);
            WaterButton.Classes.Set("on", water);
            restRow.IsVisible = !water;
        }
        ThermalButton.Click += (_, _) => Mode(false);
        WaterButton.Click += (_, _) => Mode(true);
        ThermalButton.Tip("erode.thermal");
        WaterButton.Tip("erode.water");
        _erodeRows = new StackPanel { Spacing = 4, Children = { new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { ThermalButton, WaterButton } }, restRow } };
        Mode(false);
        // The Paint brush's paints: a swatch each, two to a row, with its key.
        var paints = new UniformGrid { Columns = 2, ColumnSpacing = 4, RowSpacing = 4 };
        foreach (var paint in Paints)
        {
            var swatch = Swatch(14);
            swatch.Background = PaintColour(paint);
            swatch.VerticalAlignment = VerticalAlignment.Center;
            var name = new TextBlock { Text = Brush.Label(paint), FontSize = 12, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            var key = new TextBlock { Text = KeyOf(paint), FontSize = 9, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("monospace") };
            Grid.SetColumn(name, 1);
            Grid.SetColumn(key, 2);
            var b = new Button
            {
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = { swatch, name, key } },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(6, 4),
            };
            ToolTip.SetTip(b, Lang.T("rail.brushTip", Brush.Label(paint), KeyOf(paint), Brush.Help(paint)));
            b.Click += (_, _) => Choose(paint);
            PaintButtons[paint] = b;
            paints.Children.Add(b);
        }
        _paintRows = paints;
        Options = Ui.Card(new StackPanel
        {
            Width = 280,
            Spacing = 6,
            Children =
            {
                Ui.Titled(_title, () => HelpTopics.Tool(ToolMode.Brush, Tool)), _help,
                _paintRows,
                Ui.Row(Lang.T("brush.label.size"), SizeSlider, sizeV),
                Ui.Row(Lang.T("brush.label.strength"), StrengthSlider, strengthV),
                Ui.Row(Lang.T("brush.label.shape"), ShapeBox),
                (_falloffRow = Ui.Row(Lang.T("brush.label.falloff"), FalloffBox)),
                _turnRow,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { LoadStampButton, ForgetStampButton } },
                _flattenRows,
                _naturalRows,
                _erodeRows,
            },
        });
        Choose(null);
    }
}
