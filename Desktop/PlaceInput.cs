using System.Numerics;
using Avalonia;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Place tool's mouse and keys (the web editor's plant.js handlers). Brush: a click places what the preview shows,
/// a drag paints more (Shift + drag removes the chosen kinds); each stroke is one undo step. Line, Zone: click points
/// (or hold and drag to draw freely), drag a point to move it, the line to add one, Ctrl + click to remove one; Circle
/// and Rectangle: press and drag; Grid: drag a box; Enter (or a double-click on the last point) places.
/// </summary>
public sealed partial class PlaceInput
{
    /// <summary>The view: hit tests, selection highlight and redraws.</summary>
    private readonly GlView _view;
    /// <summary>The Place tool whose settings and shapes the input drives.</summary>
    public PlaceTool Tool { get; }
    /// <summary>The edit session placements are committed to (null while no world is open).</summary>
    public Func<EditSession?> Session { get; set; } = () => null;
    /// <summary>A line for the status bar.</summary>
    public event Action<Said>? Message;
    /// <summary>The step <c>,</c> and <c>.</c> turn by, in degrees (Shift: 1°); null: 1°, Shift 15°.</summary>
    public float? TurnStep { get; set; }
    /// <summary>Something was placed: the kinds placed, for the recent list.</summary>
    public event Action<IReadOnlyList<string>>? Placed;

    /// <summary>
    /// Wires the input to the view and the tool; the preview is worked out again whenever the tool changes.
    /// </summary>
    public PlaceInput(GlView view, PlaceTool tool)
    {
        _view = view;
        Tool = tool;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => Paint();
        tool.Changed += Refresh;
    }

    /// <summary>
    /// What is drawn: the preview (or what a stroke has placed so far), as copies for the drawing thread.
    /// </summary>
    public PlaceTool.Placement[] Shown { get; private set; } = [];
    /// <summary>What a click or Enter would place now.</summary>
    private List<PlaceTool.Placement> _preview = [];
    /// <summary>What a click or Enter would place now (for the panel's count).</summary>
    public IReadOnlyList<PlaceTool.Placement> PreviewNow => _preview;
    /// <summary>The cursor's grid point, or null off the ground.</summary>
    private Vector2? _at;
    /// <summary>The mouse on the view, in pixels, once it has moved.</summary>
    private Point? _pointer;
    /// <summary>The view's size with the last mouse event.</summary>
    private Size _size;
    /// <summary>Shift is held: in Brush the preview is hidden (a drag removes instead).</summary>
    private bool _shift;

    /// <summary>
    /// The cursor's grid point changed, or a setting: the preview is worked out again (not during a stroke).
    /// </summary>
    public void Refresh()
    {
        if (_stroke != null)
        {
            return;
        }
        int? under = (Tool.OnTop || Tool.Building) && _pointer is Point p ? _view.ObjectAt(p, _size) : null;
        _preview = _view.Mode == ToolMode.Place && !(Tool.Mode == PlaceTool.Modes.Brush && _shift) ? Tool.Preview(_at, under) : [];
        Shown = [.. _preview];
        _view.LassoChanged();
        Changed?.Invoke();
    }
    /// <summary>The preview was worked out again.</summary>
    public event Action? Changed;

    /// <summary>Shift was pressed or let go.</summary>
    public void ShiftHeld(bool on)
    {
        if (_shift != on)
        {
            _shift = on;
            Refresh();
        }
    }

    /// <summary>
    /// Pick from world: the next click takes the kind of the object clicked (with whether Shift was held).
    /// </summary>
    public Action<string, bool>? PickOnce { get; set; }
    /// <summary>Building: Ctrl + wheel lifts the piece (or lowers it) from where the cursor points.</summary>
    public void Lift(float metres)
    {
        Tool.HeightNudge = MathF.Round(Tool.HeightNudge + metres, 2);
        if (_pointer is Point p)
        {
            Moved(p, _size);
        }
        Tool.Notify();
        Message?.Invoke(Tool.HeightNudge == 0 ? Said.Of("place.msg.liftNone") : Said.Of("place.msg.lift", Tool.HeightNudge));
    }
}
