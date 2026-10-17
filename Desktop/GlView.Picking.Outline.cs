using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The outlines: the box an object's outline marks (a standing tree is a column round its root), and the selection's
// box lines drawn in orange.
public sealed partial class GlView
{
    /// <summary>
    /// What a thing's outline marks (selected, or under the pointer): its box, except for a standing tree,
    /// where it is a column round the root the full height of the tree (a tenth of the crown's width
    /// across, 0.5 to 2 m either way), so the outline shows the trunk, not the whole crown. A log or a
    /// stump (wider than tall) keeps its box.
    /// </summary>
    /// <param name="kind">The thing's View kind.</param>
    /// <param name="box">Its box, in view space.</param>
    /// <param name="root">Where it stands (its position), in view space.</param>
    internal static (Vector3 Min, Vector3 Max) OutlineBox(ObjectKind kind, (Vector3 Min, Vector3 Max) box, Vector3 root)
    {
        if (kind != ObjectKind.Trees)
        {
            return box;
        }
        float w = MathF.Max(box.Max.X - box.Min.X, box.Max.Z - box.Min.Z);
        if (box.Max.Y - box.Min.Y < 0.6f * w)
        {
            return box;
        }
        float r = Math.Clamp(0.1f * w, 0.5f, 2f);
        return (new Vector3(root.X - r, box.Min.Y, root.Z - r), new Vector3(root.X + r, box.Max.Y, root.Z + r));
    }

    /// <summary>A thing's outline box (<see cref="OutlineBox"/>), under the objects lock.</summary>
    private (Vector3 Min, Vector3 Max) OutlineBoxOf(WorldScene s, int i)
    {
        var at = _boundsAt[i];
        return OutlineBox(_kinds[i], _bounds[i], ToView(s, at));
    }

    /// <summary>The line shader (made at first use), and the selection's lines on the graphics card.</summary>
    private uint _lineProg, _lineVao, _lineVbo;

    /// <summary>
    /// Over this many selected objects, one box round them all instead of a box each: thousands of boxes
    /// over a stacked structure make a solid blob that hides it.
    /// </summary>
    internal const int BoxEachUpTo = 500;

    /// <summary>
    /// The selection's box lines: a box round each object (12 edges, two points each), or, past
    /// <see cref="BoxEachUpTo"/> objects, one box round all of them.
    /// </summary>
    /// <param name="bounds">The selected objects' boxes.</param>
    internal static float[] SelectionBoxes(IReadOnlyList<(Vector3 Min, Vector3 Max)> bounds)
    {
        var v = new List<float>();
        void Box(Vector3 lo, Vector3 hi)
        {
            Vector3 C(int k) => new((k & 1) == 0 ? lo.X : hi.X, (k & 2) == 0 ? lo.Y : hi.Y, (k & 4) == 0 ? lo.Z : hi.Z);
            foreach (var (a, b) in BoxEdges)
            {
                var p = C(a); var q = C(b);
                v.AddRange([p.X, p.Y, p.Z, q.X, q.Y, q.Z]);
            }
        }
        if (bounds.Count > BoxEachUpTo)
        {
            Vector3 lo = bounds[0].Min, hi = bounds[0].Max;
            foreach (var (min, max) in bounds)
            {
                lo = Vector3.Min(lo, min);
                hi = Vector3.Max(hi, max);
            }
            Box(lo, hi);
        }
        else
        {
            foreach (var (lo, hi) in bounds)
            {
                Box(lo, hi);
            }
        }
        return [.. v];
    }

    /// <summary>
    /// The selected objects' boxes, orange, seen through what is in front of them (one box round a large
    /// selection, <see cref="SelectionBoxes"/>).
    /// </summary>
    private unsafe void DrawSelection(Matrix4x4 vp)
    {
        if (_selectionDirty && Scene is { } s)
        {
            _selectionDirty = false;
            // Only things whose box is known: one without its model yet has no box to draw (it is drawn once it has).
            _selectionSegs = SelectionBoxes([.. Selected.Where(i => i < _bounds.Length && _known[i]).Select(i => OutlineBoxOf(s, i))]);
        }
        // Seen through what is in front of it, like the web editor's selection.
        DrawLines(ref _lineVao, ref _lineVbo, _selectionSegs, vp, new Vector4(1, 0.66f, 0.2f, 1));
    }

    /// <summary>The selection's box lines, made again when it changes.</summary>
    private float[] _selectionSegs = [];
}
