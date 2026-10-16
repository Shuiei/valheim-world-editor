using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Which tool is in use: View (look around, click picks), Select, Measure, Shape, Path, Area, Paste (from the
/// Area tool, Ctrl+V), Place, Blueprints (the library, to paste one), or a brush (Tool says which).
/// </summary>
public enum ToolMode
{
    /// <summary>Look around; a click picks an object.</summary>
    View,
    /// <summary>The Select tool.</summary>
    Select,
    /// <summary>The Measure tool.</summary>
    Measure,
    /// <summary>The Shape tool.</summary>
    Shape,
    /// <summary>The Path tool.</summary>
    Path,
    /// <summary>The Area tool.</summary>
    Area,
    /// <summary>Pasting what the Area tool copied (Ctrl+V).</summary>
    Paste,
    /// <summary>The Place tool.</summary>
    Place,
    /// <summary>A sculpt or paint brush (Tool says which).</summary>
    Brush,
    /// <summary>The Mountain tool.</summary>
    Mountain,
    /// <summary>The Script tool.</summary>
    Script,
    /// <summary>The Blueprints library, to paste one.</summary>
    Blueprints,
    /// <summary>The Dungeon tool.</summary>
    Dungeon,
}

/// <summary>
/// The tool rail (left edge) and the chosen tool's options next to it, like the web editor's: View
/// (look around and pick objects), Select (move, turn and delete objects: see SelectPanel), the sculpt
/// brushes and the paint brushes. Keys as in the web editor: 1-9 and 0 pick the brushes, E selects,
/// Esc goes back to View.
/// </summary>
public sealed class ToolPanel
{
    /// <summary>The brush shapes before the stamps, in BrushShape's order.</summary>
    private static readonly string[] BuiltInShapes = ["Circle", "Square", "Ring", "Ragged (noise)"];

    /// <summary>The brush settings the options change (size, strength, shape, falloff, each tool's own).</summary>
    public Brush Brush { get; } = new();
    /// <summary>The brush in use; null when the mode is not Brush.</summary>
    public BrushTool? Tool { get; private set; }
    /// <summary>The tool in use (Brush when <c>Tool</c> is set).</summary>
    public ToolMode Mode { get; private set; }
    /// <summary>The Select tool is in use.</summary>
    public bool SelectMode => Mode == ToolMode.Select;
    /// <summary>The tool changed (Mode and Tool say which).</summary>
    public event Action<BrushTool?>? ToolChanged;

    /// <summary>The tool rail card, on the window's left edge.</summary>
    public Control Rail { get; }
    /// <summary>The brush options card, shown only while a brush is in use.</summary>
    public Control Options { get; }

    /// <summary>The rail button of each brush.</summary>
    private readonly Dictionary<BrushTool, Button> _buttons = [];

    /// <summary>The rail buttons of the other tools (Blueprints, the library, has its own property).</summary>
    private readonly Button _viewButton, _measureButton, _shapeButton, _pathButton, _areaButton, _placeButton, _mountainButton, _caveButton, _scriptButton, _dungeonButton;
    /// <summary>The Cave button: the Path tool with its Cave action (the window picks the action).</summary>
    public event Action? CaveChosen;

    /// <summary>The Path tool lit as Cave while its action is Cave (the window says so when the action changes).</summary>
    public void MarkCave(bool on)
    {
        _caveButton.Classes.Set("on", on && Mode == ToolMode.Path);
        _pathButton.Classes.Set("on", !on && Mode == ToolMode.Path);
    }
    /// <summary>The Select tool's rail button.</summary>
    internal Button SelectButton { get; }
    /// <summary>Blueprints: the library, to paste one (in a world), in place of a tool's options.</summary>
    internal Button BlueprintsButton { get; }
    /// <summary>The options card's title: the brush's name.</summary>
    private readonly TextBlock _title = new() { FontSize = 14, FontWeight = FontWeight.SemiBold };
    /// <summary>The options card's help line for the brush.</summary>
    private readonly TextBlock _help = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>The option rows shown only for some brushes or shapes (Flatten, Naturalize, Turn, Erode, Falloff, stamps).</summary>
    private readonly Control _flattenRows, _naturalRows, _turnRow, _erodeRows, _falloffRow, _stampOnceRows, _stampHeightRow;
    /// <summary>The stamps in the Shape list after the four shapes: the built-in ones, then loaded pictures.</summary>
    internal List<Stamps.Stamp> StampList { get; } = [.. Stamps.BuiltIn, .. Stamps.LoadKept()];
    /// <summary>Load stamp…: asks the window for a picture (<c>LoadStampAsked</c>).</summary>
    internal Button LoadStampButton { get; } = new() { Content = "Load stamp…", FontSize = 12 };
    /// <summary>Forget stamp: drops the loaded picture chosen as Shape (built-in stamps stay).</summary>
    internal Button ForgetStampButton { get; } = new() { Content = "Forget stamp", FontSize = 12 };
    /// <summary>Stamp once: a click of Raise or Lower puts the whole stamp in.</summary>
    internal CheckBox StampOnceBox { get; } = new() { Content = "Stamp once: a click puts the whole stamp in", FontSize = 12 };
    /// <summary>How high, in metres, Stamp once puts the stamp in.</summary>
    internal NumericUpDown StampHeightBox { get; } = new() { Value = 4, Increment = 0.5m, FormatString = "0.0#", FontSize = 12 };
    /// <summary>Load stamp…: the window picks the picture (and calls <c>AddStamp</c>).</summary>
    public event Action? LoadStampAsked;
    /// <summary>A line for the window's message bar.</summary>
    public event Action<string>? Message;
    /// <summary>No limit: the ground tools go past the game's ±8 m (Ground.NoLimit); off at every start.</summary>
    internal CheckBox NoLimitBox { get; } = new() { Content = "No limit: past the game's ±8 m", FontSize = 12 };
    /// <summary>No limit was switched (on: true).</summary>
    public event Action<bool>? NoLimitChanged;
    /// <summary>Erode's Thermal mode: slopes steeper than the rest angle slide down.</summary>
    internal Button ThermalButton { get; } = new() { Content = "Thermal", FontSize = 12 };
    /// <summary>Erode's Water mode (the rest angle does not apply).</summary>
    internal Button WaterButton { get; } = new() { Content = "Water", FontSize = 12 };
    /// <summary>Thermal erosion's rest angle, in degrees (10..60).</summary>
    internal Slider RestSlider { get; }

    /// <summary>The brush radius in metres (1..30). For tests too.</summary>
    internal Slider SizeSlider { get; }
    /// <summary>Naturalize: a new noise pattern for the next stroke (and natural paths).</summary>
    internal Button NewPatternButton { get; } = new() { Content = "New pattern", FontSize = 12 };
    /// <summary>The brush strength (0.05..1).</summary>
    internal Slider StrengthSlider { get; }
    /// <summary>The brush shape: the four shapes, then the stamps.</summary>
    internal ComboBox ShapeBox { get; }
    /// <summary>The brush falloff, in Falloff's order.</summary>
    internal ComboBox FalloffBox { get; }
    /// <summary>Flatten: level to the height where the stroke starts, not the typed height.</summary>
    internal CheckBox TargetFromClickBox { get; }
    /// <summary>Flatten's height in metres (off while it comes from the stroke's start).</summary>
    internal NumericUpDown TargetBox { get; }
    /// <summary>A brush's rail button (tests).</summary>
    internal Button ButtonOf(BrushTool t) => _buttons[t];

    /// <summary>The keys of the web editor's rail.</summary>
    public static readonly (BrushTool Tool, string Key)[] Keys =
    [
        (BrushTool.Raise, "1"), (BrushTool.Lower, "2"), (BrushTool.Flatten, "3"), (BrushTool.Smooth, "4"), (BrushTool.Natural, "0"), (BrushTool.Erode, "O"), (BrushTool.Restore, "5"),
        (BrushTool.PaintDirt, "6"), (BrushTool.PaintCultivated, "7"), (BrushTool.PaintPaved, "8"), (BrushTool.PaintClear, "9"),
    ];

    /// <summary>A paint brush (the rail's Paint section), not a sculpt one.</summary>
    private static bool IsPaint(BrushTool t) => t is BrushTool.PaintDirt or BrushTool.PaintCultivated or BrushTool.PaintPaved or BrushTool.PaintClear;

    /// <summary>The rail's short names (the web editor's).</summary>
    private static string RailLabel(BrushTool t) => t switch
    {
        BrushTool.Natural => Lang.T("rail.label.natural"),
        BrushTool.PaintCultivated => Lang.T("rail.label.cultivated"),
        BrushTool.PaintClear => Lang.T("rail.label.clear"),
        _ => Brush.Label(t),
    };

    /// <summary>A brush's rail icon (sculpt brushes) or paint colour (paint brushes), the web editor's.</summary>
    private static (string? Icon, IBrush? Swatch) Look(BrushTool t) => t switch
    {
        BrushTool.Raise => ("raise", null),
        BrushTool.Lower => ("lower", null),
        BrushTool.Flatten => ("flatten", null),
        BrushTool.Smooth => ("smooth", null),
        BrushTool.Natural => ("natural", null),
        BrushTool.Erode => ("erode", null),
        BrushTool.Restore => ("restore", null),
        BrushTool.PaintDirt => (null, new SolidColorBrush(Color.Parse("#8a6a46"))),
        BrushTool.PaintCultivated => (null, new SolidColorBrush(Color.Parse("#4e3622"))),
        BrushTool.PaintPaved => (null, new SolidColorBrush(Color.Parse("#8d8d88"))),
        _ => (null, new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#5e8a3a"), 0.5), new GradientStop(Color.Parse("#7a6a4a"), 0.5) },
        }),
    };

    /// <summary>An option row: label, control, and the value shown after it when there is one.</summary>
    private static Grid Row(string label, Control input, TextBlock? value = null)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*,46") };
        var l = new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        Tips.Label(l, input);
        g.Children.Add(l);
        Grid.SetColumn(input, 1);
        g.Children.Add(input);
        if (value != null)
        {
            Grid.SetColumn(value, 2);
            value.VerticalAlignment = VerticalAlignment.Center;
            value.FontSize = 12;
            value.Margin = new Thickness(6, 0, 0, 0);
            g.Children.Add(value);
        }
        return g;
    }

    /// <summary>A snapping slider whose value is shown (formatted) in <paramref name="shown"/> and passed to <paramref name="set"/>.</summary>
    private static Slider Slide(double min, double max, double step, double value, TextBlock shown, Func<double, string> format, Action<double> set)
    {
        var s = new Slider { Minimum = min, Maximum = max, SmallChange = step, TickFrequency = step, IsSnapToTickEnabled = true, Value = value };
        shown.Text = format(value);
        s.ValueChanged += (_, e) => { shown.Text = format(e.NewValue); set(e.NewValue); };
        return s;
    }

    /// <summary>Builds the rail and the options, with View in use.</summary>
    public ToolPanel()
    {
        var rail = _rail = new StackPanel { Spacing = 0 };
        // A rail button: the web editor's icon (or the paint's colour), the name and the key.
        Button Make(string label, string key, string? icon, IBrush? swatch = null)
        {
            Control picture = icon != null ? Icons.Make(icon, 16) : new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(5), Background = swatch, BorderBrush = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)), BorderThickness = new Thickness(1) };
            picture.HorizontalAlignment = HorizontalAlignment.Center;
            var b = new Button
            {
                Content = new Grid
                {
                    Children =
                    {
                        new StackPanel { Spacing = 1, Children = { picture, new TextBlock { Text = label, FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center } } },
                        new TextBlock { Text = key, FontSize = 9, Opacity = 0.6, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -2, -1, 0), FontFamily = new FontFamily("monospace") },
                    },
                },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(2, 3, 3, 2),
            }.Classed("rail");
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
        // On a short window the rail scrolls with the wheel; no bar (it would squeeze the buttons).
        Rail = Ui.Card(new ScrollViewer { VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden, Content = new StackPanel { Width = 56, Children = { rail } } });

        // The options: size and strength, the brush shape, and each tool's own.
        var sizeV = new TextBlock();
        SizeSlider = Slide(1, 30, 0.5, Brush.Radius, sizeV, v => $"{v:0.#} m", v => Brush.Radius = (float)v);
        var strengthV = new TextBlock();
        StrengthSlider = Slide(0.05, 1, 0.05, Brush.Strength, strengthV, v => $"{v:0.00}", v => Brush.Strength = (float)v);
        ShapeBox = new ComboBox { SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12, MaxDropDownHeight = 400 };
        FillShapes();
        FalloffBox = new ComboBox { ItemsSource = new[] { "Smooth", "Linear", "Dome", "Flat top", "Peak", "Sharp edge (pickaxe)" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
        var turnV = new TextBlock();
        var turn = Slide(-180, 180, 1, 0, turnV, v => $"{v:0}°", v => Brush.Turn = (float)v);
        _turnRow = Row("Turn", turn.Tip("brush.turn"), turnV);
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
                Message?.Invoke("Choose a loaded picture as Shape first (built-in stamps stay).");
                return;
            }
            StampList.RemoveAt(i);
            Stamps.SaveKept(StampList.Where(s => s.Loaded));
            FillShapes();
            ShapeBox.SelectedIndex = 0;
        };
        StampOnceBox.IsCheckedChanged += (_, _) => { Brush.StampOnce = StampOnceBox.IsChecked == true; SyncStamp(); };
        StampHeightBox.ValueChanged += (_, e) => Brush.StampHeight = (float)(e.NewValue ?? 0);
        _stampHeightRow = Row("Height", StampHeightBox, new TextBlock { Text = " m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        _stampOnceRows = new StackPanel { Spacing = 4, Children = { StampOnceBox, _stampHeightRow } };
        FalloffBox.SelectionChanged += (_, _) => Brush.Falloff = (Falloff)Math.Max(0, FalloffBox.SelectedIndex);
        _turnRow.IsVisible = false;

        TargetFromClickBox = new CheckBox { Content = "Level to the height where the stroke starts", IsChecked = Brush.TargetFromClick, FontSize = 12 };
        TargetBox = new NumericUpDown { Value = (decimal)Brush.Target, Increment = 0.1m, FormatString = "0.0", FontSize = 12, IsEnabled = !Brush.TargetFromClick }.Tip("brush.target");
        TargetFromClickBox.Tip("brush.targetFromClick");
        TargetFromClickBox.IsCheckedChanged += (_, _) => { Brush.TargetFromClick = TargetFromClickBox.IsChecked == true; TargetBox.IsEnabled = !Brush.TargetFromClick; };
        TargetBox.ValueChanged += (_, e) => Brush.Target = (float)(e.NewValue ?? 0);
        _flattenRows = new StackPanel { Spacing = 4, Children = { TargetFromClickBox, Row("Height (m)", TargetBox) } };

        var ampV = new TextBlock();
        var noiseV = new TextBlock();
        _naturalRows = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Row("Bumps", Slide(0.2, 4, 0.1, Brush.NoiseAmp, ampV, v => $"{v:0.0} m", v => Brush.NoiseAmp = (float)v).Tip("natural.amp"), ampV),
                Row("Bump size", Slide(4, 60, 1, Brush.NoiseSize, noiseV, v => $"{v:0} m", v => Brush.NoiseSize = (float)v).Tip("natural.size"), noiseV),
                NewPatternButton,
            },
        };
        NewPatternButton.Click += (_, _) => NewPattern();
        NewPatternButton.Tip("natural.seed");

        var restV = new TextBlock();
        RestSlider = Slide(10, 60, 1, Brush.RestAngle, restV, v => $"{v:0}°", v => Brush.RestAngle = (float)v).Tip("erode.rest");
        var restRow = Row("Rest angle", RestSlider, restV);
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
                _title, _help,
                Row("Size", SizeSlider, sizeV),
                Row("Strength", StrengthSlider, strengthV),
                Row("Shape", ShapeBox),
                (_falloffRow = Row("Falloff", FalloffBox)),
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

    /// <summary>Picks the Select tool.</summary>
    public void ChooseSelect() => ChooseMode(ToolMode.Select);

    /// <summary>Picks a tool other than a brush.</summary>
    public void ChooseMode(ToolMode mode) => Choose(null, mode);

    /// <summary>The rail's buttons and captions.</summary>
    private readonly StackPanel _rail;
    /// <summary>
    /// The Workshop: only Build (the Place tool, for building pieces), Select and View on the rail; the
    /// other tools (and their keys) are for worlds.
    /// </summary>
    public bool Workshop { get; private set; }

    /// <summary>Switches the rail to the Workshop's (Place shown as Build) or back; switching on, another tool in use goes back to View.</summary>
    public void SetWorkshop(bool on)
    {
        Workshop = on;
        foreach (var c in _rail.Children)
        {
            c.IsVisible = !on || c == _placeButton || c == SelectButton || c == _viewButton;
        }
        var label = ((_placeButton.Content as Grid)!.Children[0] as StackPanel)!.Children[1] as TextBlock;
        label!.Text = on ? "Build" : "Place";
        ToolTip.SetTip(_placeButton, Lang.T(on ? "rail.tip.build" : "rail.tip.place"));
        if (on && (Tool != null || Mode is not (ToolMode.View or ToolMode.Place or ToolMode.Select)))
        {
            Choose(null);
        }
    }

    /// <summary>A brush, or (null) the mode's tool. In the Workshop only View, Place and Select can be picked.</summary>
    public void Choose(BrushTool? t, ToolMode mode = ToolMode.View)
    {
        if (Workshop && (t != null || mode is not (ToolMode.View or ToolMode.Place or ToolMode.Select)))
        {
            return;
        }
        Tool = t;
        Mode = t != null ? ToolMode.Brush : mode;
        _viewButton.Classes.Set("on", Mode == ToolMode.View);
        SelectButton.Classes.Set("on", Mode == ToolMode.Select);
        _measureButton.Classes.Set("on", Mode == ToolMode.Measure);
        _shapeButton.Classes.Set("on", Mode == ToolMode.Shape);
        _pathButton.Classes.Set("on", Mode == ToolMode.Path);
        _areaButton.Classes.Set("on", Mode is ToolMode.Area or ToolMode.Paste);
        _placeButton.Classes.Set("on", Mode == ToolMode.Place);
        _mountainButton.Classes.Set("on", Mode == ToolMode.Mountain);
        _scriptButton.Classes.Set("on", Mode == ToolMode.Script);
        _dungeonButton.Classes.Set("on", Mode == ToolMode.Dungeon);
        BlueprintsButton.Classes.Set("on", Mode == ToolMode.Blueprints);
        _caveButton.Classes.Set("on", false);
        foreach (var (k, b) in _buttons)
        {
            b.Classes.Set("on", k == t);
        }
        Options.IsVisible = t != null;
        if (t is BrushTool tool)
        {
            _title.Text = Brush.Label(tool);
            _help.Text = Lang.T("brush.helpHold", Brush.Help(tool));
            _flattenRows.IsVisible = tool == BrushTool.Flatten;
            _naturalRows.IsVisible = tool == BrushTool.Natural;
            _erodeRows.IsVisible = tool == BrushTool.Erode;
            SyncStamp();
        }
        ToolChanged?.Invoke(t);
    }

    /// <summary>A new pattern of natural bumps (the web editor's New pattern): another seed for the noise.</summary>
    /// <param name="seed">The noise's seed; null for a random one.</param>
    internal void NewPattern(int? seed = null)
    {
        Brush.Noise = new Noise(seed ?? Random.Shared.Next());
        Message?.Invoke("New natural pattern. The next Naturalize stroke or natural path uses it.");
    }

    /// <summary>[ and ]: the brush one metre smaller or bigger (within the Size slider's range).</summary>
    internal void ResizeBrush(int metres) => SizeSlider.Value = Math.Clamp(SizeSlider.Value + metres, SizeSlider.Minimum, SizeSlider.Maximum);

    /// <summary>The rail's keys (Avalonia key names, digits as "0".."9"); true when the key was one.</summary>
    public bool Key(string key)
    {
        if (key is "Escape" or "H")
        {
            Choose(null);
            return true;
        }
        if (key == "E")
        {
            ChooseSelect();
            return true;
        }
        if (key == "T")
        {
            ChooseMode(ToolMode.Place);
            return true;
        }
        if (key == "B")
        {
            ChooseMode(ToolMode.Area);
            return true;
        }
        if (key == "P")
        {
            ChooseMode(ToolMode.Path);
            return true;
        }
        if (key == "G")
        {
            ChooseMode(ToolMode.Shape);
            return true;
        }
        if (key == "M")
        {
            ChooseMode(ToolMode.Measure);
            return true;
        }
        foreach (var (t, k) in Keys)
        {
            if (k == key)
            {
                Choose(t);
                return true;
            }
        }
        return false;
    }

    /// <summary>Fills the Shape list: the built-in shapes, then every stamp.</summary>
    private void FillShapes() => ShapeBox.ItemsSource = BuiltInShapes.Concat(StampList.Select(s => $"Stamp: {s.Label}")).ToList();

    /// <summary>The Shape list's choice into the brush (a stamp's weights and name too).</summary>
    private void ShapeChosen()
    {
        int i = Math.Max(0, ShapeBox.SelectedIndex);
        if (i >= 4 && i - 4 < StampList.Count)
        {
            Brush.Shape = BrushShape.Stamp;
            Brush.StampData = StampList[i - 4].Data;
            Brush.StampLabel = StampList[i - 4].Label;
        }
        else
        {
            Brush.Shape = (BrushShape)Math.Min(i, 3);
        }
        SyncStamp();
    }

    /// <summary>
    /// Shows the rows that fit the shape. A stamp: no falloff (the picture has its own), it turns, and Raise and
    /// Lower can stamp it once.
    /// </summary>
    private void SyncStamp()
    {
        bool stamp = Brush.Shape == BrushShape.Stamp;
        _turnRow.IsVisible = Brush.Shape is BrushShape.Square or BrushShape.Stamp;
        _falloffRow.IsVisible = !stamp;
        int i = ShapeBox.SelectedIndex - 4;
        ForgetStampButton.IsVisible = i >= 0 && i < StampList.Count && StampList[i].Loaded;
        _stampOnceRows.IsVisible = stamp && Tool is BrushTool.Raise or BrushTool.Lower;
        _stampHeightRow.IsVisible = Brush.StampOnce;
    }

    /// <summary>A picture loaded as a stamp: chosen as the Shape, and kept for next time.</summary>
    /// <param name="label">Its name in the Shape list.</param>
    /// <param name="data">Its weights (see Stamps.FromPicture).</param>
    public void AddStamp(string label, float[] data)
    {
        var st = new Stamps.Stamp($"pic:{DateTime.Now.Ticks}", label, data, Loaded: true);
        StampList.Add(st);
        Stamps.SaveKept(StampList.Where(s => s.Loaded));
        FillShapes();
        ShapeBox.SelectedIndex = 4 + StampList.Count - 1;
        Message?.Invoke($"Loaded the stamp “{label}”: white parts work fully, black parts not at all.");
    }

    /// <summary>The flatten height follows the ground where a stroke started (shown rounded to 0.1 m).</summary>
    public void ShowTarget(float h) => TargetBox.Value = (decimal)MathF.Round(h, 1);

    /// <summary>Alt + click with a brush: level to that height (Flatten, not from the stroke's start).</summary>
    public void FlattenTo(float h)
    {
        TargetFromClickBox.IsChecked = false;
        Brush.Target = MathF.Round(h, 1);
        ShowTarget(h);
        Choose(BrushTool.Flatten);
    }
}
