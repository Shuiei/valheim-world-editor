using System.Numerics;

namespace TerrainEditor.Desktop;

// The mesh hits: a thing's triangles decide the hit (with its stand-in box when it has no model yet).
public sealed partial class GlView
{
    /// <summary>
    /// How far along the ray a thing's drawn shape is met: by its solid parts, and by its cut-out parts
    /// (leaves, bushes, grass), null each where the ray misses them. Without its model (the stand-in
    /// box), its box's distance counts as solid. What the Workshop's cut hides (above <see cref="CutY"/>)
    /// is not met, as it is not drawn: a wall crossing the cut is met below it or not at all.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="i">The thing's index.</param>
    /// <param name="o">The ray's origin (view space).</param>
    /// <param name="d">The ray's direction, normalised.</param>
    /// <param name="box">Where the ray enters the thing's box.</param>
    private (float? Solid, float? CutOut) MeshHit(WorldScene s, int i, Vector3 o, Vector3 d, float box)
    {
        float? cutY = CutY;
        var (t, batches, rootScale) = ModelParts(s, i);
        if (t is not { } thing)
        {
            return (BoxUnderCut(i, o, d, box, cutY), null);
        }
        var (solid, cutOut, any) = PartHits(o, d, batches, Placement(s, thing, rootScale), cutY);
        return any ? (solid, cutOut) : (BoxUnderCut(i, o, d, box, cutY), null);
    }

    /// <summary>
    /// The stand-in box: where the ray enters it under the cut (anywhere without one). The cut is not a face of it.
    /// </summary>
    /// <param name="i">The thing's index.</param>
    /// <param name="o">The ray's origin (view space).</param>
    /// <param name="d">The ray's direction, normalised.</param>
    /// <param name="box">Where the ray enters the thing's box, when the cut is not in the way.</param>
    /// <param name="cutY">The cut's height (null: none).</param>
    internal float? BoxUnderCut(int i, Vector3 o, Vector3 d, float box, float? cutY)
    {
        if (cutY is not float c)
        {
            return box;
        }
        var (lo, hi) = _bounds[i];
        return Picking.HitBox(o, d, lo, hi, c);
    }

    /// <summary>
    /// The first object whose mesh the ray meets, solid parts before cut-out ones: the nearest solid hit, else the
    /// nearest cut-out hit. Stops at the first box entered past the nearest solid hit.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="o">The ray's origin (view space).</param>
    /// <param name="d">The ray's direction, normalised.</param>
    /// <param name="boxes">The objects whose boxes the ray enters, nearest first (<see cref="BoxesHitBy"/>).</param>
    /// <returns>The object hit (null: none), and how far along the ray it is met.</returns>
    internal (int? Hit, float Best) NearestMeshHit(WorldScene s, Vector3 o, Vector3 d, List<(float T, int I)> boxes)
    {
        float bestSolid = float.MaxValue, bestCut = float.MaxValue;
        int? solid = null, cutOut = null;
        foreach (var (t, i) in boxes)
        {
            if (t > bestSolid)
            {
                // Its box is entered past the nearest solid hit: nothing of it can be nearer.
                break;
            }
            var (so, cu) = MeshHit(s, i, o, d, t);
            if (so is float a && a < bestSolid)
            {
                bestSolid = a;
                solid = i;
            }
            if (cu is float c && c < bestCut)
            {
                bestCut = c;
                cutOut = i;
            }
        }
        int? hit = solid ?? cutOut;
        float best = solid != null ? bestSolid : bestCut;
        return (hit, best);
    }
}
