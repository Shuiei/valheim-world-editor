using Avalonia;
using Avalonia.Controls;
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
        // A rail button: the web editor's icon (or the paint's colour), the name and the key.
        Button Make(string label, string key, string? icon, IBrush? swatch = null)
        {
            Control picture = icon != null ? Icons.Make(icon, 16) : new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(5), Background = swatch, BorderBrush = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)), BorderThickness = new Thickness(1) };
            var b = RailButton(label, key, picture);
            rail.Children.Add(b);
            return b;
        }
        void Cap(string text) => rail.Children.Add(new TextBlock { Text = text.ToUpperInvariant(), FontSize = 9, Foreground = Ui.Muted, LetterSpacing = 0.7, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, rail.Children.Count == 0 ? 0 : 5, 0, 1) });
        // A brush's rail button, with its key.
        void AddBrush(BrushTool t, string key)
        {
            var (icon, swatch) = Look(t);
            var b = Make(RailLabel(t), key, icon, swatch);
            ToolTip.SetTip(b, Lang.T("rail.brushTip", Brush.Label(t), key, Brush.Help(t)));
            b.Click += (_, _) => Choose(t);
            _buttons[t] = b;
        }
        // Sculpt, then the tools that draw on the world (Area, Path, Place), Paint, the tools that make things, and
        // last the tools that only look or pick.
        Cap(Lang.T("rail.cap.sculpt"));
        foreach (var (t, key) in Keys.Where(k => !IsPaint(k.Tool)))
        {
            AddBrush(t, key);
        }
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
        Cap(Lang.T("rail.cap.paint"));
        foreach (var (t, key) in Keys.Where(k => IsPaint(k.Tool)))
        {
            AddBrush(t, key);
        }
        Cap(Lang.T("rail.cap.create"));
        _shapeButton = Make(Lang.T("rail.label.shape"), "G", "shape");
        ToolTip.SetTip(_shapeButton, Lang.T("rail.tip.shape"));
        _shapeButton.Click += (_, _) => ChooseMode(ToolMode.Shape);
        _mountainButton = Make(Lang.T("rail.label.mountain"), "", "mountain");
        ToolTip.SetTip(_mountainButton, Lang.T("rail.tip.mountain"));
        _mountainButton.Click += (_, _) => ChooseMode(ToolMode.Mountain);
        _caveButton = Make(Lang.T("rail.label.cave"), "", "cave");
        ToolTip.SetTip(_caveButton, Lang.T("rail.tip.cave"));
        _caveButton.Click += (_, _) =>
        {
            ChooseMode(ToolMode.Path);
            CaveChosen?.Invoke();
        };
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
        Rail = Ui.Card(new ScrollViewer { VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden, Content = new StackPanel { Width = 56, Children = { rail } } });

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
        StampOnceBox.Tip("brush.stampOnce");
        StampHeightBox.Tip("brush.stampHeight");
        SizeSlider.Tip("brush.size");
        StrengthSlider.Tip("brush.strength");
        ShapeBox.Tip("brush.shape");
        FalloffBox.Tip("brush.falloff");
        LoadStampButton.Click += (_, _) => LoadStampAsked?.Invoke();
        ForgetStampButton.Click += (_, _) =>
        {
            int i = ShapeBox.SelectedIndex - 4;
            if (i < 0 || i >= StampList.Count || !StampList[i].Loaded)
            {
                Message?.Invoke(Said.Of("brush.msg.notLoaded"));
                return;
            }
            StampList.RemoveAt(i);
            Stamps.SaveKept(StampList.Where(s => s.Loaded));
            FillShapes();
            ShapeBox.SelectedIndex = 0;
        };
        StampOnceBox.IsCheckedChanged += (_, _) => { Brush.StampOnce = StampOnceBox.IsChecked == true; SyncStamp(); };
        StampHeightBox.ValueChanged += (_, e) => Brush.StampHeight = (float)(e.NewValue ?? 0);
        _stampHeightRow = Ui.Row(Lang.T("common.height"), StampHeightBox, new TextBlock { Text = Lang.T("common.metres"), Margin = new Thickness(3, 0, 0, 0), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        _stampOnceRows = new StackPanel { Spacing = 4, Children = { StampOnceBox, _stampHeightRow } };
        FalloffBox.SelectionChanged += (_, _) => Brush.Falloff = (Falloff)Math.Max(0, FalloffBox.SelectedIndex);
        _turnRow.IsVisible = false;

        TargetFromClickBox = new CheckBox { Content = Lang.T("brush.label.targetFromClick"), IsChecked = Brush.TargetFromClick, FontSize = 12 };
        TargetBox = new NumericUpDown { Value = (decimal)Brush.Target, Increment = 0.1m, FormatString = "0.0", FontSize = 12, IsEnabled = !Brush.TargetFromClick }.Tip("brush.target");
        TargetFromClickBox.Tip("brush.targetFromClick");
        TargetFromClickBox.IsCheckedChanged += (_, _) => { Brush.TargetFromClick = TargetFromClickBox.IsChecked == true; TargetBox.IsEnabled = !Brush.TargetFromClick; };
        TargetBox.ValueChanged += (_, e) => Brush.Target = (float)(e.NewValue ?? 0);
        _flattenRows = new StackPanel { Spacing = 4, Children = { TargetFromClickBox, Ui.Row(Lang.T("brush.label.heightMetres"), TargetBox) } };

        var ampV = new TextBlock();
        var noiseV = new TextBlock();
        _naturalRows = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Ui.Row(Lang.T("brush.label.bumps"), Ui.Slide(0.2, 4, 0.1, Brush.NoiseAmp, ampV, v => $"{v:0.0} m", v => Brush.NoiseAmp = (float)v).Tip("natural.amp"), ampV),
                Ui.Row(Lang.T("brush.label.bumpSize"), Ui.Slide(4, 60, 1, Brush.NoiseSize, noiseV, v => $"{v:0} m", v => Brush.NoiseSize = (float)v).Tip("natural.size"), noiseV),
                NewPatternButton,
            },
        };
        NewPatternButton.Click += (_, _) => NewPattern();
        NewPatternButton.Tip("natural.seed");

        var restV = new TextBlock();
        RestSlider = Ui.Slide(10, 60, 1, Brush.RestAngle, restV, v => $"{v:0}°", v => Brush.RestAngle = (float)v).Tip("erode.rest");
        var restRow = Ui.Row(Lang.T("brush.label.restAngle"), RestSlider, restV);
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
        NoLimitBox.Tip("brush.noLimit");
        NoLimitBox.IsCheckedChanged += (_, _) => NoLimitChanged?.Invoke(NoLimitBox.IsChecked == true);
        Options = Ui.Card(new StackPanel
        {
            Width = 280,
            Spacing = 6,
            Children =
            {
                Ui.Titled(_title, () => HelpTopics.Tool(ToolMode.Brush, Tool)), _help,
                Ui.Row(Lang.T("brush.label.size"), SizeSlider, sizeV),
                Ui.Row(Lang.T("brush.label.strength"), StrengthSlider, strengthV),
                Ui.Row(Lang.T("brush.label.shape"), ShapeBox),
                (_falloffRow = Ui.Row(Lang.T("brush.label.falloff"), FalloffBox)),
                _turnRow,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { LoadStampButton, ForgetStampButton } },
                _stampOnceRows,
                _flattenRows,
                _naturalRows,
                _erodeRows,
                NoLimitBox,
            },
        });
        Choose(null);
    }
}
