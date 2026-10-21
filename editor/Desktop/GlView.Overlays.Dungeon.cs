using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

// The Dungeon tool's marks: free openings, joints, the room picked or pointed at, the room a click adds
// (GlView.Overlays.Dungeon.Openings.cs for the openings and joints).
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
        // The hovered opening's and joint's lines gather here, and the room to add's box joins them: drawn last, together.
        var hovered = new List<float>();
        DrawOpenings(vp, V, hovered);
        DrawJoints(vp, V, hovered);
        DrawPickedRoom(d, vp, V);
        DrawRoomToAdd(s, vp, V, hovered);
    }

    /// <summary>
    /// The room selected (white) or pointed at (faint), as its box.
    /// </summary>
    /// <param name="d">The dungeon the picked rooms are in.</param>
    /// <param name="vp">The camera's view × projection.</param>
    /// <param name="view">Turns a world point into view space.</param>
    private void DrawPickedRoom(DungeonRooms.Dungeon d, Matrix4x4 vp, Func<Vector3, Vector3> view)
    {
        var boxes = new List<float>();
        foreach (int i in new[] { Dungeon.Selected, Dungeon.HoverRoom }.OfType<int>().Distinct().Where(i => i < d.Rooms.Count))
        {
            BoxLines(boxes, Dungeons.Box(d.Rooms[i]), view);
        }
        if (boxes.Count > 0)
        {
            DrawLines(ref _dungeonPickVao, ref _dungeonPickVbo, [.. boxes], vp, new Vector4(1, 0.9f, 0.6f, 0.9f), blend: true, width: 1.5f);
        }
    }

    /// <summary>
    /// The room a click adds: its model see-through, its box green, or red when it would not fit. The box joins the
    /// hovered lines, drawn with them.
    /// </summary>
    /// <param name="s">The area.</param>
    /// <param name="vp">The camera's view × projection.</param>
    /// <param name="view">Turns a world point into view space.</param>
    /// <param name="hovered">The hovered lines, which the room's box joins.</param>
    private void DrawRoomToAdd(WorldScene s, Matrix4x4 vp, Func<Vector3, Vector3> view, List<float> hovered)
    {
        if (Dungeon.Preview is { } p)
        {
            DrawGhostInstances(s, [(p.Hash, p.Position, BlueprintFormats.ToEuler(p.Rotation), 0f)]);
            BoxLines(hovered, Dungeons.Box(p), view);
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

    /// <summary>
    /// The four edges of a quad (its corners in order) as line pairs, through <paramref name="view"/>.
    /// </summary>
    /// <param name="into">Where the pairs go.</param>
    /// <param name="corners">The four corners, in order round the quad.</param>
    /// <param name="view">Turns a world point into view space.</param>
    internal static void QuadEdges(List<float> into, Vector3[] corners, Func<Vector3, Vector3> view)
    {
        for (int k = 0; k < 4; k++)
        {
            var a = view(corners[k]);
            var b = view(corners[(k + 1) % 4]);
            into.AddRange([a.X, a.Y, a.Z, b.X, b.Y, b.Z]);
        }
    }
}
