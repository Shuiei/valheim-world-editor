using System.ComponentModel;
using Avalonia.Controls;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>list_controls: the window's controls with their ids, values and options.</summary>
    [McpServerTool(Name = "list_controls", ReadOnly = true, Title = "The editor's controls")]
    [Description("Every control the editor shows now, as the user sees them: the View panel's switches (kinds of objects, game look, see-through, water, slope, contours…), the Workshop's Cut slider and Support check, each tool's panel (brush size and strength, Place settings, Area actions, Mask, Path, Script, Dungeon…), the top bar, the rail of tools and every button: id, kind (switch, toggle, radio, dropdown, slider, number, text, button), label, value, options (a dropdown's), range (a slider's or number's), whether it is shown and enabled, what it does (its tip), and why it is blocked when Claude may not use it. Filter by words of the id, label or tip. Set them with set_control. Panels the user has not opened are listed too (shown: false); their switches work while hidden.")]
    public Task<string> ListControls(
        [Description("Words to find in the id, label or tip (optional), e.g. \"view\", \"cut\", \"brush\".")] string? query = null,
        [Description("Only the controls shown now (default false: hidden panels too).")] bool shownOnly = false,
        [Description("At most this many (default 300).")] int limit = 300) => OnUi(() =>
    {
        string[] words = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var found = ControlsOf(_w).Where(c =>
        {
            string tip = ToolTip.GetTip(c.Control) as string ?? "";
            return (!shownOnly || c.Control.IsEffectivelyVisible)
                && words.All(w => c.Id.Contains(w, StringComparison.OrdinalIgnoreCase) || c.Label.Contains(w, StringComparison.OrdinalIgnoreCase) || tip.Contains(w, StringComparison.OrdinalIgnoreCase));
        }).ToList();
        return ToJson(new
        {
            count = found.Count,
            controls = found.Take(Math.Clamp(limit, 1, 2000)).Select(c => new
            {
                id = c.Id,
                kind = c.Kind,
                label = c.Label,
                value = ValueOf(c.Control),
                options = c.Control is ComboBox b ? OptionsOf(b) : null,
                range = c.Control switch { Slider s => (double[]?)[s.Minimum, s.Maximum], NumericUpDown u => [(double)u.Minimum, (double)u.Maximum], _ => null },
                shown = c.Control.IsEffectivelyVisible,
                enabled = c.Control.IsEffectivelyEnabled,
                tip = ToolTip.GetTip(c.Control) as string,
                blocked = Blocked(c),
            }),
        });
    });

    /// <summary>set_control: a control set as the user would.</summary>
    [McpServerTool(Name = "set_control", Title = "Set one of the editor's controls")]
    [Description("Sets one of the editor's controls (list_controls gives their ids) as the user would, so the editor does what the control does: a switch or toggle true or false (e.g. a View switch, see-through, Support check), a dropdown's option (its text, or its number from 0), a slider's or a number box's value (e.g. workshop.cut 12), a text box's text (enter: true also presses Enter, for search boxes), a button: click (a tool of the rail, a panel's action, Apply of a tool…). Answers with the control's value after and the editor's message. Saving, applying live, discarding, reloading, writing or deleting the user's files and connecting to the live game are refused; while the world is live with Auto on, every control but the View panel's is.")]
    public Task<string> SetControl(
        [Description("The control's id (list_controls).")] string id,
        [Description("The value: true/false, an option, a number, a text, or click.")] string value,
        [Description("For a text box: also press Enter after typing (default false).")] bool enter = false) => OnUi(() =>
    {
        var c = ControlsOf(_w).FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (c == null)
        {
            return $"No control {id} now (list_controls gives the ids; a panel's controls exist once the editor shows that page).";
        }
        if (Set(c, value, enter) is string problem)
        {
            return problem;
        }
        return ToJson(new { id = c.Id, value = ValueOf(c.Control), shown = c.Control.IsEffectivelyVisible, message = _w.LastMessageEn });
    });
}
