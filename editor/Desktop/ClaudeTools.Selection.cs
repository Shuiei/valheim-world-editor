using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's tools for the objects the user selected in the view (get_selection), and for selecting objects for the user
/// (select_objects).
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>get_selection: the objects selected in the view.</summary>
    [McpServerTool(Name = "get_selection", ReadOnly = true, Title = "What the user selected")]
    [Description("The objects the user has selected in the editor (Select tool): id, name, kind, position, turn. Use it when the user says \"these\", \"the selected trees\", \"this building\".")]
    public Task<string> GetSelection() => OnUi(() =>
    {
        if (_w.Session?.Scene is not { } scene)
        {
            return "No area is open.";
        }
        List<Dictionary<string, object?>> selected;
        int reads;
        lock (scene.Things)
        {
            reads = scene.Reads;
            selected = [.. _w.View.Selected.Where(i => i >= 0 && i < scene.Things.Count && !scene.Things[i].Gone).Select(i => Describe(scene.Things[i], i))];
        }
        if (selected.Count > 0)
        {
            GaveIds(scene, reads);
        }
        return ToJson(selected);
    });

    /// <summary>select_objects: selects objects by id and points the camera at them; changes nothing in the world.</summary>
    [McpServerTool(Name = "select_objects", Title = "Select objects for the user")]
    [Description("Selects objects in the editor (by id), so the user sees which ones are meant, and points the camera at them. Changes nothing in the world.")]
    public Task<string> SelectObjects(
        [Description("Their ids (list_objects).")] int[] ids,
        [Description("Point the camera at them (default: true).")] bool focus = true) => WithCamera(() =>
    {
        if (_w.Session?.Scene is not { } scene)
        {
            return "No area is open.";
        }
        if (StaleIds(scene) is string stale)
        {
            return stale;
        }
        int[] asked = [.. ids.Distinct()];
        List<int> ok;
        List<System.Numerics.Vector3> at;
        // Under the things' lock, as every other reader of them.
        lock (scene.Things)
        {
            ok = [.. asked.Where(i => i >= 0 && i < scene.Things.Count && !scene.Things[i].Gone)];
            at = [.. ok.Select(i => scene.Things[i].Position)];
        }
        _w.View.Select(ok);
        if (focus && ok.Count > 0)
        {
            var mid = at.Aggregate(System.Numerics.Vector3.Zero, (a, b) => a + b) / ok.Count;
            float spread = at.Select(p => System.Numerics.Vector3.Distance(p, mid)).DefaultIfEmpty(0).Max();
            _w.View.Focus(mid, MathF.Max(20, spread * 2.5f));
        }
        return $"Selected {ok.Count}{(ok.Count < asked.Length ? $" ({asked.Length - ok.Count} id(s) were not objects of the area)" : "")}.";
    });
}
