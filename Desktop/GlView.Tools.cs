using System.Numerics;
using Avalonia;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The tools' state the view holds (the tool in use, the Select, Measure, Path, Area, Place and Paste tools, the
// brush) and the clicks it raises for the window.
public sealed partial class GlView
{
    /// <summary>
    /// The Select tool takes the left button (see SelectTool); otherwise it slides the view and clicks pick.
    /// </summary>
    public SelectTool SelectTool { get; }

    /// <summary>The tool in use; leaving Select commits a move under way.</summary>
    public ToolMode Mode { get; set { if (value != ToolMode.Select) { SelectTool.Commit(); } field = value; Wake(); } }
    /// <summary>True while the Select tool is in use.</summary>
    public bool SelectMode => Mode == ToolMode.Select;
    /// <summary>The Measure tool's tape.</summary>
    public MeasureTool Tape { get; } = new();
    /// <summary>Shape tool: a click on the ground (grid point).</summary>
    public event Action<float, float>? ShapeClicked;
    /// <summary>The radius the Shape tool's outline shows, in metres.</summary>
    public float ShapeRadius { get; set; } = 16;

    /// <summary>
    /// The Mountain tool's shape (metres added east and north of the middle), drawn under the pointer as
    /// a mesh on the ground before clicking; null: none.
    /// </summary>
    internal Func<float, float, float>? MountainPreview
    {
        get; set
        {
            field = value;
            _previewKey = null;
            RequestNextFrameRendering();
        }
    }
    /// <summary>The Path tool's state (its points, width, soft edge).</summary>
    public PathTool Path { get; } = new();
    /// <summary>Stamp once: a click on the ground (grid point).</summary>
    public event Action<float, float>? StampClicked;
    /// <summary>Alt + click with a brush: the ground's height there (shift: Alt + Shift).</summary>
    public event Action<float, bool>? BrushAltClick;
    /// <summary>The Area tool's state (its outline).</summary>
    public AreaTool Area { get; } = new();
    /// <summary>The Place tool's mouse (set by the window).</summary>
    public PlaceInput? Place { get; set; }
    /// <summary>The left button went down with the Place tool.</summary>
    private bool _placeDown;
    /// <summary>The Paste tool's state (what is pasted, and where).</summary>
    public PasteTool Paste { get; } = new();
    /// <summary>Paste tool: a click on the ground (grid point).</summary>
    public event Action<Vector2>? PasteClicked;
    /// <summary>The left button went down with the Area tool.</summary>
    private bool _areaDown;
    /// <summary>The left button went down with the Path tool.</summary>
    private bool _pathDown;
    /// <summary>The left button went down with the Select tool.</summary>
    private bool _selectDown;

    // ---- Tools: a sculpt or paint brush (null: looking around and picking objects).
    /// <summary>The sculpt or paint brush in use; null: looking around and picking objects.</summary>
    public BrushTool? Tool { get; set { field = value; Wake(); } }
    /// <summary>A stroke finished, with what to say about it (empty when nothing).</summary>
    public event Action<Said>? StrokeEnded;
    /// <summary>Where the pointer is over the view, null when it is outside.</summary>
    private Point? _pointer;

    /// <summary>
    /// Where the pointer was last seen over the surface (kept when it leaves): where a drag ends when the pointer is
    /// taken away.
    /// </summary>
    private Point _heldAt;
    /// <summary>The size of the surface taking the mouse, in its own units.</summary>
    private Size _surfaceSize;
    /// <summary>A brush stroke is on (the left button held).</summary>
    private volatile bool _brushDown;
    /// <summary>The brush's grid point under the pointer at the last frame, null when off the ground.</summary>
    private (float X, float Z)? _hover;
    /// <summary>For tests: the brush's grid point under the mouse.</summary>
    internal (float X, float Z)? Hover => _hover;
    /// <summary>For tests: whether the brush is painting.</summary>
    internal bool BrushDown => _brushDown;
}
