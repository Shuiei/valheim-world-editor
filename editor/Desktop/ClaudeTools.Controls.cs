using System.Text.RegularExpressions;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>A control as Claude sees it.</summary>
    /// <param name="Id">How Claude names it: its tip key (view.gameLook, workshop.cut…), else ~ and its English label; #2… when shared.</param>
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
        "select.delete", "select.forget", "brush.forgetStamp", "area.export",
        "blueprints.details", "blueprints.vbuild", "blueprints.vbuildOld", "blueprints.toHomestead",
        "blueprints.delete", "blueprints.deleteOld",
    ];

    /// <summary>
    /// English labels of controls without a tip key that Claude never sets (the same kinds of actions); a label in
    /// another language is matched by its English (Lang.EnglishOf).
    /// </summary>
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
        if (Tips.KeyOf(c.Control) == null && c.Kind == "button" && BlockedLabel().IsMatch(Lang.EnglishOf(c.Label)))
        {
            return "it saves, discards or writes or deletes the user's files: the user's to do";
        }
        // The Script tool's Run is run_script by another door: the same leave, said first (as run_script does), since
        // turning Auto off would not be enough.
        if (Tips.KeyOf(c.Control) == "script.run" && !_w.Settings.ClaudeScripts)
        {
            return "the user has not allowed Claude to run scripts (Settings → Claude → Claude may run scripts)";
        }
        // Like every other editing tool (CannotEdit): a tool's Apply, an Area action or a script run would go to the
        // game at once. Only the View panel's controls, which change nothing in the world, stay.
        if (LiveWithAuto && !(Tips.KeyOf(c.Control) is string view && view.StartsWith("view.", StringComparison.Ordinal)))
        {
            return "the open world is live with Auto on, so what it changes would go to the game at once; only the View panel's controls can be used. Ask the user to turn Auto off";
        }
        return null;
    }

    /// <summary>
    /// Presses a button as a pointer would, through its automation peer: a toggle changes first (so its handlers see
    /// the new value), then its Click handlers and its Command run. Raising the Click event alone would skip the
    /// toggle and the Command.
    /// </summary>
    private static void Press(Button b) => new ButtonAutomationPeer(b).Invoke();

    /// <summary>
    /// True while Claude is setting a control (Set): the steps the control's handlers make, at once or after an
    /// await, are labelled "Claude: …" by EditSession, as every other tool's are, so History and Take back Claude's
    /// changes know them. It flows with the execution context, so a handler's continuations inherit it and the
    /// user's own actions meanwhile do not.
    /// </summary>
    internal static readonly AsyncLocal<bool> Acting = new();
}
