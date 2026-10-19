using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// Editing a scene: brush strokes on the ground, deleting and moving objects, undo and redo, what is waiting to be
/// saved, and saving. Edits go to the EditStore and are written by WorldWriter, the same code the web editor's session
/// uses (/api/zones, /api/save). Strokes run on the drawing thread, the buttons on the window's: everything here takes
/// the lock. Objects are changed on the window's thread only.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// The open area being edited.
    /// </summary>
    public WorldScene Scene { get; }

    /// <summary>
    /// The area's ground grid the brushes and tools change.
    /// </summary>
    public Ground Ground { get; }

    /// <summary>
    /// Where the edits wait to be saved or applied live (zones, deleted and added objects, zone resets).
    /// </summary>
    public EditStore Edits { get; }

    /// <summary>
    /// The window shares its brush settings with the session.
    /// </summary>
    public Brush Brush { get; set; } = new();

    /// <summary>
    /// The window's Mask too: which ground the brushes and tools may change.
    /// </summary>
    public Mask Mask { get; set; } = new();

    /// <summary>
    /// The mask of the stroke going on, judged as the ground was when it started (null when off or between strokes).
    /// </summary>
    private Func<int, float>? _strokeMask;

    /// <summary>
    /// Guards the ground, the history and the stroke: strokes run on the drawing thread, the buttons on the window's.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The undo list, oldest first (the next undo last).
    /// </summary>
    public IReadOnlyList<Change> UndoList { get { lock (_lock) { return [.. _undo]; } } }

    /// <summary>
    /// What was undone, the next redo last.
    /// </summary>
    public IReadOnlyList<Change> RedoList { get { lock (_lock) { return [.. _redo]; } } }

    /// <summary>
    /// The ground state of a change that touched no ground points.
    /// </summary>
    private static readonly Ground.State NoPoints = new([], [], [], [], [], []);

    /// <summary>
    /// The history: what can be undone (the next undo last) and what can be redone (the next redo last).
    /// </summary>
    private readonly List<Change> _undo = [], _redo = [];

    /// <summary>
    /// Steps of the world's history made in other areas, which cannot be undone here: kept as they came (each with the
    /// frame its points are in and its objects' ids) to go back into the world's history untouched (Export), so they
    /// come back where they were made.
    /// </summary>
    private readonly List<(Change Change, Frame Frame, int[] Ids)> _shelvedUndo = [], _shelvedRedo = [];

    /// <summary>
    /// Where each step stands in the order they were made, this area's and the kept-aside ones together (Export puts
    /// them back in that order, whichever list they are in); the kept history's order on Import, then as they come.
    /// By reference: a change is its own step.
    /// </summary>
    private readonly Dictionary<Change, long> _order = new(ReferenceEqualityComparer.Instance);

    /// <summary>The order the next step not seen yet gets.</summary>
    private long _nextOrder;

    /// <summary>A step's place in the order they were made; a step not seen yet comes after every other.</summary>
    internal long OrderOf(Change c) => _order.TryGetValue(c, out long o) ? o : _order[c] = _nextOrder++;

    /// <summary>
    /// How many steps of the world's history were made in other areas and are kept aside here (they can be undone
    /// or redone where they were made).
    /// </summary>
    public int Shelved
    {
        get
        {
            lock (_lock)
            {
                return _shelvedUndo.Count + _shelvedRedo.Count;
            }
        }
    }

    /// <summary>
    /// How many steps the undo list of the sessions made from now on keeps (Settings,
    /// <see cref="TerrainEditor.App.AppSettings.HistorySteps"/>); 0 or less: no limit.
    /// </summary>
    public static int StepLimit { get; set; }

    /// <summary>
    /// How much memory an undo list may take (about; <see cref="Change.Bytes"/>), whatever the steps' limit: 2 GB.
    /// </summary>
    public const long HistoryBytes = 2L << 30;

    /// <summary>
    /// How many steps this session's undo list keeps (<see cref="StepLimit"/> when it was made); 0 or less: no limit.
    /// The oldest go first, at the next change.
    /// </summary>
    internal int KeepSteps { get; set; } = StepLimit;

    /// <summary>
    /// How much memory this session's undo list may take (<see cref="HistoryBytes"/>; tests set less): past it, the
    /// oldest steps go first, at the next change. The newest step always stays.
    /// </summary>
    internal long KeepBytes { get; set; } = HistoryBytes;

    /// <summary>
    /// The brush stroke going on; null between strokes (during one, undo and redo do nothing and saving throws).
    /// </summary>
    private Stroke? _stroke;

    /// <summary>
    /// An Apply live came back during the stroke: the ground takes the edits again when the stroke ends (taken during
    /// it, they would wipe what the stroke did so far).
    /// </summary>
    private bool _takeEditsAfterStroke;

    /// <summary>
    /// The stroke going on was dropped: an Apply live read the area again during it. EndStroke says so.
    /// </summary>
    private bool _strokeDropped;

    /// <summary>
    /// The grid rectangle changed since the view last took it (to send to the graphics card).
    /// </summary>
    private (int X0, int Z0, int X1, int Z1)? _dirty;

    /// <summary>
    /// Raised after anything here changed (an edit, a save, an apply): what is pending and the undo buttons may differ.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// A change made by the user (a stroke, a tool, undo, redo, a change taken out): not a save or an apply. Live
    /// auto-apply sends these to the game.
    /// </summary>
    public event Action? EditMade;

    /// <summary>
    /// Raises Changed and EditMade after an edit by the user.
    /// </summary>
    private void Edited()
    {
        Changed?.Invoke();
        EditMade?.Invoke();
    }

    /// <summary>
    /// Things that appeared, went or came back (indices into the scene's things).
    /// </summary>
    public event Action<IReadOnlyList<int>>? ThingsChanged;

    /// <summary>
    /// After a save or an apply that read the world again, every thing is read again: the scene's things are a new
    /// list.
    /// </summary>
    public event Action? ThingsReset;

    /// <summary>
    /// New objects were added (placed, pasted, copied, restored...): their indices in Scene.Things.
    /// </summary>
    public event Action<IReadOnlyList<int>>? ThingsAdded;

    /// <summary>
    /// Ids for objects added in this session: negative, like the web editor's.
    /// </summary>
    private int _nextId = -1;

    /// <summary>
    /// A session over an open area, its ground and the edit store its edits go to.
    /// </summary>
    public EditSession(WorldScene scene, Ground ground, EditStore edits)
    {
        Scene = scene;
        Ground = ground;
        Edits = edits;
    }

    /// <summary>The list's last change (null: none), under the lock.</summary>
    private Change? Last(List<Change> list)
    {
        lock (_lock)
        {
            return list.Count > 0 ? list[^1] : null;
        }
    }

    /// <summary>Whether the list holds this very change (not an equal one), under the lock.</summary>
    private bool Holds(List<Change> list, Change c)
    {
        lock (_lock)
        {
            return list.Exists(l => ReferenceEquals(l, c));
        }
    }

    /// <summary>
    /// Counts every change to the area (a step made, undone or redone, the area read again): what was worked out from
    /// it earlier (a script's run) is out of date once it moved.
    /// </summary>
    public int Generation => Volatile.Read(ref _generation);

    /// <summary>
    /// Backs Generation; changed with Interlocked.
    /// </summary>
    private int _generation;

    /// <summary>
    /// The zones marked to be given back to the world generator (to draw them).
    /// </summary>
    public List<ZoneReset> Resets => Edits.Resets;
}
