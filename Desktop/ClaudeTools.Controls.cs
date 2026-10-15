using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's tools for the editor's own controls: every switch, dropdown, slider, number and text box and button of the
/// window (the View panel's switches, the Workshop's Cut and Support check, every tool panel's settings…), listed with
/// their values and set as the user would, so the window's own handlers run. Controls that save, apply live, discard,
/// reload, write or delete the user's files, or connect to the live game, are never set; while the world is live with
/// Auto on, only the View panel's are.
/// </summary>
public sealed partial class ClaudeTools
{
    // ---- The window's controls.

    /// <summary>A control as Claude sees it.</summary>
    /// <param name="Id">How Claude names it: its tip key (view.gameLook, workshop.cut…), else ~ and its label; #2… when shared.</param>
    /// <param name="Kind">switch, toggle, radio, dropdown, slider, number, text or button.</param>
    /// <param name="Label">Its words: its content, placeholder, or the label beside it.</param>
    /// <param name="Control">The control.</param>
    internal sealed record Ctl(string Id, string Kind, string Label, Control Control);

    /// <summary>Tip keys of controls Claude never sets: saving, applying live, discarding, reloading, files, the live game.</summary>
    internal static readonly HashSet<string> BlockedKeys =
    [
        "top.save", "top.apply", "top.discard", "top.reload", "top.auto",
        "map.save", "map.apply", "map.discard", "map.reload",
        "area.saveBlueprint", "place.savePreset", "place.deletePreset", "script.save",
        "blueprints.import", "blueprints.getHomestead", "build.add", "build.open",
        "start.game", "start.server", "start.editLive", "start.home",
        "start.connect", "start.connectUrl", "start.savedConnect", "start.savedForget",
        "dungeon.gen.openOnPlot", "dungeon.gen.addToPlot", "dungeon.gen.workshop",
        "select.forget", "brush.forgetStamp", "area.export",
    ];

    /// <summary>Labels of controls without a tip key that Claude never sets (the same kinds of actions).</summary>
    [GeneratedRegex(@"^(save|apply live|discard|reload|delete|details|to homestead|\.vbuild|import|get homestead|forget)", RegexOptions.IgnoreCase)]
    private static partial Regex BlockedLabel();

    /// <summary>Why Claude may not set this control, or null when it may.</summary>
    internal string? Blocked(Ctl c)
    {
        if (ReferenceEquals(c.Control, _w.SaveButton) || ReferenceEquals(c.Control, _w.DiscardButton))
        {
            return "saving, applying live, saving a blueprint and discarding are the user's";
        }
        if (Tips.KeyOf(c.Control) is string k && (BlockedKeys.Contains(k) || k.StartsWith("settings.", StringComparison.Ordinal)))
        {
            return "it saves, applies, discards, reloads, writes the user's files or connects to the live game: the user's to do";
        }
        if (Tips.KeyOf(c.Control) == null && c.Kind == "button" && BlockedLabel().IsMatch(c.Label))
        {
            return "it saves, discards or writes or deletes the user's files: the user's to do";
        }
        // Like every other editing tool (CannotEdit): a tool's Apply, an Area action or a script run would go to the
        // game at once. Only the View panel's controls, which change nothing in the world, stay.
        if (LiveWithAuto && !(Tips.KeyOf(c.Control) is string view && view.StartsWith("view.", StringComparison.Ordinal)))
        {
            return "the open world is live with Auto on, so what it changes would go to the game at once; only the View panel's controls can be used. Ask the user to turn Auto off";
        }
        return null;
    }

    /// <summary>A control's kind as Claude names it, or null when it is not one Claude sets.</summary>
    internal static string? KindOf(Control c) => c switch
    {
        CheckBox => "switch",
        RadioButton => "radio",
        ToggleButton => "toggle",
        Button => "button",
        ComboBox => "dropdown",
        Slider => "slider",
        NumericUpDown => "number",
        TextBox => "text",
        _ => null,
    };

