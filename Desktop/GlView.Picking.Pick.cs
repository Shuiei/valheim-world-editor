using Avalonia;

namespace TerrainEditor.Desktop;

// The click that picks: one object, added or taken out, or the row a Ctrl + Shift + click makes.
public sealed partial class GlView
{
    /// <summary>
    /// A click on an object, by the keys held (<see cref="SelectHow.Pick"/>): it alone (clicked again at the same spot,
    /// the next one behind); added, or taken out when it
    /// was selected (Ctrl); taken out (Shift); or every shown object in a row from the last one clicked to this one
    /// added (Ctrl + Shift: w1, then Ctrl + Shift + click w3: w1, w2 and w3).
    /// </summary>
    /// <param name="at">Where the click is, on the view.</param>
    /// <param name="size">The view's size.</param>
    /// <param name="how">What the click does.</param>
    /// <returns>The object clicked, null when the click was beside every object.</returns>
    internal int? Pick(Point at, Size size, PickHow how)
    {
        // A plain click again at the same spot takes the next object behind (GlView.Picking.Cycle).
        int? i = how == PickHow.Only ? CycledAt(at, size) : ObjectAt(at, size);
        var s = Scene;
        lock (_selection)
        {
            if (how == PickHow.Row && s != null && RangeEnds(s, i) is (int a, int k))
            {
                _selection.UnionWith(Between(s, a, k));
                // The next row goes on from where this one ended.
                _anchor = k;
            }
            else
            {
                PickOne(i, how);
            }
        }
        SelectionDone();
        return i;
    }

    /// <summary>
    /// A click's object (null: none) into the selection, under its lock: alone, toggled (a row without its first
    /// object toggles too), or taken out; the next row starts from it when it is selected after the click, and from
    /// none when it was the start and is taken out.
    /// </summary>
    /// <param name="i">The object clicked, or null.</param>
    /// <param name="how">What the click does.</param>
    private void PickOne(int? i, PickHow how)
    {
        if (how == PickHow.Only)
        {
            _selection.Clear();
        }
        if (i is not int k)
        {
            return;
        }
        if (how != PickHow.Remove && (_selection.Add(k) || how == PickHow.Only))
        {
            _anchor = k;
            return;
        }
        // Taken out: a row no longer starts from it.
        _selection.Remove(k);
        if (_anchor == k)
        {
            _anchor = null;
        }
    }

    /// <summary>
    /// The row a Ctrl + Shift + click makes: from the object the last click picked to this one, when both are things in
    /// the scene and not gone; null otherwise.
    /// </summary>
    /// <param name="s">The scene (null: none).</param>
    /// <param name="i">The object clicked (null: none under the pointer).</param>
    /// <returns>The object the row starts from and the one it ends at, or null.</returns>
    internal (int A, int K)? RangeEnds(WorldScene s, int? i)
    {
        if (i is int k && _anchor is int a && a != k && a < s.Things.Count && !s.Things[a].Gone)
        {
            return (a, k);
        }
        return null;
    }

    /// <summary>
    /// The active object, as Blender's: the last one clicked, while it is selected (its outline is brighter); null when
    /// none is.
    /// </summary>
    internal int? ActiveObject
    {
        get
        {
            lock (_selection)
            {
                return _anchor is int a && _selection.Contains(a) ? a : null;
            }
        }
    }

    /// <summary>The object a Ctrl + Shift + click's row starts from: the last one clicked.</summary>
    private int? _anchor;
}
