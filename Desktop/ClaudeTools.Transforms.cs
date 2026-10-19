using System.ComponentModel;
using System.Numerics;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>A place, turn or size for one object (set_transform); what is left out stays as it is.</summary>
    public sealed class TransformSpec
    {
        /// <summary>The object's id.</summary>
        [Description("The object's id (list_objects).")]
        public int Id { get; set; }

        /// <summary>World x (east), in metres; null: as it is.</summary>
        [Description("World x (east), metres (optional: as it is).")]
        public float? X { get; set; }

        /// <summary>The height of its origin; null: as it is.</summary>
        [Description("Height of its origin, metres (optional: as it is).")]
        public float? Y { get; set; }

        /// <summary>World z (north), in metres; null: as it is.</summary>
        [Description("World z (north), metres (optional: as it is).")]
        public float? Z { get; set; }

        /// <summary>Turn round the vertical, in degrees; null: as it is.</summary>
        [Description("Turn round the vertical, degrees (0: its front faces north; 90: east; optional: as it is).")]
        public float? Yaw { get; set; }

        /// <summary>Tilt forward, in degrees; null: as it is.</summary>
        [Description("Tilt forward, degrees (optional: as it is).")]
        public float? Pitch { get; set; }

        /// <summary>Tilt sideways, in degrees; null: as it is.</summary>
        [Description("Tilt sideways, degrees (optional: as it is).")]
        public float? Roll { get; set; }

        /// <summary>Its size, 1 the prefab's own; null: as it is.</summary>
        [Description("Its size: 1 is the prefab's own, 2 twice as large (0.05 to 20; optional: as it is). The game draws and collides at that size; pieces' snap points and support do not grow with it.")]
        public float? Scale { get; set; }

        /// <summary>Stand on the ground at its (new) place, its lowest point touching it, when no y is given.</summary>
        [Description("With no y: stand it on the ground where it ends up, its lowest point touching it (default false).")]
        public bool OnGround { get; set; }
    }

    /// <summary>set_transform: exact places, turns and sizes, each object its own (see <see cref="TransformSpec"/>).</summary>
    [McpServerTool(Name = "set_transform", Title = "Place, turn or size objects exactly")]
    [Description("Puts objects of the open area (by id) exactly where given: position, yaw, pitch, roll and size, each object its own; what is left out stays as it is. A part of a group a mod links into one (a ship and the pieces built on it) takes its whole group along (its root first when both are given). A creature whose origin would be inside a building piece is stood on top of it (stood says how many; the answer gives the final y). Each keeps what it holds but gets a new id (the answer gives them). Objects that would leave the area are left out and named. One pending step.")]
    public Task<string> SetTransform(
        [Description("The objects and their new values.")] TransformSpec[] objects,
        [Description("A few words for the history.")] string label = "Claude: placed exactly") => OnUi(() =>
    {
        if (EditStart() is string why)
        {
            return why;
        }
        var s = _w.Session!;
        var scene = s.Scene;
        int reads = scene.Reads;
        var (ok, problems) = Usable(scene, objects.Select(o => o.Id));
        var area = Bounds(scene);
        var moves = new List<(int Index, Vector3 Position, Vector3 Rotation, float Scale)>();
        foreach (var o in objects.Where(o => ok.Contains(o.Id)).DistinctBy(o => o.Id))
        {
            var (move, problem) = PlanTransform(scene, o, area);
            if (move == null)
            {
                problems.Add(problem!);
                continue;
            }
            moves.Add(move.Value);
        }
        if (moves.Count == 0)
        {
            return "Nothing changed: " + string.Join("; ", problems);
        }
        int stood = CreatureStand.Apply(scene, null, moves);
        var copies = s.Transform(moves, ClaudesLabel(label));
        _w.AfterClaudeEdit(Said.Of("claude.msg.placed", copies.Count) + CreatureStand.Note(stood));
        // Then come the other parts of the linked groups moved (ObjectLinks), which went along.
        GaveIds(scene, reads);
        return ToJson(new { placed = copies.Take(moves.Count).Select((c, k) => Moved(scene, moves[k].Index, c)), stood, alongWithGroups = copies.Count - moves.Count, problems, pending = Pending() });
    });
}
