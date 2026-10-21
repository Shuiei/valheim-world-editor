using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's undo and redo of the open area's changes (undo, redo), as Ctrl+Z and Ctrl+Y, and the changes pending in words.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>undo: the last change of the area, anyone's, as Ctrl+Z.</summary>
    [McpServerTool(Name = "undo", Title = "Undo")]
    [Description("Takes back the last change of the area (anyone's: Claude's or the user's), as Ctrl+Z.")]
    public Task<string> Undo() => OnUi(async () =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        if (_w.Session is not { CanUndo: true })
        {
            return "Nothing to undo.";
        }
        await _w.Undo();
        return "Undone. Now pending: " + Pending();
    });

    /// <summary>redo: the last change undone, as Ctrl+Y.</summary>
    [McpServerTool(Name = "redo", Title = "Redo")]
    [Description("Puts back the last change undone, as Ctrl+Y.")]
    public Task<string> Redo() => OnUi(async () =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        if (_w.Session is not { CanRedo: true })
        {
            return "Nothing to redo.";
        }
        await _w.Redo();
        return "Redone. Now pending: " + Pending();
    });

    /// <summary>The changes not saved yet, in words.</summary>
    private string Pending() => _w.World is { } w
        ? w.Pending is (0, 0, 0, 0) ? "nothing." : $"{w.Pending.Zones} zone(s) of ground, {w.Pending.Added} object(s) added, {w.Pending.Deleted} taken away."
        : _w.Session is { } s ? $"{Workshop.Pieces(s.Scene)} building piece(s) on the plot, not saved as a blueprint." : "nothing.";
}
