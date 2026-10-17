using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

// The Dungeon tool's marks: free openings, joints, the room picked or pointed at, the room a click adds.
public sealed partial class GlView
{
    // ---- The Dungeon tool: its state, and what it shows (the free openings, the room selected or
    // pointed at, and the room a click would add, see-through).
    /// <summary>
    /// The Dungeon tool: its state, and what it shows (the free openings, the room selected or pointed at, and
    /// the room a click would add, see-through).
    /// </summary>
    public DungeonTool Dungeon { get; } = new();

    /// <summary>
    /// The Dungeon tool's lines on the graphics card: openings, the picked room, the room to add, door joints.
    /// </summary>
    private uint _dungeonVao, _dungeonVbo, _dungeonPickVao, _dungeonPickVbo, _dungeonAddVao, _dungeonAddVbo, _dungeonDoorVao, _dungeonDoorVbo, _dungeonJointVao, _dungeonJointVbo;

    /// <summary>
    /// The Dungeon tool's marks: free openings (green), joints that can take a door (blue with one, orange without),
    /// the room selected or pointed at, and the room a click adds (yellow, red when it would not fit).
    /// </summary>
    private void DrawDungeon(WorldScene s, Matrix4x4 vp)
    {
        if (Mode != ToolMode.Dungeon || Dungeon.Dungeon is not { } d)
        {
            return;
        }
        Vector3 V(Vector3 w) => ToView(s, w);
        // Free openings: a square across each, 2 m wide, 3 m high, standing on its floor point.
        var ends = new List<float>();
        var hovered = new List<float>();
        var free = Dungeon.FreeEnds;
        for (int i = 0; i < free.Count; i++)
        {
            var e = free[i];
            var side = Vector3.Transform(Vector3.UnitX, e.Rotation);
            var up = Vector3.UnitY;
            Vector3[] c = [e.Position - side, e.Position + side, e.Position + side + up * 3, e.Position - side + up * 3];
            var list = Dungeon.HoverEnd == i ? hovered : ends;
            for (int k = 0; k < 4; k++)
            {
                var a = V(c[k]);
                var b = V(c[(k + 1) % 4]);
                list.AddRange([a.X, a.Y, a.Z, b.X, b.Y, b.Z]);
            }
            var m = V(e.Position + up * 1.5f);
            var n = V(e.Position + up * 1.5f + Vector3.Transform(Vector3.UnitZ, e.Rotation) * -1.5f);
            list.AddRange([m.X, m.Y, m.Z, n.X, n.Y, n.Z]);
        }
        if (ends.Count > 0)
        {
            DrawLines(ref _dungeonVao, ref _dungeonVbo, [.. ends], vp, new Vector4(0.35f, 0.95f, 0.45f, 0.95f), blend: true, width: 2f);
        }
        // Joints that can take a door: a small square, blue with a door, orange without; a click puts one
        // or takes it away.
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
            for (int k = 0; k < 4; k++)
            {
                var a = V(c[k]);
                var b = V(c[(k + 1) % 4]);
                list.AddRange([a.X, a.Y, a.Z, b.X, b.Y, b.Z]);
            }
        }
        if (withDoor.Count > 0)
        {
            DrawLines(ref _dungeonDoorVao, ref _dungeonDoorVbo, [.. withDoor], vp, new Vector4(0.4f, 0.7f, 1f, 0.95f), blend: true, width: 2f);
        }
        if (noDoor.Count > 0)
        {
            DrawLines(ref _dungeonJointVao, ref _dungeonJointVbo, [.. noDoor], vp, new Vector4(1f, 0.6f, 0.2f, 0.9f), blend: true, width: 1.5f);
        }
        // The room selected (white) or pointed at (faint), as its box.
        var boxes = new List<float>();
        foreach (int i in new[] { Dungeon.Selected, Dungeon.HoverRoom }.OfType<int>().Distinct().Where(i => i < d.Rooms.Count))
        {
            BoxLines(boxes, Dungeons.Box(d.Rooms[i]), V);
        }
        if (boxes.Count > 0)
        {
            DrawLines(ref _dungeonPickVao, ref _dungeonPickVbo, [.. boxes], vp, new Vector4(1, 0.9f, 0.6f, 0.9f), blend: true, width: 1.5f);
        }
        // The room a click adds: its model see-through, its box green, or red when it would not fit.
        if (Dungeon.Preview is { } p)
        {
            DrawGhostInstances(s, [(p.Hash, p.Position, BlueprintFormats.ToEuler(p.Rotation), 0f)]);
            BoxLines(hovered, Dungeons.Box(p), V);
        }
        if (hovered.Count > 0)
        {
            bool ok = Dungeon.Preview is not { } pp || Dungeon.Problem(pp) == null;
            DrawLines(ref _dungeonAddVao, ref _dungeonAddVbo, [.. hovered], vp, ok ? new Vector4(1, 0.95f, 0.3f, 1) : new Vector4(1, 0.3f, 0.25f, 1), blend: true, width: 2.5f);
        }
    }

    /// <summary>
    /// The twelve edges of a turned box (centre, half size at least 25 cm, turn) as line pairs, through
    /// <paramref name="view"/>.
    /// </summary>
    private static void BoxLines(List<float> into, (Vector3 C, Vector3 H, Quaternion R) box, Func<Vector3, Vector3> view)
    {
        Vector3 h = new(Math.Max(box.H.X, 0.25f), Math.Max(box.H.Y, 0.25f), Math.Max(box.H.Z, 0.25f));
        Vector3 Corner(int k) => view(box.C + Vector3.Transform(new Vector3((k & 1) == 0 ? -h.X : h.X, (k & 2) == 0 ? -h.Y : h.Y, (k & 4) == 0 ? -h.Z : h.Z), box.R));
        foreach (var (a, b) in BoxEdges)
        {
            var p = Corner(a);
            var q = Corner(b);
            into.AddRange([p.X, p.Y, p.Z, q.X, q.Y, q.Z]);
        }
    }
}