    /// <summary>Text shown by a control's content (a string, a text block, or the first text in a panel).</summary>
    private static string? TextOf(object? content) => content switch
    {
        string s => s,
        TextBlock t => t.Text,
        Control c => c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)),
        _ => null,
    };

    /// <summary>
    /// A control's words: its content or placeholder, else the text block just before it in its panel, else its tip's
    /// first sentence.
    /// </summary>
    internal static string LabelOf(Control c)
    {
        string? text = c switch
        {
            ContentControl cc => TextOf(cc.Content),
            TextBox t => t.PlaceholderText,
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(text) && c.Parent is Panel p)
        {
            int i = p.Children.IndexOf(c);
            text = p.Children.Take(Math.Max(0, i)).Reverse().OfType<TextBlock>().Select(t => t.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        }
        if (string.IsNullOrWhiteSpace(text) && ToolTip.GetTip(c) is string tip)
        {
            text = tip.Split(". ")[0];
        }
        return (text ?? "").Trim();
    }

    /// <summary>The window's controls Claude can set, in the window's order, with ids (stable while the window's layout is).</summary>
    internal static List<Ctl> ControlsOf(Control root)
    {
        var list = new List<Ctl>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var c in root.GetLogicalDescendants().OfType<Control>())
        {
            if (KindOf(c) is not string kind)
            {
                continue;
            }
            string label = LabelOf(c);
            string id = Tips.KeyOf(c) ?? "~" + (label.Length > 0 ? Regex.Replace(label.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-') : kind);
            int k = seen[id] = seen.GetValueOrDefault(id) + 1;
            list.Add(new Ctl(k == 1 ? id : $"{id}#{k}", kind, label, c));
        }
        return list;
    }

    /// <summary>The options a dropdown offers, as text.</summary>
    private static List<string> OptionsOf(ComboBox b) =>
        [.. b.Items.Cast<object?>().Select(i => i is ComboBoxItem ci ? TextOf(ci.Content) ?? "" : TextOf(i) ?? i?.ToString() ?? "")];

    /// <summary>A control's value as Claude reads it.</summary>
    internal static object? ValueOf(Control c) => c switch
    {
        ToggleButton t when t is CheckBox or RadioButton || t.IsChecked != null => t.IsChecked,
        ComboBox b => b.SelectedIndex >= 0 && b.SelectedIndex < OptionsOf(b).Count ? OptionsOf(b)[b.SelectedIndex] : null,
        Slider s => Math.Round(s.Value, 3),
        NumericUpDown u => u.Value,
        TextBox t => t.Text is { Length: > 300 } text ? text[..300] + "…" : t.Text,
        _ => null,
    };

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

    /// <summary>
    /// Sets a control as the user would (on the window's thread, so its handlers run): a switch on or off, a dropdown's
    /// option, a slider's or number's value, a text, a button pressed. Null when done, else why not.
    /// </summary>
    internal string? Set(Ctl c, string value, bool enter)
    {
        if (Blocked(c) is string why)
        {
            return $"{c.Id} is not Claude's to use: {why}.";
        }
        if (!c.Control.IsEffectivelyEnabled)
        {
            return $"{c.Id} is greyed out now (the editor does not allow it in this state).";
        }
        static bool? Bool(string v) => v.Trim().ToLowerInvariant() switch { "true" or "on" or "yes" or "1" or "checked" => true, "false" or "off" or "no" or "0" or "unchecked" => false, _ => null };
        static double? Num(string v) => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : null;
        switch (c.Control)
        {
            case ToggleButton t when value.Trim().Equals("click", StringComparison.OrdinalIgnoreCase):
                t.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                return null;
            case ToggleButton t:
                if (Bool(value) is not bool on)
                {
                    return $"{c.Id} is a {c.Kind}: give true or false (or click).";
                }
                t.IsChecked = on;
                return null;
            case Button b:
                if (!value.Trim().Equals("click", StringComparison.OrdinalIgnoreCase) && value.Trim().Length > 0)
                {
                    return $"{c.Id} is a button: give click.";
                }
                b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                return null;
            case ComboBox box:
            {
                var options = OptionsOf(box);
                int i = options.FindIndex(o => o.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
                if (i < 0)
                {
                    i = options.FindIndex(o => o.StartsWith(value.Trim(), StringComparison.OrdinalIgnoreCase));
                }
                if (i < 0 && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 0 && n < options.Count)
                {
                    i = n;
                }
                if (i < 0)
                {
                    return $"{c.Id} has no option \"{value}\": {string.Join(", ", options)}.";
                }
                box.SelectedIndex = i;
                return null;
            }
            case Slider s:
                if (Num(value) is not double v)
                {
                    return $"{c.Id} is a slider from {s.Minimum} to {s.Maximum}: give a number.";
                }
                s.Value = Math.Clamp(v, s.Minimum, s.Maximum);
                return null;
            case NumericUpDown u:
                if (Num(value) is not double d)
                {
                    return $"{c.Id} takes a number from {u.Minimum} to {u.Maximum}.";
                }
                u.Value = Math.Clamp((decimal)d, u.Minimum, u.Maximum);
                return null;
            case TextBox t:
                t.Text = value;
                if (enter)
                {
                    t.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
                }
                return null;
            default:
                return $"{c.Id} cannot be set.";
        }
    }

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
        return ToJson(new { id = c.Id, value = ValueOf(c.Control), shown = c.Control.IsEffectivelyVisible, message = _w.MessageText.Text ?? "" });
    });
}
