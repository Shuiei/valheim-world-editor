using System.Numerics;
using Avalonia;

namespace TerrainEditor.Desktop;

// The object under a point of the view: the nearest box, then its mesh, and the objects in a row for a Shift click.
public sealed partial class GlView
{
    /// <summary>
    /// The shown object under a point of the view (null: the ground or nothing is nearer): the nearest one
    /// whose drawn shape the ray meets, unless the ground is hit first (half a metre of slack). The boxes say
    /// which objects to try; then the triangles of each one's model decide, so a click between a tree's
    /// branches finds the chest under them. Foliage (leaves, bushes, grass) counts only when nothing solid is
    /// met along the ray; a cut-out piece (a bed's fur) is solid, so a bed on a floor is picked before the floor;
    /// an object without its model yet is its stand-in box.
    /// </summary>
    internal int? ObjectAt(Point at, Size size)
    {
        if (ViewRay(at, size) is not { } ray)
        {
            return null;
        }
        var (s, o, d) = ray;
        var (hit, best) = NearestMeshHit(s, o, d, BoxesHitBy(o, d));
        if (hit != null && Picking.HitGround(s, o, d, best + 1) is float g && g + 0.5f < best)
        {
            return null;
        }
        return hit;
    }

    /// <summary>
    /// The objects whose boxes the ray enters, nearest first, with how far along the ray each is entered. Not
    /// what the cut hides (whole above it), and not what is not drawn.
    /// </summary>
    /// <param name="o">The ray's origin (view space).</param>
    /// <param name="d">The ray's direction, normalised.</param>
    internal List<(float T, int I)> BoxesHitBy(Vector3 o, Vector3 d) => BoxesHitBy(o, d, Enumerable.Range(0, _bounds.Length));

    /// <summary>
    /// <see cref="BoxesHitBy(Vector3, Vector3)"/> among some objects only (those that can stand in front of
    /// what is looked for).
    /// </summary>
    /// <param name="o">The ray's origin (view space).</param>
    /// <param name="d">The ray's direction, normalised.</param>
    /// <param name="among">The objects to try.</param>
    private List<(float T, int I)> BoxesHitBy(Vector3 o, Vector3 d, IEnumerable<int> among)
    {
        var boxes = new List<(float T, int I)>();
        foreach (int i in among)
        {
            if (Hittable(i) && Picking.HitBox(o, d, _bounds[i].Min, _bounds[i].Max) is float t)
            {
                boxes.Add((t, i));
            }
        }
        boxes.Sort((a, b) => a.T.CompareTo(b.T));
        return boxes;
    }

    /// <summary>
    /// Whether an object can be met by a ray: its box is known, its kind shown, it is not hidden (GlView.Hide), and the
    /// cut does not hide it (whole above it).
    /// </summary>
    /// <param name="i">The object.</param>
    private bool Hittable(int i) =>
        _known[i] && _shown[(int)_kinds[i]] && !_hidden.Contains(i) && !(CutY is float cut && _bounds[i].Min.Y > cut);

    /// <summary>
    /// The objects in a row on the line from object a to object b (their middles within 0.75 m of it), both
    /// ends included.
    /// </summary>
    internal IEnumerable<int> Between(WorldScene s, int a, int b)
    {
        var pa = s.Things[a].Position;
        var pb = s.Things[b].Position;
        var ab = pb - pa;
        float len2 = MathF.Max(ab.LengthSquared(), 1e-6f);
        for (int i = 0; i < s.Things.Count && i < _known.Length; i++)
        {
            var t = s.Things[i];
            if (t.Gone || !_known[i] || !_shown[(int)_kinds[i]])
            {
                continue;
            }
            float u = Math.Clamp(Vector3.Dot(t.Position - pa, ab) / len2, 0, 1);
            if (Vector3.Distance(t.Position, pa + ab * u) <= 0.75f)
            {
                yield return i;
            }
        }
    }
}
