using System.Numerics;

namespace TerrainEditor.Desktop;

// Objects hidden in the view (Ctrl + H, as Blender's H): not drawn, not picked, until shown again; the world keeps them.
public sealed partial class GlView
{
    /// <summary>
    /// The objects hidden in the view: drawn and picked like gone ones until shown again. Replaced whole on a change,
    /// so the drawing reads a set no one writes to; emptied when another area opens or its objects are read again.
    /// </summary>
    private HashSet<int> _hidden = [];

    /// <summary>How many objects are hidden in the view.</summary>
    internal int HiddenCount => _hidden.Count;

    /// <summary>Hides these objects in the view (taken out of the selection too).</summary>
    /// <param name="things">The objects to hide.</param>
    internal void Hide(IEnumerable<int> things)
    {
        var list = things.ToList();
        SetHidden([.. _hidden.Concat(list)]);
        Unselect(list);
    }

    /// <summary>Shows every hidden object again; how many there were.</summary>
    internal int ShowHidden()
    {
        int n = _hidden.Count;
        SetHidden([]);
        return n;
    }

    /// <summary>
    /// The hidden objects are now these: the groups of those hidden or shown again are drawn again, and their green
    /// dots found again.
    /// </summary>
    /// <param name="hidden">The new set of hidden objects.</param>
    private void SetHidden(HashSet<int> hidden)
    {
        var old = _hidden;
        _hidden = hidden;
        if (Scene is { } s)
        {
            lock (_groups)
            {
                foreach (int i in old.Concat(hidden).Where(i => i < s.Things.Count))
                {
                    if (_groups.TryGetValue(KeyOf(s.Things[i]), out var g))
                    {
                        MarkDirty(g);
                    }
                }
            }
        }
        _marksDirty = true;
        _selectionDirty = true;
        Wake();
    }

    /// <summary>
    /// The camera turned onto the selection, close enough to see all of it (Shift + F, as a map editor's frame): the
    /// middle of the selected objects' boxes, from a distance one and a half times their size (10 m at least).
    /// False when nothing selected has a box yet.
    /// </summary>
    internal bool FrameSelection()
    {
        if (Scene is not { } s)
        {
            return false;
        }
        var boxes = Selected.Where(i => i < _bounds.Length && _known[i]).Select(i => _bounds[i]).ToList();
        if (boxes.Count == 0)
        {
            return false;
        }
        var lo = boxes.Aggregate(boxes[0].Min, (a, b) => Vector3.Min(a, b.Min));
        var hi = boxes.Aggregate(boxes[0].Max, (a, b) => Vector3.Max(a, b.Max));
        var c = (lo + hi) / 2;
        Focus(new Vector3(c.X + s.Cx, c.Y, -c.Z + s.Cz), MathF.Max(10, 1.5f * Vector3.Distance(lo, hi)));
        return true;
    }
}
