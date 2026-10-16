using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Path tool's panel, like the web editor's: the action, width and soft edge, each action's own values, curve and
/// natural look, Apply and Clear, and how long the line is.
/// </summary>
public sealed class PathPanel
{
    /// <summary>The panel itself, shown over the view while the Path tool is on.</summary>
    public Control Card { get; }
    /// <summary>The view's Path tool the panel drives.</summary>
    private readonly PathTool _path;
    /// <summary>The view, for the ground height at the line's ends and to redraw the line.</summary>
    private readonly GlView _view;
    /// <summary>
    /// Set while Refresh fills the boxes, so filling the ramp boxes does not count as typing them in.
    /// </summary>
    private bool _filling;

    /// <summary>The action list, in <c>PathTool.Action</c> order.</summary>
    internal ComboBox ActionBox { get; }
    /// <summary>The path width, 1 to 40 m.</summary>
    internal Slider WidthSlider { get; }
    /// <summary>The soft edge, 0 to 15 m.</summary>
    internal Slider SoftSlider { get; }
    /// <summary>Flatten: the height to flatten to, in metres.</summary>
    internal NumericUpDown HeightBox { get; } = Num(32, 0.1m);
    /// <summary>Raise and Lower: by how much, in metres.</summary>
    internal NumericUpDown AmountBox { get; } = Num(2, 0.1m);
    /// <summary>Ramp: the height at the line's start; typing in it stops it following the ground.</summary>
    internal NumericUpDown StartBox { get; } = Num(32, 0.1m);
    /// <summary>Ramp: the height at the line's end; typing in it stops it following the ground.</summary>
    internal NumericUpDown EndBox { get; } = Num(32, 0.1m);
    /// <summary>River: how far below the water level the bed goes, at least 0.25 m.</summary>
    internal NumericUpDown DepthBox { get; } = Num(2, 0.25m);
    /// <summary>Cave: the preset; choosing one fills width, soft edge, depth and headroom from it.</summary>
    internal ComboBox CavePresetBox { get; }
    /// <summary>Cave: the preset's values each varied by about 15%, and new boulders.</summary>
    internal Button CaveRandomButton { get; } = new() { Content = "Randomize", FontSize = 12 };
    /// <summary>Cave: depth below the ground, 2 to 20 m.</summary>
    internal NumericUpDown CaveDepthBox { get; } = Num(7.5m, 0.5m);
    /// <summary>Cave: headroom inside, 2 to 15 m.</summary>
    internal NumericUpDown HeadroomBox { get; } = Num(4.5m, 0.5m);
    /// <summary>Cave: the roof's boulders, in <c>PathTool.CaveRock</c> order.</summary>
    internal ComboBox RockBox { get; }
    /// <summary>Cave: the preset's description (and a note after Randomize).</summary>
    internal TextBlock CaveHelp { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>For Randomize: the varied values and the new boulder seed.</summary>
    private readonly Random _random = new();
    /// <summary>Smooth curve through the points, or straight stretches.</summary>
    internal CheckBox CurveBox { get; } = new() { Content = "Smooth curve through the points", IsChecked = true, FontSize = 12 };
    /// <summary>Natural look; shows the bump rows when on.</summary>
    internal CheckBox NaturalBox { get; } = new() { Content = "Natural look (ragged edges, bumps)", FontSize = 12 };
    /// <summary>Asks for the action to be applied (raises <see cref="ApplyAsked"/>).</summary>
    internal Button ApplyButton { get; } = new Button { Content = "Apply (Enter)", FontSize = 12 }.Classed("primary");
    /// <summary>Removes the line.</summary>
    internal Button ClearButton { get; } = new() { Content = "Clear (Esc)", FontSize = 12 };
    /// <summary>How many points the line has and how long it is.</summary>
    internal TextBlock Info { get; } = new() { FontSize = 12, Foreground = Ui.Muted };
    /// <summary>The rows only some actions show, with the actions that show them.</summary>
    private readonly Dictionary<Control, PathTool.Action[]> _rows = [];
    /// <summary>The Natural look's bump rows (the brush's noise amplitude and size).</summary>
    private readonly Control _naturalRows;
    /// <summary>Apply was asked for (the window does it: it has the edit session).</summary>
    public event Action? ApplyAsked;
    /// <summary>The action was changed in the list.</summary>
    public event Action<PathTool.Action>? ActionChanged;

    /// <summary>A number box starting at <c>v</c> that steps by <c>step</c>.</summary>
    private static NumericUpDown Num(decimal v, decimal step) => new() { Value = v, Increment = step, FormatString = "0.0#", FontSize = 12 };

    /// <summary>A labelled row: the label, the input, and optionally something after it (a unit or a button).</summary>
    private static Grid Row(string label, Control input, Control? after = null)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*,46") };
        var l = new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        Tips.Label(l, input);
        g.Children.Add(l);
        Grid.SetColumn(input, 1);
        g.Children.Add(input);
        if (after != null)
        {
            Grid.SetColumn(after, 2);
            g.Children.Add(after);
        }
        return g;
    }

