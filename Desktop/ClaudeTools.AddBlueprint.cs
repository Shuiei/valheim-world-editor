using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's adding of a library blueprint to the Workshop's plot or to the open area (add_blueprint).
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>
    /// add_blueprint: a library blueprint onto the Workshop's plot, with what is there, or into the open area of a
    /// world, its lowest piece on the ground at its middle (or at y); taken back when a piece would leave the area.
    /// </summary>
    [McpServerTool(Name = "add_blueprint", Title = "Add a blueprint")]
    [Description("Adds a blueprint of the library (list_blueprints) to the Workshop's plot or to the open area of a world, with what is there: centred on x, z (default: the plot's or the area's middle), turned by turn degrees (as yaw: 90 turns north to east), its lowest piece at height y (default: the ground at its middle; level the ground first with flatten for a building on a slope). One pending step; refused when a piece would be outside the area. Kinds the game does not know (mods) are left out and named.")]
    public Task<string> AddBlueprint(
        [Description("The blueprint's name (list_blueprints).")] string blueprint,
        [Description("World x of its middle (optional).")] float? x = null,
        [Description("World z of its middle (optional).")] float? z = null,
        [Description("Degrees to turn it round its middle (default 0).")] float turn = 0,
        [Description("In a world: the height of its lowest piece (optional: the ground at its middle).")] float? y = null) =>
        WithLibraryFile(blueprint, found => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        if (found is not string path)
        {
            return $"No blueprint \"{blueprint}\" in the library (list_blueprints).";
        }
        if (LoneCoordinate(x, z) is string lone)
        {
            return lone;
        }
        string label = ClaudesStep(Lang.T("claude.undo.added", blueprint));
        if (_w.InWorkshop)
        {
            _w.AddToWorkshop(path, x is float ax && z is float az ? new System.Numerics.Vector2(ax, az) : null, turn, label);
            return _w.LastMessageEn + " Now pending: " + Pending();
        }
        var s = _w.Session!;
        var scene = s.Scene;
        int reads = scene.Reads;
        float cx = x ?? scene.Cx, cz = z ?? scene.Cz;
        var (minX, minZ, maxX, maxZ) = Bounds(scene);
        if (cx < minX || cx > maxX || cz < minZ || cz > maxZ)
        {
            return $"({cx}, {cz}) is outside the open area (x {minX} to {maxX}, z {minZ} to {maxZ}): open_area there first.";
        }
        // Worked out before anything is put down: a refusal leaves the history alone (a step committed and
        // undone would clear what the user could redo and leave the refused blueprint there instead).
        Workshop.Planned plan;
        try
        {
            plan = Workshop.Plan(s, path, null, new System.Numerics.Vector2(cx, cz), turn, y ?? GroundAt(scene, cx, cz));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or JsonException)
        {
            Log.CaughtBrief(ex, "reading the blueprint Claude asked to place");
            return "Could not read that blueprint: " + ex.Message;
        }
        if (plan.Adds.Count == 0)
        {
            return "Nothing added: the blueprint has no piece this world can have.";
        }
        int outside = plan.Adds.Count(a => a.Object.Position.X < minX || a.Object.Position.X > maxX || a.Object.Position.Z < minZ || a.Object.Position.Z > maxZ);
        if (outside > 0)
        {
            return $"Nothing added: {outside} piece(s) would be outside the area (x {minX} to {maxX}, z {minZ} to {maxZ}). Open a larger area (open_area) or put it further in.";
        }
        int before = scene.Things.Count;
        var added = Workshop.Put(s, plan, label);
        GaveIds(scene, reads);
        _w.AfterClaudeEdit(Said.Of("claude.msg.blueprint", blueprint, added.Placed));
        return $"Added {added.Placed} piece(s) of \"{blueprint}\", ids {before} to {scene.Things.Count - 1}."
            + (added.Unknown.Count > 0 ? $" Left out (unknown kinds): {string.Join(", ", added.Unknown.Take(10))}." : "")
            + " Now pending: " + Pending();
    }));
}
