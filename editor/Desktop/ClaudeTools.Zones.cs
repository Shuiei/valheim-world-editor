using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>
    /// The zones (world column, row) a world rectangle touches, within these zones (inclusive) when given: the range is
    /// cut to them before the zones are listed, so a huge rectangle does not list millions first.
    /// </summary>
    /// <param name="x0">One corner, x.</param>
    /// <param name="z0">One corner, z.</param>
    /// <param name="x1">The opposite corner, x.</param>
    /// <param name="z1">The opposite corner, z.</param>
    /// <param name="within">The first and last zone columns and rows to keep (null: all).</param>
    internal static List<(int X, int Z)> ZonesOf(float x0, float z0, float x1, float z1, (int X0, int Z0, int X1, int Z1)? within = null)
    {
        static int Zone(float v) => (int)Math.Clamp((double)MathF.Floor((v + 32) / 64), int.MinValue / 2, int.MaxValue / 2);
        int fromX = Zone(MathF.Min(x0, x1)), toX = Zone(MathF.Max(x0, x1)), fromZ = Zone(MathF.Min(z0, z1)), toZ = Zone(MathF.Max(z0, z1));
        if (within is { } w)
        {
            (fromX, toX, fromZ, toZ) = (Math.Max(fromX, w.X0), Math.Min(toX, w.X1), Math.Max(fromZ, w.Z0), Math.Min(toZ, w.Z1));
        }
        var zones = new List<(int X, int Z)>();
        for (int zz = fromZ; zz <= toZ; zz++)
        {
            for (int zx = fromX; zx <= toX; zx++)
            {
                zones.Add((zx, zz));
            }
        }
        return zones;
    }

    /// <summary>reset_zones: zones of the open area marked for reset when saving (or unmarked), as one pending step.</summary>
    [McpServerTool(Name = "reset_zones", Title = "Give zones back to the world generator")]
    [Description("Marks the 64 m zones a rectangle of the open area touches for reset, as the Area tool's Reset zones does: when the user saves (or applies live), every tree, rock, ruin and dungeon entrance in them is taken away (the players' buildings too unless keepBuildings), and Valheim generates them anew the next time a player comes near; with ground, their ground edits are undone at once. It throws away what is there: do it only when the user asked for it, and say which zones. cancel: true unmarks them instead. One pending step (Ctrl+Z, or cancel, takes it back).")]
    public Task<string> ResetZones(
        [Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
        [Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
        [Description("Keep the pieces players built (default true).")] bool keepBuildings = true,
        [Description("Also undo the zones' ground edits (default false).")] bool ground = false,
        [Description("Unmark them instead (default false).")] bool cancel = false) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        if (_w.InWorkshop)
        {
            return "Zones are reset in a world, not on the Workshop's plot.";
        }
        var s = _w.Session!;
        var scene = s.Scene;
        var zones = ZonesOf(x0, z0, x1, z1, (scene.X0, scene.Z0, scene.X0 + scene.ZonesX - 1, scene.Z0 + scene.ZonesZ - 1));
        if (zones.Count == 0)
        {
            return "The rectangle touches no zone of the open area (editor_state gives its bounds).";
        }
        string list = string.Join(" · ", zones.Select(z => $"{z.X}, {z.Z}"));
        if (cancel)
        {
            int n = s.UnmarkResets(zones, ClaudesStep(Lang.T("claude.undo.unmarkReset", zones.Count)));
            if (n == 0)
            {
                return "None of those zones was marked for reset.";
            }
            _w.AfterClaudeEdit(Said.Of("claude.msg.unmarked", n));
            return $"Unmarked {n} zone(s). Now pending: {Pending()}";
        }
        s.MarkResets(zones, keepBuildings, ground, ClaudesStep(Lang.T("claude.undo.reset", zones.Count)));
        _w.AfterClaudeEdit(Said.Of("claude.msg.marked", zones.Count));
        return $"Marked {zones.Count} zone(s) for reset when saved ({list}){(keepBuildings ? ", the players' buildings kept" : ", the players' buildings too")}{(ground ? ", their ground edits undone" : "")}. Now pending: {Pending()}";
    });
}
