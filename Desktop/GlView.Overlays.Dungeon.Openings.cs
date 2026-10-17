using System.Numerics;

namespace TerrainEditor.Desktop;

// The Dungeon tool's free openings (a square across each, with a line to the middle) and the joints that can take a
// door, drawn in their colours.
public sealed partial class GlView
{
    /// <summary>
    /// The free openings: a square across each, 2 m wide, 3 m high, standing on its floor point (green; the one hovered
    /// joins the room to add's lines).
    /// </summary>
    /// <param name="vp">The camera's view × projection.</param>
    /// <param name="view">Turns a world point into view space.</param>
    /// <param name="hovered">The hovered lines, which the hovered opening joins.</param>
    private void DrawOpenings(Matrix4x4 vp, Func<Vector3, Vector3> view, List<float> hovered)
    {
        var ends = new List<float>();
        var free = Dungeon.FreeEnds;
        for (int i = 0; i < free.Count; i++)
        {
            var e = free[i];
            var side = Vector3.Transform(Vector3.UnitX, e.Rotation);
            var up = Vector3.UnitY;
            Vector3[] c = [e.Position - side, e.Position + side, e.Position + side + up * 3, e.Position - side + up * 3];
            var list = Dungeon.HoverEnd == i ? hovered : ends;
            QuadEdges(list, c, view);
            var m = view(e.Position + up * 1.5f);
            var n = view(e.Position + up * 1.5f + Vector3.Transform(Vector3.UnitZ, e.Rotation) * -1.5f);
            list.AddRange([m.X, m.Y, m.Z, n.X, n.Y, n.Z]);
        }
        if (ends.Count > 0)
        {
            DrawLines(ref _dungeonVao, ref _dungeonVbo, [.. ends], vp, new Vector4(0.35f, 0.95f, 0.45f, 0.95f), blend: true, width: 2f);
        }
    }

    /// <summary>
    /// The joints that can take a door: a small square, blue with a door, orange without; a click puts one or takes it
    /// away. The one hovered joins the room to add's lines.
    /// </summary>
    /// <param name="vp">The camera's view × projection.</param>
    /// <param name="view">Turns a world point into view space.</param>
    /// <param name="hovered">The hovered lines, which the hovered joint joins.</param>
    private void DrawJoints(Matrix4x4 vp, Func<Vector3, Vector3> view, List<float> hovered)
    {
        var withDoor = new List<float>();
        var noDoor = new List<float>();
        var joints = Dungeon.DoorJoints;
        for (int i = 0; i < joints.Count; i++)
        {
            var j = joints[i];
            var side = Vector3.Transform(Vector3.UnitX, j.Rotation) * 0.6f;
            var up = Vector3.UnitY;
            Vector3[] c = [j.Position + up * 0.9f - side, j.Position + up * 0.9f + side, j.Position + up * 2.1f + side, j.Position + up * 2.1f - side];
            var list = Dungeon.HoverJoint == i ? hovered : Dungeon.DoorsAt(j).Count > 0 ? withDoor : noDoor;
            QuadEdges(list, c, view);
        }
        if (withDoor.Count > 0)
        {
            DrawLines(ref _dungeonDoorVao, ref _dungeonDoorVbo, [.. withDoor], vp, new Vector4(0.4f, 0.7f, 1f, 0.95f), blend: true, width: 2f);
        }
        if (noDoor.Count > 0)
        {
            DrawLines(ref _dungeonJointVao, ref _dungeonJointVbo, [.. noDoor], vp, new Vector4(1f, 0.6f, 0.2f, 0.9f), blend: true, width: 1.5f);
        }
    }
}
