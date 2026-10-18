using Avalonia;

namespace TerrainEditor.Desktop;

// The click that picks: one object, added or taken out, or the row a Shift + click makes.
public sealed partial class GlView
{
    /// <summary>
    /// A click on an object: it alone, or (add) added or taken out; range (Shift): every shown object in
    /// a row from the last one clicked to this one (w1, Shift + click w3: w1, w2 and w3).
    /// </summary>
    /// <returns>The object clicked, null when the click was beside every object.</returns>
    internal int? Pick(Point at, Size size, bool add, bool range = false)
    {
        int? i = ObjectAt(at, size);
        var s = Scene;
        lock (_selection)
        {
            if (range && s != null && RangeEnds(s, i) is (int a, int k))
            {
                foreach (int j in Between(s, a, k))
                {
                    _selection.Add(j);
                }
            }
            else
            {
                if (!add)
                {
                    _selection.Clear();
                }
                if (i is int k2 && !_selection.Remove(k2))
                {
                    _selection.Add(k2);
                }
                _anchor = i;
            }
        }
        SelectionDone();
        return i;
    }

    /// <summary>
    /// The row a Shift + click makes: from the object the last click picked to this one, when both are things in
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

    /// <summary>The object a Shift + click's row starts from: the last one clicked.</summary>
    private int? _anchor;
}
