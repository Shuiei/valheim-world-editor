using System.Numerics;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The selection and the things' boxes: the boxes of each thing in view space, the objects picked, the eyedropper's
// one click, and the Workshop's cut. The picks and hits are in the GlView.Picking.*.cs files beside this one.
public sealed partial class GlView
{
    // ---- Selection: the objects picked (indices into the scene's things), drawn as orange boxes.
    /// <summary>Each thing's box in view space (for picking and the selection), by thing index.</summary>
    private (Vector3 Min, Vector3 Max)[] _bounds = [];
    /// <summary>Whether a thing's box is known (its model read, and it is not gone).</summary>
    private bool[] _known = [];
    /// <summary>Where each thing was when its box was made (a move's preview may be ahead of the boxes).</summary>
    private Vector3[] _boundsAt = [];
    /// <summary>Each thing's View kind, by thing index.</summary>
    private ObjectKind[] _kinds = [];
    /// <summary>The objects picked, as indices into the scene's things.</summary>
    private readonly HashSet<int> _selection = [];
    /// <summary>The camera's view × projection of the last frame, which the mouse is read with.</summary>
    private Matrix4x4 _lastViewProj;
    /// <summary>The selection changed: its boxes are made again at the next frame.</summary>
    private bool _selectionDirty;
    /// <summary>The selection changed: the things now selected.</summary>
    public event Action<IReadOnlyList<WorldScene.Thing>>? SelectionChanged;

    /// <summary>A double click in Select mode.</summary>
    public event Action? InspectAsked;
    /// <summary>A double click on an object in the View tool: the Select tool wanted, the object picked.</summary>
    public event Action? SelectToolAsked;
    /// <summary>The objects picked, as indices into the scene's things (a copy).</summary>
    internal IReadOnlyCollection<int> Selected { get { lock (_selection) { return [.. _selection]; } } }

    /// <summary>
    /// The Workshop's cut: nothing drawn above this height (world y), and the cursor goes through what is
    /// hidden; null: none.
    /// </summary>
    internal float? CutY
    {
        get; set
        {
            field = value;
            Wake();
        }
    }

    /// <summary>
    /// Eyedropper: the next left click gives the object under it (null: none) instead of going to
    /// the tool; its release is swallowed too.
    /// </summary>
    internal Action<int?>? PickObjectOnce { get; set; }
    /// <summary>The eyedropper's click was taken: its release is swallowed.</summary>
    private bool _pickSwallow;

    /// <summary>Selects these things (add: to what is selected).</summary>
    internal void Select(IEnumerable<int> things, bool add = false)
    {
        var s = Scene;
        lock (_selection)
        {
            if (!add)
            {
                _selection.Clear();
            }
            foreach (int i in things)
            {
                if (s != null && i < s.Things.Count && !s.Things[i].Gone)
                {
                    _selection.Add(i);
                }
            }
        }
        SelectionDone();
    }

    /// <summary>The selection changed: its boxes made again, and <c>SelectionChanged</c> raised on the UI thread.</summary>
    private void SelectionDone()
    {
        _selectionDirty = true;
        var s = Scene;
        if (s != null)
        {
            var things = Selected.Select(n => s.Things[n]).ToList();
            Dispatcher.UIThread.Post(() => SelectionChanged?.Invoke(things));
        }
        Wake();
    }

    /// <summary>Tests (headless, so nothing is drawn): the boxes drawing gives things, in view space.</summary>
    internal void SetBoxes(IEnumerable<(int Index, Vector3 Min, Vector3 Max)> boxes)
    {
        var s = Scene!;
        EnsureSize(s.Things.Count);
        lock (_objLock)
        {
            foreach (var (i, lo, hi) in boxes)
            {
                var t = s.Things[i];
                _kinds[i] = ObjectKinds.Of(NameOf(t.Prefab), t.Piece || t.Joined, t.Tamed);
                _bounds[i] = (lo, hi);
                _boundsAt[i] = t.Position;
                _known[i] = !t.Gone;
            }
        }
    }

    /// <summary>Whether a thing is drawn (not gone, its kind shown, its model known): only those can be picked.</summary>
    internal bool IsPickable(int i)
    {
        lock (_objLock)
        {
            return i < _known.Length && _known[i] && _shown[(int)_kinds[i]];
        }
    }

    /// <summary>A thing's box (view space) and the position it was made at (world coordinates).</summary>
    internal (Vector3 Min, Vector3 Max, Vector3 At) BoxOf(int i)
    {
        lock (_objLock)
        {
            return (_bounds[i].Min, _bounds[i].Max, _boundsAt[i]);
        }
    }

    /// <summary>A thing's box, in view space.</summary>
    internal (Vector3 Min, Vector3 Max) BoundsOf(int i)
    {
        lock (_objLock)
        {
            return _bounds[i];
        }
    }

    /// <summary>A thing's View kind.</summary>
    internal ObjectKind KindOf(int i)
    {
        lock (_objLock)
        {
            return _kinds[i];
        }
    }

    /// <summary>Sets the object under the pointer (null: none).</summary>
    private void SetHoverObject(int? i)
    {
        if (i != HoverObject)
        {
            HoverObject = i;
            Wake();
        }
    }
}
