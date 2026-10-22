using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's building pieces and objects put down as one pending step (place_pieces), and the piece's specification.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>A piece (or any object) to put down (place_pieces, and ClaudeBuilder's layouts).</summary>
    public sealed class PieceSpec
    {
        /// <summary>The prefab name.</summary>
        [Description("Prefab name, e.g. woodwall, wood_floor, stone_wall_2x1, wood_roof_45, Beech1.")]
        public string Prefab { get; set; } = "";

        /// <summary>World x (east), in metres.</summary>
        [Description("World x (east), metres.")]
        public float X { get; set; }

        /// <summary>World z (north), in metres.</summary>
        [Description("World z (north), metres.")]
        public float Z { get; set; }

        /// <summary>The height of the piece's origin; null: standing on the ground there, its lowest point touching it.</summary>
        [Description("Height of the piece's origin (optional: it stands on the ground there, its lowest point touching it). The Workshop's plot is at y = 34.")]
        public float? Y { get; set; }

        /// <summary>Turn round the vertical, in degrees (0: front faces north).</summary>
        [Description("Turn round the vertical, degrees (0: the piece's front faces north; 90: east).")]
        public float Yaw { get; set; }

        /// <summary>Tilt forward, in degrees.</summary>
        [Description("Tilt forward, degrees (usually 0).")]
        public float Pitch { get; set; }

        /// <summary>Tilt sideways, in degrees.</summary>
        [Description("Tilt sideways, degrees (usually 0).")]
        public float Roll { get; set; }

        /// <summary>Snap to the snap points of pieces already there (and earlier in the list) within 0.5 m.</summary>
        [Description("Snap to the snap points of pieces already there (and of those before it in the list) within 0.5 m, as the game's hammer does (default: true).")]
        public bool Snap { get; set; } = true;
    }

    /// <summary>place_pieces: puts down pieces and objects as one pending step (see <see cref="Place"/>).</summary>
    [McpServerTool(Name = "place_pieces", Title = "Put down pieces and objects")]
    [Description("Puts down building pieces (and any other objects: trees, rocks, creatures…) in the open area or on the Workshop's plot, all as one step of the history (pending). Pieces snap to the snap points of pieces already there within 0.5 m, as the game's hammer does, so place them where they should join and let the snap make them meet; piece_info gives sizes and snap points. A creature whose origin would be inside a building piece is stood on top of it (the answer says how many were, and gives the final y). Returns where each went (after snapping) and its id. Check the result with support_check (Workshop) and screenshot.")]
    public Task<string> PlacePieces(
        [Description("The pieces, in the order they are put down.")] PieceSpec[] pieces,
        [Description("A few words for the history.")] string label = "Claude: building") => OnUi(() => Place(pieces, label));

    /// <summary>
    /// The pieces put down as one step (on the window's thread): where each went and its id, or why not. Pieces
    /// outside the area or unknown to the game are left out and named in the problems.
    /// </summary>
    private string Place(IReadOnlyList<PieceSpec> pieces, string label)
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        var session = _w.Session!;
        var scene = session.Scene;
        int reads = scene.Reads;
        var (minX, minZ, maxX, maxZ) = Bounds(scene);
        var placed = PlacedNear(scene, pieces);
        var adds = new List<(Editing.NewObject Object, bool Piece)>();
        var snapped = new List<bool>();
        var problems = new List<string>();
        foreach (var (p, k) in pieces.Select((p, k) => (p, k)))
        {
            int hash = Save.StableHash.Of(p.Prefab);
            var piece = Terrain.PieceCatalog.Get(hash);
            if (!scene.World.CanCreate(hash))
            {
                problems.Add($"#{k} {p.Prefab}: not an object the game has (find_prefabs)");
                continue;
            }
            if (p.X < minX || p.X > maxX || p.Z < minZ || p.Z > maxZ)
            {
                problems.Add($"#{k} {p.Prefab}: ({p.X}, {p.Z}) is outside the area (x {minX} to {maxX}, z {minZ} to {maxZ})");
                continue;
            }
            var euler = new System.Numerics.Vector3(p.Pitch, p.Yaw, p.Roll);
            var q = BlueprintFormats.FromEuler(euler);
            float y = p.Y ?? GroundAt(scene, p.X, p.Z) - Editing.Hammer.Bottom(p.Prefab, q);
            var pos = new System.Numerics.Vector3(p.X, y, p.Z);
            int? to = null;
            if (p.Snap)
            {
                (pos, to) = Editing.Hammer.Snap(p.Prefab, q, pos, placed);
            }
            placed.Add(new(-1 - k, p.Prefab, pos, q));
            adds.Add((new Editing.NewObject(0, hash, pos, euler, 0), piece?.Tool != null));
            snapped.Add(to != null);
        }
        if (adds.Count == 0)
        {
            return NothingDone("Nothing put down", problems);
        }
        int stood = CreatureStand.Apply(scene, adds);
        var ids = session.Commit(label.StartsWith("Claude", StringComparison.Ordinal) ? label : ClaudesStep(label), null, [], adds);
        _w.AfterClaudeEdit(Said.Of("claude.msg.putDown", adds.Count) + CreatureStand.Note(stood));
        GaveIds(scene, reads);
        var report = adds.Select((a, i) => new { id = i < ids.Count ? ids[i] : -1, piece = new { prefab = _w.NameOfPrefab(a.Object.Prefab), x = MathF.Round(a.Object.Position.X, 3), y = MathF.Round(a.Object.Position.Y, 3), z = MathF.Round(a.Object.Position.Z, 3), snapped = snapped[i] } });
        return ToJson(new { placed = report, stood, problems, pending = Pending() });
    }
}
