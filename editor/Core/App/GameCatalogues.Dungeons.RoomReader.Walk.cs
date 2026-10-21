using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class DungeonScan
    {
        private sealed partial class RoomReader
        {
            /// <summary>
            /// Depth first, in child order: the order the game finds the components in, so the order it
            /// rolls the random parts in. Inactive objects and their children are left out.
            /// </summary>
            private void Walk(long gid, V3 pos, Quat rot, bool root)
            {
                if (_open.Node(gid) is not { } g || _open.Transform(g.Tr) is not { } t)
                {
                    return;
                }
                if (!root)
                {
                    if (!g.Active)
                    {
                        return;
                    }
                    pos = Add(pos, rot.Rotate(t.Pos));
                    rot = rot.Mul(t.Rot);
                }
                if (!root && g.Tr == _interiorTr)
                {
                    _interior = Placement(pos, rot, root);
                }
                _snap = SnapOf(g.Behaviours);
                foreach (var (k, pid) in g.Behaviours)
                {
                    AddBehaviour(gid, g.Name, k, pid, pos, rot, root);
                }
                foreach (long child in t.Children)
                {
                    Walk(child, pos, rot, false);
                }
            }

            /// <summary>The snap-to-ground offset of the object being walked, when it has a SnapToGround.</summary>
            private double? _snap;

            /// <summary>
            /// The offset above the ground an object's SnapToGround puts it at, when it has one (the game snaps it as it
            /// wakes, enabled or not); null without.
            /// </summary>
            private double? SnapOf(List<(string Kind, long Pid)> behaviours) =>
                behaviours.FirstOrDefault(b => b.Kind == "snap") is { Pid: not 0 } snap && _open.B.Field(snap.Pid) is { } f ? R(f["m_offset"].AsFloat, 4) : null;

            /// <summary>
            /// Where an object stands: the root's origin (whole numbers) and its rotation, or the position and rotation in
            /// the prefab's frame.
            /// </summary>
            /// <param name="pos">The position in the prefab's frame.</param>
            /// <param name="rot">The rotation in the prefab's frame.</param>
            /// <param name="root">Whether it is the room's root.</param>
            private static List<object?> Placement(V3 pos, Quat rot, bool root) =>
                root ? [.. Origin(), .. rot.Rounded(4)] : [R(pos.X, 4), R(pos.Y, 4), R(pos.Z, 4), .. rot.Rounded(4)];
        }
    }
}
