namespace TerrainEditor.Desktop;

/// <summary>
/// What the editor shows, and why a world cannot be left yet, as Claude reads it (editor_state and the other answers).
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>Why a world cannot be left now (changes the user has not saved), or null.</summary>
    private string? Unsaved()
    {
        if (_w.World is { } w && w.Pending is not (0, 0, 0, 0))
        {
            return $"The open world has changes not {(w.IsLive ? "applied" : "saved")} ({w.Pending.Zones} zone(s), {w.Pending.Added} added, {w.Pending.Deleted} deleted): ask the user to {(w.IsLive ? "apply" : "save")} or discard them first.";
        }
        if (_w.WorkshopDirty)
        {
            return "The Workshop's building is not saved as a blueprint: ask the user to save it (or to leave the Workshop) first.";
        }
        return null;
    }

    /// <summary>
    /// What the window shows, for editor_state and other answers: the page (start, map, area or workshop), the world,
    /// the area's bounds in world metres, what is pending, whether undo is possible, and the last message.
    /// </summary>
    internal static object StateOf(MainWindow w)
    {
        var s = w.Session;
        var scene = s?.Scene;
        string page = w.MapShown ? "map" : w.InWorkshop ? "workshop" : s != null ? "area" : "start";
        return new
        {
            page,
            world = w.World is { } world ? new { name = world.World.Name, folder = world.World.Directory, live = world.IsLive, autoApply = world.IsLive && w.Settings.AutoApply } : null,
            area = scene == null ? null : new
            {
                name = scene.Name,
                minX = scene.OriginX,
                minZ = scene.OriginZ,
                maxX = scene.OriginX + scene.ZonesX * 64f,
                maxZ = scene.OriginZ + scene.ZonesZ * 64f,
                water = scene.Water,
                objects = scene.Things.Count(t => !t.Gone),
            },
            pending = w.World is { } pw ? new { zones = pw.Pending.Zones, added = pw.Pending.Added, deleted = pw.Pending.Deleted } : null,
            canUndo = s?.CanUndo ?? false,
            message = w.LastMessageEn,
        };
    }
}
