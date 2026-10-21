using Avalonia;
using Avalonia.Controls;
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
    /// <summary>A mod's own tool (ToolPanel.ModTool).</summary>
    Mod,
}

/// <summary>
/// The tool rail (left edge) and the chosen tool's options next to it, like the web editor's: View
/// (look around and pick objects), Select (move, turn and delete objects: see SelectPanel), the sculpt
/// brushes and the paint brushes. Keys as in the web editor: 1-9 and 0 pick the brushes, E selects,
/// Esc goes back to View.
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
