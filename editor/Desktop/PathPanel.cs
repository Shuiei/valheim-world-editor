using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Path tool's panel, like the web editor's: the action, width and soft edge, each action's own values, curve and
/// natural look, Apply and Clear, and how long the line is.
/// </summary>
public sealed partial class PathPanel
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
    internal Button CaveRandomButton { get; } = new() { Content = Lang.T("common.randomize"), FontSize = 12, Margin = new Thickness(6, 0, 0, 0) };
    /// <summary>Cave: depth below the ground, 2 to 20 m.</summary>
    internal NumericUpDown CaveDepthBox { get; } = Num(7.5m, 0.5m);
    /// <summary>Cave: headroom inside, 2 to 15 m.</summary>
    internal NumericUpDown HeadroomBox { get; } = Num(4.5m, 0.5m);
    /// <summary>Cave: the roof's boulders, in <c>PathTool.CaveRock</c> order.</summary>
    internal ComboBox RockBox { get; }
    /// <summary>Cave: the preset's description (and a note after Randomize).</summary>
    internal TextBlock CaveHelp { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
    /// <summary>The cave presets, named in the language in use, in the preset list's order.</summary>
    private readonly PathTool.CavePreset[] _presets = PathTool.CavePresets();
    /// <summary>For Randomize: the varied values and the new boulder seed.</summary>
    private readonly Random _random = new();
    /// <summary>Smooth curve through the points, or straight stretches.</summary>
    internal CheckBox CurveBox { get; } = new() { Content = Lang.T("path.label.curve"), IsChecked = true, FontSize = 12 };
    /// <summary>Natural look; shows the bump rows when on.</summary>
    internal CheckBox NaturalBox { get; } = new() { Content = Lang.T("path.label.natural"), FontSize = 12 };
    /// <summary>Asks for the action to be applied (raises <see cref="ApplyAsked"/>).</summary>
    internal Button ApplyButton { get; } = new Button { Content = Lang.T("path.label.apply"), FontSize = 12 }.Classed("primary");
    /// <summary>Removes the line.</summary>
    internal Button ClearButton { get; } = new() { Content = Lang.T("common.clearEsc"), FontSize = 12 };
    /// <summary>How many points the line has and how long it is.</summary>
    internal TextBlock Info { get; } = new() { FontSize = 12, Foreground = Ui.Muted };
    /// <summary>The rows only some actions show, with the actions that show them.</summary>
    private readonly Dictionary<Control, PathTool.Action[]> _rows = [];
    /// <summary>The Natural look's bump rows (the brush's noise amplitude and size).</summary>
    private readonly Control _naturalRows;
    /// <summary>The width slider's value, shown with its unit.</summary>
    private readonly TextBlock _widthShown = new();
    /// <summary>The soft edge slider's value, shown with its unit.</summary>
    private readonly TextBlock _softShown = new();
    /// <summary>Apply was asked for (the window does it: it has the edit session).</summary>
    public event Action? ApplyAsked;
    /// <summary>The action was changed in the list.</summary>
    public event Action<PathTool.Action>? ActionChanged;

    /// <summary>A number box starting at <c>v</c> that steps by <c>step</c>.</summary>
    private static NumericUpDown Num(decimal v, decimal step) => new() { Value = v, Increment = step, FormatString = "0.0#", FontSize = 12 };

    /// <summary>
    /// A slider that shows its value with the unit in <c>shown</c> and passes each new value to
    /// <c>set</c>.
    /// </summary>
    private static Slider Slide(double min, double max, double step, double value, TextBlock shown, string unit, Action<float> set) =>
        Ui.Slide(min, max, step, value, shown, v => $"{v:0.#} {unit}", v => set((float)v));

    /// <summary>An "m" unit label, a little apart from its box.</summary>
    private static TextBlock M() => new() { Text = Lang.T("common.metres"), Margin = new Thickness(3, 0, 0, 0), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Builds the panel for the view's Path tool.</summary>
    /// <param name="view">The view whose Path tool the panel drives.</param>
    /// <param name="brush">The brush whose noise the Natural look's bump rows set.</param>
    public PathPanel(GlView view, Brush brush)
    {
        _view = view;
        _path = view.Path;
        var actions = Enum.GetValues<PathTool.Action>();
        ActionBox = new ComboBox { ItemsSource = actions.Select(a => PathTool.Label(a).Shown).ToArray(), SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        ActionBox.SelectionChanged += (_, _) => { _path.Act = actions[Math.Max(0, ActionBox.SelectedIndex)]; ShowRows(); _path.Notify(); ActionChanged?.Invoke(_path.Act); };
        WidthSlider = Slide(1, 40, 0.5, _path.Width, _widthShown, "m", v => { _path.Width = v; _path.Notify(); });
        SoftSlider = Slide(0, 15, 0.5, _path.Soft, _softShown, "m", v => _path.Soft = v);
        CavePresetBox = new ComboBox { ItemsSource = _presets.Select(p => p.Name).ToArray(), SelectedIndex = 1, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        var rocks = Enum.GetValues<PathTool.CaveRock>();
        RockBox = new ComboBox { ItemsSource = rocks.Select(PathTool.Label).ToArray(), SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        WireCave(rocks);
        WireControls();
        _naturalRows = BuildNaturalRows(brush);
        Card = BuildCard();
        _path.Changed += Refresh;
        ShowRows();
        Refresh();
    }
}
