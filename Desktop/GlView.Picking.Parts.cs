using System.Numerics;

namespace TerrainEditor.Desktop;

// The parts of a thing's model that a ray is tested against.
public sealed partial class GlView
{
    /// <summary>
    /// A thing and the parts of its model: a copy of the group's batches (the drawing thread adds them as the model
    /// comes in), and the root's scale. The thing is null when the scene has no such thing, or its model is not
    /// ready (or has no parts).
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="i">The thing's index.</param>
    private (WorldScene.Thing? Thing, (Batch Batch, Matrix4x4 Pre)[] Batches, Vector3 RootScale) ModelParts(WorldScene s, int i)
    {
        WorldScene.Thing t;
        lock (s.Things)
        {
            if (i >= s.Things.Count)
            {
                return (null, [], default);
            }
            t = _previews.TryGetValue(i, out var p) ? p : s.Things[i];
        }
        lock (_groups)
        {
            if (!_groups.TryGetValue(KeyOf(t), out var g) || !g.HasModel || !g.Ready || g.Batches.Count == 0)
            {
                return (null, [], default);
            }
            return (t, [.. g.Batches], g.RootScale);
        }
    }

    /// <summary>
    /// The ray's hit on each part of a model: the solid parts' nearest and the cut-out parts' nearest, and whether
    /// any part of the model could be tested.
    /// </summary>
    /// <param name="o">The ray's origin (view space).</param>
    /// <param name="d">The ray's direction, normalised.</param>
    /// <param name="batches">The model's batches, with each one's placement in the group.</param>
    /// <param name="m">The thing's placement.</param>
    /// <param name="cutY">The cut's height (null: none).</param>
    private (float? Solid, float? CutOut, bool Any) PartHits(Vector3 o, Vector3 d, (Batch Batch, Matrix4x4 Pre)[] batches, Matrix4x4 m, float? cutY)
    {
        float? solid = null, cutOut = null;
        bool any = false;
        foreach (var (b, pre) in batches)
        {
            if (!_pickMeshes.TryGetValue(b.MeshId, out var pm) || b.Sub >= pm.Submeshes.Length)
            {
                continue;
            }
            any = true;
            if (Picking.HitMesh(o, d, pm.Positions, pm.Submeshes[b.Sub], pm.Bounds, pre * m, cutY ?? float.MaxValue) is not float h)
            {
                continue;
            }
            if (b.Material.Cutoff > 0)
            {
                cutOut = cutOut is float c && c < h ? c : h;
            }
            else
            {
                solid = solid is float a && a < h ? a : h;
            }
        }
        return (solid, cutOut, any);
    }
}
