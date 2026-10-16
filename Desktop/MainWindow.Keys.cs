using Avalonia.Controls;
using Avalonia.Input;

namespace TerrainEditor.Desktop;

// The 3D editor's keys: one table of rules, read in order; the first rule whose keys, modifiers and condition
// match runs, and a rule whose tool turns the key down lets the rules after it try.
public partial class MainWindow
{
    /// <summary>
    /// Which modifier keys a key rule asks for.
    /// </summary>
    private enum KeyMods
    {
        /// <summary>Any, held or not.</summary>
        Any,
        /// <summary>Ctrl (or Cmd) held, with any others.</summary>
        Ctrl,
        /// <summary>Neither Ctrl nor Cmd held; Shift and Alt may be.</summary>
        NoCtrl,
        /// <summary>No modifier key at all.</summary>
        None,
    }

    /// <summary>
    /// A key pressed, as the key rules read it.
    /// </summary>
    /// <param name="Key">The key.</param>
    /// <param name="Mods">The modifier keys held.</param>
    private readonly record struct KeyPress(Key Key, KeyModifiers Mods)
    {
        /// <summary>Ctrl or Cmd held.</summary>
        public bool Ctrl => Mods.HasFlag(KeyModifiers.Control) || Mods.HasFlag(KeyModifiers.Meta);

        /// <summary>Shift held.</summary>
        public bool Shift => Mods.HasFlag(KeyModifiers.Shift);
    }

    /// <summary>
    /// One rule of the key table.
    /// </summary>
    /// <param name="Keys">The keys it is for; null for any key.</param>
    /// <param name="Mods">The modifier keys it asks for.</param>
    /// <param name="When">What must hold besides (the tool, the panels); null for nothing.</param>
    /// <param name="Act">What the key does; false when the tool turns it down and the next rules may try.</param>
    /// <param name="Handles">Whether the key is marked handled once taken (false: it goes on to the rest of the
    /// window).</param>
    private sealed record KeyRule(Key[]? Keys, KeyMods Mods, Func<KeyPress, bool>? When, Func<KeyPress, bool> Act, bool Handles)
    {
        /// <summary>
        /// Whether the key, its modifiers and the condition are the rule's.
        /// </summary>
        public bool Matches(KeyPress p) => (Keys == null || Array.IndexOf(Keys, p.Key) >= 0)
            && Mods switch
            {
                KeyMods.Ctrl => p.Ctrl,
                KeyMods.NoCtrl => !p.Ctrl,
                KeyMods.None => p.Mods == KeyModifiers.None,
                _ => true,
            }
            && (When?.Invoke(p) ?? true);
    }

    /// <summary>
    /// The key table, made on the first key.
    /// </summary>
    private KeyRule[]? _keyRules;

    /// <summary>
    /// A rule that always takes its key and marks it handled.
    /// </summary>
    private static KeyRule Take(Key[]? keys, KeyMods mods, Func<KeyPress, bool>? when, Action<KeyPress> act) =>
        new(keys, mods, when, p => { act(p); return true; }, true);

    /// <summary>
    /// A rule whose tool decides: the key is handled when it takes it, else the next rules try.
    /// </summary>
    private static KeyRule Offer(Key[]? keys, KeyMods mods, Func<KeyPress, bool>? when, Func<KeyPress, bool> act) =>
        new(keys, mods, when, act, true);

    /// <summary>
    /// A rule that acts on its key but leaves it unhandled.
    /// </summary>
    private static KeyRule Pass(Key[]? keys, KeyMods mods, Func<KeyPress, bool>? when, Action<KeyPress> act) =>
        new(keys, mods, when, p => { act(p); return true; }, false);

    /// <summary>
    /// The ten keys from <paramref name="first"/> on (the digits of the top row or of the number pad).
    /// </summary>
    private static Key[] Digits(Key first) => [.. Enumerable.Range(0, 10).Select(i => first + i)];

