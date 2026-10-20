using Avalonia;

namespace TerrainEditor.Desktop;

// Clicking again at the same spot: the next object behind, as Unity's Scene view does, then round again.
public sealed partial class GlView
{
    /// <summary>
    /// The last plain click: where on the view, the objects under it nearest first, and which of them it took.
    /// </summary>
    private (Point At, List<int> Hits, int Index)? _cycle;

    /// <summary>
    /// The object a plain click takes: the nearest one under the pointer, or, clicked again at the same spot (within 4
    /// pixels) with the same objects under it, the next one behind the one the last click took (round again past the
    /// last one). Null when nothing is under it.
    /// </summary>
    /// <param name="at">Where the click is, on the view.</param>
    /// <param name="size">The view's size.</param>
    private int? CycledAt(Point at, Size size)
    {
        var hits = ObjectsAt(at, size);
        if (hits.Count == 0)
        {
            _cycle = null;
            return null;
        }
        bool again = _cycle is { } c && Point.Distance(c.At, at) < 4 && c.Hits.SequenceEqual(hits);
        int next = again ? (_cycle!.Value.Index + 1) % hits.Count : 0;
        _cycle = (at, hits, next);
        return hits[next];
    }

    /// <summary>
    /// Every shown object whose drawn shape the ray through a point meets in front of the ground (half a metre of
    /// slack), nearest first, each by the nearer of its solid and foliage parts.
    /// </summary>
    /// <param name="at">The point on the view.</param>
    /// <param name="size">The view's size.</param>
    internal List<int> ObjectsAt(Point at, Size size)
    {
        if (ViewRay(at, size) is not { } ray)
        {
            return [];
        }
        var (s, o, d) = ray;
        float ground = Picking.HitGround(s, o, d) is float g ? g + 0.5f : float.MaxValue;
        var hits = new List<(float T, int I)>();
        foreach (var (t, i) in BoxesHitBy(o, d))
        {
            var (solid, foliage) = MeshHit(s, i, o, d, t);
            if (MathF.Min(solid ?? float.MaxValue, foliage ?? float.MaxValue) is var near && near < ground)
            {
                hits.Add((near, i));
            }
        }
        return [.. hits.OrderBy(h => h.T).Select(h => h.I)];
    }
}
