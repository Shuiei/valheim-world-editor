using System.ComponentModel;
using System.Numerics;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>
    /// How each copy sits from the one before: shifted by Dx, Dy and Dz metres, turned by Turn degrees about the pivot
    /// (PivotX, PivotZ), and kept as high above the ground as its original when Ground.
    /// </summary>
    /// <param name="PivotX">World x of the point turned round.</param>
    /// <param name="PivotZ">World z of that point.</param>
    /// <param name="Dx">Metres east of each copy from the one before.</param>
    /// <param name="Dy">Metres up.</param>
    /// <param name="Dz">Metres north.</param>
    /// <param name="Turn">Degrees each copy is turned from the one before.</param>
    /// <param name="Ground">Whether each keeps its height above the ground.</param>
    private readonly record struct CopyStep(float PivotX, float PivotZ, float Dx, float Dy, float Dz, float Turn, bool Ground);

    /// <summary>
    /// The copies of the objects, each a step further (see <see cref="CopyStep"/>): the new objects with whether each is a
    /// piece, and the originals' indices; or why one would leave the area.
    /// </summary>
    /// <param name="s">The session the copies are made in.</param>
    /// <param name="scene">The open area.</param>
    /// <param name="ok">The originals' indices into the scene's things.</param>
    /// <param name="copies">How many copies.</param>
    /// <param name="step">How each copy sits from the one before.</param>
    /// <param name="area">The area's bounds in world metres.</param>
    private (List<(Editing.NewObject O, bool Piece)>? Adds, List<int>? From, string? Problem) PlanCopies(EditSession s, WorldScene scene, List<int> ok, int copies, CopyStep step, (float MinX, float MinZ, float MaxX, float MaxZ) area)
    {
        var linkIds = s.ReadLinks().Ids;
        var adds = new List<(Editing.NewObject, bool)>();
        var from = new List<int>();
        for (int k = 1; k <= copies; k++)
        {
            var (copy, problem) = CopyOnce(s, scene, ok, k, step, area);
            if (copy == null)
            {
                return (null, null, problem);
            }
            // A linked group's parts go with it (ObjectLinks: a ship); each copy gets ids of its own below.
            var own = ObjectLinks.Remap([.. copy.Select(c => c.O)], linkIds);
            for (int j = 0; j < copy.Count; j++)
            {
                adds.Add((own[j], copy[j].Piece));
                from.Add(copy[j].From);
            }
        }
        return (adds, from, null);
    }

    /// <summary>
    /// The k-th copy of the objects (see <see cref="CopyStep"/>), each with whether it is a piece and its original's index;
    /// or why one would leave the area.
    /// </summary>
    /// <param name="s">The session the copy is made in.</param>
    /// <param name="scene">The open area.</param>
    /// <param name="ok">The originals' indices into the scene's things.</param>
    /// <param name="k">Which copy: 1 is the first.</param>
    /// <param name="step">How each copy sits from the one before.</param>
    /// <param name="area">The area's bounds in world metres.</param>
    private (List<(Editing.NewObject O, bool Piece, int From)>? Copy, string? Problem) CopyOnce(EditSession s, WorldScene scene, List<int> ok, int k, CopyStep step, (float MinX, float MinZ, float MaxX, float MaxZ) area)
    {
        var copy = new List<(Editing.NewObject O, bool Piece, int From)>();
        foreach (int i in ok)
        {
            var t = scene.Things[i];
            var (ox, oz) = Turned(t.Position.X - step.PivotX, t.Position.Z - step.PivotZ, step.Turn * k);
            float cx = step.PivotX + ox + step.Dx * k, cz = step.PivotZ + oz + step.Dz * k;
            if (cx < area.MinX || cx > area.MaxX || cz < area.MinZ || cz > area.MaxZ)
            {
                return (null, $"Nothing copied: copy {k} of {i} ({_w.NameOfPrefab(t.Prefab)}) would be at ({cx:0.##}, {cz:0.##}), outside the area (x {area.MinX} to {area.MaxX}, z {area.MinZ} to {area.MaxZ}).");
            }
            float cy = t.Position.Y + step.Dy * k + (step.Ground ? GroundAt(scene, cx, cz) - GroundAt(scene, t.Position.X, t.Position.Z) : 0);
            // As the Select tool's copy: what it was copied from, with all it holds.
            var (source, data) = s.CopySourceOf(t);
            copy.Add((new Editing.NewObject(0, t.Prefab, new Vector3(cx, cy, cz), Wrapped(t.Rotation + new Vector3(0, step.Turn * k, 0)), t.Scale,
                source, true, Data: data), t.Piece, i));
        }
        return (copy, null);
    }

    /// <summary>
    /// copy_objects: copies of objects, shifted and turned as one group, once or several times in a row (each copy a
    /// step further), new independent objects as the Select tool's copy makes them.
    /// </summary>
    [McpServerTool(Name = "copy_objects", Title = "Copy objects")]
    [Description("Copies objects of the open area (by id) as one group, as the editor's copy and paste does: shifted by dx, dy, dz metres and turned by turn degrees round one point (default: their middle), copies times in a row, each copy a step further (a row of houses, a fence, a ring of pillars with turn and a pivot). Copies are new objects of the same kinds and sizes, with all the originals hold (builder, chest contents, text, portal tags, health). A part of a group a mod links into one (a ship and the pieces built on it) brings its whole group, and each copy is a group of its own. ground: true keeps each copy as high above the ground as its original. Refused when a copy would leave the area. One pending step; the answer gives the new ids.")]
    public Task<string> CopyObjects(
        [Description("Their ids (list_objects, get_selection).")] int[] ids,
        [Description("Metres east of each copy from the one before (negative: west).")] float dx = 0,
        [Description("Metres up.")] float dy = 0,
        [Description("Metres north (negative: south).")] float dz = 0,
        [Description("Degrees each copy is turned from the one before, round the pivot (as yaw: 90 turns north to east).")] float turn = 0,
        [Description("World x of the point to turn round (optional, with pivotZ: their middle).")] float? pivotX = null,
        [Description("World z of that point.")] float? pivotZ = null,
        [Description("How many copies (default 1, up to 100).")] int copies = 1,
        [Description("Keep each copy as high above the ground as its original (default false).")] bool ground = false,
        [Description("A few words for the history.")] string label = "Claude: copied") => OnUi(() =>
    {
        var (start, problem) = StartObjectsChange(ids, pivotX, pivotZ, "Nothing copied");
        if (start == null)
        {
            return problem!;
        }
        var (scene, reads, usable, problems) = start;
        var s = _w.Session!;
        // A linked group's parts go with it (ObjectLinks: a ship); each copy gets ids of its own below.
        var ok = s.WithLinked(usable).Indices;
        if (copies is < 1 or > 100)
        {
            return "copies is 1 to 100.";
        }
        if (dx == 0 && dy == 0 && dz == 0 && turn % 360 == 0)
        {
            return "The copies would be on top of the originals: give dx, dy, dz or turn.";
        }
        var (px, pz) = PivotOf(scene, ok, pivotX, pivotZ);
        var (adds, from, planProblem) = PlanCopies(s, scene, ok, copies, new CopyStep(px, pz, dx, dy, dz, turn, ground), Bounds(scene));
        if (adds == null)
        {
            return planProblem!;
        }
        var made = s.Commit(ClaudesLabel(label), null, [], adds);
        _w.AfterClaudeEdit(made.Count == 1 ? Said.Of("claude.msg.copies.one", made.Count) : Said.Of("claude.msg.copies.many", made.Count));
        GaveIds(scene, reads);
        return ToJson(new { copied = made.Select((c, k) => Moved(scene, from![k], c)), problems, pending = Pending() });
    });
}