    /// <summary>
    /// The 3D editor's keys, in the order they are tried: the first rule that matches wins.
    /// </summary>
    private KeyRule[] KeyRules()
    {
        bool InMode(ToolMode mode) => Tools.Mode == mode;
        bool Pasting(KeyPress _) => InMode(ToolMode.Paste);
        return
        [
            Take([Key.Escape], KeyMods.NoCtrl, _ => View.PickObjectOnce != null, _ =>
            {
                View.PickObjectOnce = null;
                MessageText.Text = "Picking cancelled.";
            }),
            // The right-hand panels, as in the web editor: V the View panel, L the history, ? (or F3) the help. ? is Shift
            // and the / key (OemQuestion and Oem2 are that one key): / alone is not it.
            Take([Key.F3], KeyMods.NoCtrl, null, _ => ShowRight(HelpCard.IsVisible ? null : HelpCard)),
            Take([Key.OemQuestion], KeyMods.NoCtrl, p => p.Shift, _ => ShowRight(HelpCard.IsVisible ? null : HelpCard)),
            Take([Key.V], KeyMods.None, null, _ => ShowRight(ViewPanelCard.IsVisible ? null : ViewPanelCard)),
            Take([Key.L], KeyMods.None, null, _ => ShowRight(History.Card.IsVisible ? null : History.Card)),
            Take([Key.Z], KeyMods.Ctrl, null, p => _ = p.Shift ? Redo() : Undo()),
            Take([Key.Y], KeyMods.Ctrl, null, p => _ = Redo()),
            Take([Key.C], KeyMods.Ctrl, _ => Tools.Mode is ToolMode.Area or ToolMode.Select, _ => Copy()),
            Take([Key.X], KeyMods.Ctrl, _ => Tools.Mode is ToolMode.Area or ToolMode.Select, _ => Cut()),
            Take([Key.V], KeyMods.Ctrl, null, _ => StartPaste()),
            // Pasting: R turns a quarter, F mirrors, , and . turn by 1° (Shift: 15°), Esc ends it.
            Take([Key.R], KeyMods.NoCtrl, Pasting, _ => View.Paste.TurnBy(90)),
            Take([Key.F], KeyMods.NoCtrl, Pasting, _ =>
            {
                View.Paste.Mirror = !View.Paste.Mirror;
                View.Paste.Notify();
            }),
            Take([Key.OemComma, Key.OemPeriod], KeyMods.NoCtrl, Pasting, p => TurnPaste(p.Key, p.Shift)),
            Take([Key.Escape], KeyMods.NoCtrl, Pasting, _ => Tools.ChooseMode(ToolMode.Area)),
            Take([Key.S], KeyMods.Ctrl, null, p => _ = Save()),
            Take([Key.OemOpenBrackets, Key.OemCloseBrackets], KeyMods.NoCtrl, null, p => Tools.ResizeBrush(p.Key == Key.OemOpenBrackets ? -1 : 1)),
            Take([Key.Escape], KeyMods.NoCtrl, _ => Inspector.IsOpen, _ => Inspector.Close()),
            Take([Key.I], KeyMods.NoCtrl, _ => Tools.SelectMode && View.Selected.Count == 1, _ => Inspect()),
            // The Place and Select tools' own keys (any modifiers): what they turn down goes on.
            Offer(null, KeyMods.Any, _ => InMode(ToolMode.Place), p => PlaceInput.Key(p.Key, p.Shift, p.Ctrl)),
            Offer(null, KeyMods.Any, _ => Tools.SelectMode, p => View.SelectTool.Key(p.Key, p.Shift)),
            Take([Key.Enter, Key.Return], KeyMods.NoCtrl, _ => InMode(ToolMode.Area), p =>
            {
                if (!View.Area.CloseWithEnter())
                {
                    _ = AreaPanel.Apply();
                }
            }),
            Take([Key.Back], KeyMods.NoCtrl, _ => InMode(ToolMode.Area), _ => View.Area.RemoveLast()),
            Take([Key.Escape], KeyMods.NoCtrl, _ => InMode(ToolMode.Area) && View.Area.Points.Count > 0, _ => View.Area.Clear()),
            Take([Key.Enter, Key.Return], KeyMods.NoCtrl, _ => InMode(ToolMode.Path), _ => ApplyPath()),
            Take([Key.Back], KeyMods.NoCtrl, _ => InMode(ToolMode.Path), _ => View.Path.RemoveLast()),
            Take([Key.Escape], KeyMods.NoCtrl, _ => InMode(ToolMode.Path) && View.Path.Points.Count > 0, _ => View.Path.Clear()),
            Offer([Key.Delete], KeyMods.NoCtrl, _ => InMode(ToolMode.Dungeon), _ => View.Dungeon.Delete()),
            Take([Key.R], KeyMods.NoCtrl, _ => InMode(ToolMode.Dungeon), _ => View.Dungeon.Turn()),
            Take([Key.Escape], KeyMods.NoCtrl, _ => InMode(ToolMode.Measure) && View.Tape.A != null, _ => View.Tape.Clear()),
            // The rail's keys: the tool panel acts on them, and they stay unhandled.
            Pass([Key.Escape], KeyMods.NoCtrl, null, _ => Tools.Key("Escape")),
            Pass([Key.E, Key.M, Key.G, Key.P, Key.B, Key.O, Key.T, Key.H], KeyMods.NoCtrl, null, p => Tools.Key(p.Key.ToString())),
            Pass(Digits(Key.D0), KeyMods.NoCtrl, null, p => Tools.Key((p.Key - Key.D0).ToString())),
            Pass(Digits(Key.NumPad0), KeyMods.NoCtrl, null, p => Tools.Key((p.Key - Key.NumPad0).ToString())),
        ];
    }

    /// <summary>
    /// The 3D editor's keys (shortcuts for the tools, the panels, undo, save and the like, see <see cref="KeyRules"/>);
    /// none while typing in a box or away from the editor.
    /// </summary>
    private void OnKey(object? sender, KeyEventArgs e)
    {
        // Typing in a box: its keys are its own. The keys below are the 3D editor's: on the start page or
        // the map they would act on the area left open behind it (an undo pushed live, unseen); under the busy veil
        // (saving, reading the world again) they would wait on it or do it twice.
        if (e.Source is TextBox || _pages.Content != _editorPage || IsBusy)
        {
            return;
        }
        var press = new KeyPress(e.Key, e.KeyModifiers);
        foreach (var rule in _keyRules ??= KeyRules())
        {
            if (rule.Matches(press) && rule.Act(press))
            {
                if (rule.Handles)
                {
                    e.Handled = true;
                }
                return;
            }
        }
    }
}