    /// <summary>
    /// A slider that shows its value with the unit in <c>shown</c> and passes each new value to
    /// <c>set</c>.
    /// </summary>
    private static Slider Slide(double min, double max, double step, double value, TextBlock shown, string unit, Action<float> set)
    {
        var s = new Slider { Minimum = min, Maximum = max, SmallChange = step, TickFrequency = step, IsSnapToTickEnabled = true, Value = value };
        shown.Text = $"{value:0.#} {unit}";
        shown.FontSize = 12;
        shown.VerticalAlignment = VerticalAlignment.Center;
        shown.Margin = new Thickness(6, 0, 0, 0);
        s.ValueChanged += (_, e) => { shown.Text = $"{e.NewValue:0.#} {unit}"; set((float)e.NewValue); };
        return s;
    }

    /// <summary>A " m" unit label.</summary>
    private static TextBlock M() => new() { Text = " m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Builds the panel for the view's Path tool.</summary>
    /// <param name="view">The view whose Path tool the panel drives.</param>
    /// <param name="brush">The brush whose noise the Natural look's bump rows set.</param>
    public PathPanel(GlView view, Brush brush)
    {
        _view = view;
        _path = view.Path;
        var actions = Enum.GetValues<PathTool.Action>();
        ActionBox = new ComboBox { ItemsSource = actions.Select(PathTool.Label).ToArray(), SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        ActionBox.SelectionChanged += (_, _) => { _path.Act = actions[Math.Max(0, ActionBox.SelectedIndex)]; ShowRows(); _path.Notify(); ActionChanged?.Invoke(_path.Act); };
        var widthV = new TextBlock();
        WidthSlider = Slide(1, 40, 0.5, _path.Width, widthV, "m", v => { _path.Width = v; _path.Notify(); });
        var softV = new TextBlock();
        SoftSlider = Slide(0, 15, 0.5, _path.Soft, softV, "m", v => _path.Soft = v);
        HeightBox.ValueChanged += (_, e) => _path.Height = (float)(e.NewValue ?? 0);
        AmountBox.ValueChanged += (_, e) => _path.Amount = (float)(e.NewValue ?? 0);
        StartBox.ValueChanged += (_, e) => { _path.Start = (float)(e.NewValue ?? 0); if (!_filling) { _path.RampEdited = true; } };
        EndBox.ValueChanged += (_, e) => { _path.End = (float)(e.NewValue ?? 0); if (!_filling) { _path.RampEdited = true; } };
        DepthBox.ValueChanged += (_, e) => _path.Depth = (float)(e.NewValue ?? 0);
        CavePresetBox = new ComboBox { ItemsSource = PathTool.CavePresets.Select(p => p.Name).ToArray(), SelectedIndex = 1, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        var rocks = Enum.GetValues<PathTool.CaveRock>();
        RockBox = new ComboBox { ItemsSource = rocks.Select(PathTool.Label).ToArray(), SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        RockBox.SelectionChanged += (_, _) => _path.Rock = rocks[Math.Max(0, RockBox.SelectedIndex)];
        CaveDepthBox.Minimum = 2;
        CaveDepthBox.Maximum = 20;
        HeadroomBox.Minimum = 2;
        HeadroomBox.Maximum = 15;
        CaveDepthBox.ValueChanged += (_, e) => _path.CaveDepth = (float)(e.NewValue ?? 7.5m);
        HeadroomBox.ValueChanged += (_, e) => _path.Headroom = (float)(e.NewValue ?? 4.5m);
        CavePresetBox.SelectionChanged += (_, _) => CavePreset(false);
        CaveRandomButton.Click += (_, _) => CavePreset(true);
        CavePresetBox.Tip("cave.preset");
        CaveRandomButton.Tip("cave.randomize");
        CaveDepthBox.Tip("cave.depth");
        HeadroomBox.Tip("cave.headroom");
        RockBox.Tip("cave.rock");
        DepthBox.Minimum = 0.25m;
        CurveBox.IsCheckedChanged += (_, _) => { _path.Curved = CurveBox.IsChecked == true; _path.Notify(); };
        NaturalBox.IsCheckedChanged += (_, _) => { _path.Natural = NaturalBox.IsChecked == true; ShowRows(); };
        ApplyButton.Click += (_, _) => ApplyAsked?.Invoke();
        ActionBox.Tip("path.action");
        WidthSlider.Tip("path.width");
        SoftSlider.Tip("path.soft");
        HeightBox.Tip("path.height");
        AmountBox.Tip("path.amount");
        StartBox.Tip("path.start");
        EndBox.Tip("path.end");
        DepthBox.Tip("path.depth");
        CurveBox.Tip("path.curve");
        NaturalBox.Tip("path.natural");
        ApplyButton.Tip("path.apply");
        ClearButton.Tip("path.clear");
        ClearButton.Click += (_, _) => _path.Clear();
        var ampV = new TextBlock();
        var sizeV = new TextBlock();
        _naturalRows = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Row("Bumps", Slide(0.2, 4, 0.1, brush.NoiseAmp, ampV, "m", v => brush.NoiseAmp = v).Tip("natural.amp"), ampV),
                Row("Bump size", Slide(4, 60, 1, brush.NoiseSize, sizeV, "m", v => brush.NoiseSize = v).Tip("natural.size"), sizeV),
            },
        };
        Control Only(Control c, params PathTool.Action[] for_)
        {
            _rows[c] = for_;
            return c;
        }
        Control R(string label, NumericUpDown box, params PathTool.Action[] for_)
        {
            var r = Row(label, box, M());
            _rows[r] = for_;
            return r;
        }
        Card = Ui.AsCard(new Border
        {
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Width = 300,
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = "Path", FontSize = 14, FontWeight = FontWeight.SemiBold },
                    Row("Action", ActionBox),
                    Row("Width", WidthSlider, widthV),
                    Row("Soft edge", SoftSlider, softV),
                    R("Height", HeightBox, PathTool.Action.Flatten),
                    R("Amount", AmountBox, PathTool.Action.Raise, PathTool.Action.Lower),
                    R("Start", StartBox, PathTool.Action.Ramp),
                    R("End", EndBox, PathTool.Action.Ramp),
                    R("Depth", DepthBox, PathTool.Action.River),
                    Only(Row("Preset", CavePresetBox, CaveRandomButton), PathTool.Action.Cave),
                    Only(CaveHelp, PathTool.Action.Cave),
                    R("Depth", CaveDepthBox, PathTool.Action.Cave),
                    R("Headroom", HeadroomBox, PathTool.Action.Cave),
                    Only(Row("Roof", RockBox), PathTool.Action.Cave),
                    CurveBox,
                    NaturalBox,
                    _naturalRows,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { ApplyButton, ClearButton } },
                    Info,
                    new TextBlock
                    {
                        Text = "Click points along the route, or hold and drag. Then drag a point to move it, drag the line to add a point, Ctrl + click a point to remove it; Backspace removes the last one. Alt + click picks the height (Alt + Shift: ramp end).",
                        FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        });
        _path.Changed += Refresh;
        ShowRows();
        Refresh();
    }

    /// <summary>The cave preset's values (random: each within about 15% of them, and new boulders).</summary>
    private void CavePreset(bool random)
    {
        var p = PathTool.CavePresets[Math.Max(0, CavePresetBox.SelectedIndex)];
        float F() => random ? 0.85f + 0.3f * (float)_random.NextDouble() : 1;
        static decimal Half(float v) => Math.Round((decimal)v * 2) / 2;
        WidthSlider.Value = (double)Half(p.Width * F());
        SoftSlider.Value = (double)Half(p.Soft * F());
        CaveDepthBox.Value = Math.Min(20, Half(p.Depth * F()));
        HeadroomBox.Value = Half(p.Headroom * F());
        _path.Seed = random ? _random.Next() : _path.Seed;
        CaveHelp.Text = p.Help + (random ? " Randomized: new sizes and boulders for the next Apply (Ctrl+Z takes the last one back)." : "");
    }

    /// <summary>The Cave button: the Cave action with its preset's values.</summary>
    public void ChooseCave()
    {
        ActionBox.SelectedIndex = Array.IndexOf(Enum.GetValues<PathTool.Action>(), PathTool.Action.Cave);
        CavePreset(false);
    }

    /// <summary>Shows only the rows of the current action, and the bump rows when Natural look is on.</summary>
    private void ShowRows()
    {
        foreach (var (row, for_) in _rows)
        {
            row.IsVisible = for_.Contains(_path.Act);
        }
        _naturalRows.IsVisible = _path.Natural;
    }

    /// <summary>
    /// The line or a value picked with Alt changed: refills the boxes (the ramp's ends follow the ground at the line's
    /// ends until typed in), shows the line's length and redraws it.
    /// </summary>
    public void Refresh()
    {
        _filling = true;
        var pts = _path.Points;
        // The ramp's ends follow the ground at the line's ends until typed in.
        if (pts.Count > 0 && !_path.RampEdited)
        {
            _path.Start = MathF.Round(_view.GridHeight(pts[0]), 1);
            _path.End = MathF.Round(_view.GridHeight(pts[^1]), 1);
        }
        StartBox.Value = (decimal)_path.Start;
        EndBox.Value = (decimal)_path.End;
        HeightBox.Value = (decimal)_path.Height;
        _filling = false;
        Info.Text = pts.Count > 0 ? $"{pts.Count} point(s), {PathTool.Length(_path.Curve()):0} m long." : "No line yet.";
        _view.LassoChanged();
    }
}
