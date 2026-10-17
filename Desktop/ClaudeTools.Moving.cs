using System.ComponentModel;
using System.Numerics;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>
    /// move_objects: shifts and turns objects together, as one rigid group (a building keeps its shape), optionally
    /// keeping each the same height above the ground.
    /// </summary>
    [McpServerTool(Name = "move_objects", Title = "Move and turn objects")]
    [Description("Moves objects of the open area (by id) together, as the Select tool does: shifted by dx, dy, dz metres and turned by turn degrees round the vertical (as yaw turns: 90 = a quarter turn clockwise seen from above), all round one point (default: their middle), so that a building moved whole keeps its shape. ground: true keeps each one as high above the ground as it was (the ground where it ends up). A part of a group a mod links into one (a ship and the pieces built on it) brings its whole group. Each object keeps what it holds (chest contents, builder…) but gets a new id: the answer gives them. Refused when one would leave the area. One pending step.")]
    public Task<string> MoveObjects(
        [Description("Their ids (list_objects, get_selection).")] int[] ids,
        [Description("Metres east (negative: west).")] float dx = 0,
        [Description("Metres up (negative: down).")] float dy = 0,
        [Description("Metres north (negative: south).")] float dz = 0,
        [Description("Degrees to turn round the vertical (as yaw: 90 turns north to east).")] float turn = 0,
        [Description("World x of the point to turn round (optional, with pivotZ: their middle).")] float? pivotX = null,
        [Description("World z of that point.")] float? pivotZ = null,
        [Description("Keep each the same height above the ground (default false: dy alone changes heights).")] bool ground = false,
        [Description("A few words for the history.")] string label = "Claude: moved") => OnUi(() =>
    {
        var (start, problem) = StartObjectsChange(ids, pivotX, pivotZ, "Nothing moved");
        if (start == null)
        {
            return problem!;
        }
        var (scene, reads, usable, problems) = start;
        var s = _w.Session!;
        // A linked group's part never moves alone (ObjectLinks: a ship): the whole group does, as one block.
        var (ok, _, groups, _) = s.WithLinked(usable);
        if (dx == 0 && dy == 0 && dz == 0 && turn % 360 == 0 && !ground)
        {
            return "Nothing to do: give dx, dy, dz, turn or ground.";
        }
        var (px, pz) = PivotOf(scene, ok, pivotX, pivotZ);
        var (minX, minZ, maxX, maxZ) = Bounds(scene);
        var moves = new List<(int Index, Vector3 Position, Vector3 Rotation, float Scale)>();
        foreach (int i in ok)
        {
            var t = scene.Things[i];
            var (ox, oz) = Turned(t.Position.X - px, t.Position.Z - pz, turn);
            float x = px + ox + dx, z = pz + oz + dz;
            if (x < minX || x > maxX || z < minZ || z > maxZ)
            {
                return $"Nothing moved: {i} ({_w.NameOfPrefab(t.Prefab)}) would go to ({x:0.##}, {z:0.##}), outside the area (x {minX} to {maxX}, z {minZ} to {maxZ}).";
            }
            float y = t.Position.Y + dy + (ground ? GroundAt(scene, x, z) - GroundAt(scene, t.Position.X, t.Position.Z) : 0);
            moves.Add((i, new Vector3(x, y, z), Wrapped(t.Rotation + new Vector3(0, turn, 0)), t.Scale));
        }
        var copies = s.Transform(moves, ClaudesLabel(label));
        _w.AfterClaudeEdit(Said.Of("claude.msg.moved", copies.Count));
        GaveIds(scene, reads);
        return ToJson(new { moved = copies.Take(moves.Count).Select((c, k) => Moved(scene, moves[k].Index, c)), wholeGroups = groups, problems, pending = Pending() });
    });
}
