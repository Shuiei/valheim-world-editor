// The support scan's walk down a piece: each collider written as a box in the piece's own frame (see GameCatalogues).

using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The support scan of the build pieces; this part walks a piece's objects and writes their colliders as boxes.
    /// </summary>
    private sealed partial class SupportScan
    {
        /// <summary>
        /// A piece's colliders as boxes; or (meshes) the meshes its mesh colliders need, to read first.
        /// </summary>
        private void Walk(Bundle b, long goPid, V3 pos, Quat rot, V3 scale, bool root, List<object?>? boxes, List<long>? meshes)
        {
            if (b.Field(goPid) is not { } g || IsSnapPoint(g))
            {
                return;
            }
            object? layer = Get(g, "m_Layer", 0L);
            // Only the Transform and colliders read (not the renderers, scripts...).
            var (tr, cols) = TransformAndColliders(b, g);
            if (tr == null)
            {
                return;
            }
            var (p, q, s) = root ? (default(V3), Quat.Root, (V3)(1.0, 1.0, 1.0)) : ChildFrame(tr, pos, rot, scale);
            var frame = new ColliderFrame(p, q, s);
            foreach (var (t, cm) in cols)
            {
                if (ColliderBounds(t, cm, meshes) is { } bounds)
                {
                    AddBox(bounds, frame, layer, boxes);
                }
            }
            foreach (long child in ChildObjects(b, tr))
            {
                Walk(b, child, p, q, s, false, boxes, meshes);
            }
        }

        /// <summary>
        /// Writes one collider as a box entry: its centre and half size in the piece's frame, the rotation, the aabb flag
        /// and the layer.
        /// </summary>
        /// <param name="bounds">The collider's centre, half size and aabb flag, in its own frame.</param>
        /// <param name="frame">The frame of the object the collider sits on.</param>
        /// <param name="layer">The object's layer.</param>
        /// <param name="boxes">The boxes written so far, or null.</param>
        private static void AddBox((V3 C, V3 H, long Aabb) bounds, ColliderFrame frame, object? layer, List<object?>? boxes)
        {
            var (cx, cy, cz) = frame.Point(bounds.C);
            var (hx, hy, hz) = bounds.H;
            var (sx, sy, sz) = frame.Scaling;
            boxes?.Add(new List<object?> { R(cx, 4), R(cy, 4), R(cz, 4), R(Math.Abs(hx * sx), 4), R(Math.Abs(hy * sy), 4), R(Math.Abs(hz * sz), 4) }.Concat(frame.Rotation.Rounded(4)).Append(bounds.Aabb).Append(layer).ToList());
        }
    }
}
