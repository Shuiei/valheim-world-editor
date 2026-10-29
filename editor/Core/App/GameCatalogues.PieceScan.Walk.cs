using AssetsTools.NET;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The build pieces' walk: see <c>Walk</c>.
    /// </summary>
    internal sealed partial class PieceScan
    {
        /// <summary>
        /// Walks a piece's tree: the corners of its active, solid box colliders into <c>pts</c> and its snap points
        /// (active or not) into <c>snaps</c>, in the piece's frame; nothing below a snap point.
        /// </summary>
        private static void Walk(Bundle b, long goPid, V3 pos, Quat rot, V3 scale, bool root, List<V3> pts, List<object?> snaps)
        {
            if (b.Field(goPid) is not { } g)
            {
                return;
            }
            // Snap points are inactive: looked at before inactive branches are left out.
            bool snap = IsSnapPoint(g);
            if (!Active(g) && !snap)
            {
                return;
            }
            var (tr, boxes) = TransformAndBoxes(b, g);
            if (tr == null)
            {
                return;
            }
            var (p, r, s) = root ? (default(V3), Quat.Root, (V3)(1.0, 1.0, 1.0)) : ChildFrame(tr, pos, rot, scale);
            if (snap)
            {
                snaps.Add(root ? Origin() : [R(p.X, 3), R(p.Y, 3), R(p.Z, 3)]);
                return;
            }
            AddBoxCorners(boxes, p, r, s, pts);
            foreach (long child in ChildObjects(b, tr))
            {
                Walk(b, child, p, r, s, false, pts, snaps);
            }
        }

        /// <summary>
        /// A game object's Transform (null when it has none) and its active, solid box colliders, from its components.
        /// The renderers, scripts and the rest are not read.
        /// </summary>
        /// <param name="b">The bundle the object is in.</param>
        /// <param name="g">The game object.</param>
        internal static (AssetTypeValueField? Transform, List<AssetTypeValueField> Boxes) TransformAndBoxes(Bundle b, AssetTypeValueField g)
        {
            AssetTypeValueField? tr = null;
            var boxes = new List<AssetTypeValueField>();
            foreach (var (t, pid) in b.ComponentIds(g))
            {
                if (t == TransformType && b.Field(pid) is { } f)
                {
                    tr = f;
                }
                else if (t == BoxColliderType && b.Field(pid) is { } c && Solid(c))
                {
                    boxes.Add(c);
                }
            }
            return (tr, boxes);
        }

        /// <summary>
        /// Adds the eight corners of each box to <paramref name="pts"/>, in the piece's frame.
        /// </summary>
        /// <param name="boxes">The boxes, in their objects' frames.</param>
        /// <param name="p">The object's position in the piece's frame.</param>
        /// <param name="r">The object's rotation.</param>
        /// <param name="s">The object's scale.</param>
        /// <param name="pts">The corners written so far.</param>
        internal static void AddBoxCorners(List<AssetTypeValueField> boxes, V3 p, Quat r, V3 s, List<V3> pts)
        {
            foreach (var box in boxes)
            {
                var (cx, cy, cz) = Vec(box["m_Center"]);
                var (sizeX, sizeY, sizeZ) = Vec(box["m_Size"]);
                foreach (double dx in new[] { -0.5, 0.5 })
                {
                    foreach (double dy in new[] { -0.5, 0.5 })
                    {
                        foreach (double dz in new[] { -0.5, 0.5 })
                        {
                            var v = r.Rotate((cx + dx * sizeX) * s.X, (cy + dy * sizeY) * s.Y, (cz + dz * sizeZ) * s.Z);
                            pts.Add(Add(v, p));
                        }
                    }
                }
            }
        }
    }
}
