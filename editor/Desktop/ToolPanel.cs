using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Which tool is in use: View (look around, click picks), Select, Measure, Landform, Path, Area, Paste (from the
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
    /// <summary>The Landform tool: a whole landform in a click, from a formula, a mountain generator or a picture.</summary>
    Landform,
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
    /// <summary>The Script tool.</summary>
    Script,
    /// <summary>The Blueprints library, to paste one.</summary>
    Blueprints,
    /// <summary>The Dungeon tool.</summary>
    Dungeon,
    /// <summary>A mod's own tool (ToolPanel.ModTool).</summary>
    Mod,
}

/// <summary>
/// The tool rail (left edge) and the chosen tool's options next to it, like the web editor's: View
/// (look around and pick objects), Select (move, turn and delete objects: see SelectPanel), the sculpt
/// brushes and the Paint brush (one button; its options choose the paint). Keys as in the web editor: 1-5, 0 and
/// O pick the sculpt brushes, 6-9 the Paint brush with one of its paints, E selects, Esc goes back to View.
/// </summary>
public sealed partial class ToolPanel
{
    /// <summary>The brush shapes before the stamps, in BrushShape's order, in the language in use.</summary>
    private static string[] BuiltInShapes() =>
        [Lang.T("brush.shape.circle"), Lang.T("brush.shape.square"), Lang.T("brush.shape.ring"), Lang.T("brush.shape.ragged")];

    /// <summary>The brush settings the options change (size, strength, shape, falloff, each tool's own).</summary>
    public Brush Brush { get; } = new();
    /// <summary>The brush in use; null when the mode is not Brush.</summary>
    public BrushTool? Tool { get; private set; }
    /// <summary>The tool in use (Brush when <c>Tool</c> is set).</summary>
    public ToolMode Mode { get; private set; }
    /// <summary>
    /// The Build tool is in use: the Place tool (<see cref="Mode"/> is Place) with the Build dock, for the pieces players
    /// build. Always so in the Workshop, whose only placing tool it is.
    /// </summary>
    public bool BuildMode { get; private set; }
    /// <summary>The Select tool is in use.</summary>
    public bool SelectMode => Mode == ToolMode.Select;
    /// <summary>The tool changed (Mode and Tool say which).</summary>
    public event Action<BrushTool?>? ToolChanged;

    /// <summary>The tool rail card, on the window's left edge.</summary>
    public Control Rail { get; }
    /// <summary>The brush options card, shown only while a brush is in use.</summary>
    public Control Options { get; }

    /// <summary>The rail button of each sculpt brush.</summary>
    private readonly Dictionary<BrushTool, Button> _buttons = [];
    /// <summary>The Paint brush's rail button: one for the four paints (<see cref="Paint"/> says which it lays).</summary>
    private readonly Button _paintButton;
    /// <summary>The Paint button's picture: the colour of the paint it lays.</summary>
    private readonly Border _paintSwatch = Swatch(16);
    /// <summary>The paint the Paint brush lays: the one last chosen (a swatch in its options, or keys 6 to 9).</summary>
    public BrushTool Paint { get; private set; } = BrushTool.PaintDirt;
    /// <summary>The paints, in the order of their swatches and keys (6 to 9).</summary>
    internal static readonly BrushTool[] Paints = [BrushTool.PaintDirt, BrushTool.PaintCultivated, BrushTool.PaintPaved, BrushTool.PaintClear];
    /// <summary>The Paint brush's swatches, one per paint: a click lays that paint.</summary>
    internal Dictionary<BrushTool, Button> PaintButtons { get; } = [];

    /// <summary>The rail buttons of the other tools (Blueprints, the library, has its own property).</summary>
    private readonly Button _viewButton, _measureButton, _landformButton, _pathButton, _areaButton, _placeButton, _buildButton, _scriptButton, _dungeonButton;
    /// <summary>A brush's rail button: the Paint button for every paint (tests).</summary>
    internal Button ButtonOf(BrushTool t) => IsPaint(t) ? _paintButton : _buttons[t];

    /// <summary>The keys of the web editor's rail: one per sculpt brush, and one per paint of the Paint brush.</summary>
    public static readonly (BrushTool Tool, string Key)[] Keys =
    [
        (BrushTool.Raise, "1"), (BrushTool.Lower, "2"), (BrushTool.Flatten, "3"), (BrushTool.Smooth, "4"), (BrushTool.Natural, "0"), (BrushTool.Erode, "O"), (BrushTool.Restore, "5"),
        (BrushTool.PaintDirt, "6"), (BrushTool.PaintCultivated, "7"), (BrushTool.PaintPaved, "8"), (BrushTool.PaintClear, "9"),
    ];

    /// <summary>The Paint brush laying one of its paints, not a sculpt brush.</summary>
    internal static bool IsPaint(BrushTool t) => t is BrushTool.PaintDirt or BrushTool.PaintCultivated or BrushTool.PaintPaved or BrushTool.PaintClear;

    /// <summary>A sculpt brush's rail icon (the web editor's).</summary>
    private static string Icon(BrushTool t) => t switch
    {
        BrushTool.Raise => "raise",
        BrushTool.Lower => "lower",
        BrushTool.Flatten => "flatten",
        BrushTool.Smooth => "smooth",
        BrushTool.Natural => "natural",
        BrushTool.Erode => "erode",
        _ => "restore",
    };

    /// <summary>A paint's colour on its swatch (the web editor's): Clear paint is half grass, half bare ground.</summary>
    internal static IBrush PaintColour(BrushTool t) => t switch
    {
        BrushTool.PaintDirt => new SolidColorBrush(Color.Parse("#8a6a46")),
        BrushTool.PaintCultivated => new SolidColorBrush(Color.Parse("#4e3622")),
        BrushTool.PaintPaved => new SolidColorBrush(Color.Parse("#8d8d88")),
        _ => new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#5e8a3a"), 0.5), new GradientStop(Color.Parse("#7a6a4a"), 0.5) },
        },
    };

    /// <summary>A paint's key (6 to 9).</summary>
    internal static string KeyOf(BrushTool paint) => Keys.First(k => k.Tool == paint).Key;

    /// <summary>An empty paint swatch: a small rounded square, its colour set by who shows it.</summary>
    private static Border Swatch(double size) => new() { Width = size, Height = size, CornerRadius = Ui.ControlRadius, BorderBrush = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)), BorderThickness = new Thickness(1) };

    /// <summary>Picks the Select tool.</summary>
    public void ChooseSelect() => ChooseMode(ToolMode.Select);

    /// <summary>Picks a tool other than a brush.</summary>
    public void ChooseMode(ToolMode mode) => Choose(null, mode);

    /// <summary>Picks the Build tool: the Place tool with the Build dock.</summary>
    public void ChooseBuild() => Choose(null, ToolMode.Place, build: true);

    /// <summary>The rail's buttons and captions.</summary>
    private readonly StackPanel _rail;
    /// <summary>
    /// The Workshop: only Build, Select and View on the rail; the other tools (and their keys) are for worlds.
    /// </summary>
    public bool Workshop { get; private set; }

    /// <summary>[ and ]: the brush one metre smaller or bigger (within the Size slider's range).</summary>
    internal void ResizeBrush(int metres) => SizeSlider.Value = Math.Clamp(SizeSlider.Value + metres, SizeSlider.Minimum, SizeSlider.Maximum);

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
