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
}
