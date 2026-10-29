using Avalonia.Input;

namespace TerrainEditor.Desktop;

/// <summary>What a click on an object does to the selection, by the keys held (see <see cref="SelectHow"/>).</summary>
internal enum PickHow
{
    /// <summary>No key: that object only.</summary>
    Only,
    /// <summary>Ctrl: added when it was not selected, taken out when it was.</summary>
    Toggle,
    /// <summary>Shift: taken out.</summary>
    Remove,
    /// <summary>Ctrl + Shift: the row from the last object clicked to this one, added.</summary>
    Row,
}

/// <summary>What a zone or a box drawn does to the selection, by the keys held (see <see cref="SelectHow"/>).</summary>
internal enum ZoneHow
{
    /// <summary>No key: what is inside becomes the selection.</summary>
    Replace,
    /// <summary>Ctrl: what is inside is added.</summary>
    Add,
    /// <summary>Shift: what is inside is taken out.</summary>
    Remove,
}

/// <summary>
/// The selection keys, as in Blender or a map editor: Ctrl adds (a click toggles), Shift takes out, Ctrl + Shift + click
/// takes a row. Cmd is the Mac's Ctrl.
/// </summary>
internal static class SelectHow
{
    /// <summary>Ctrl (or Cmd) held.</summary>
    /// <param name="keys">The keys held.</param>
    private static bool Ctrl(KeyModifiers keys) => keys.HasFlag(KeyModifiers.Control) || keys.HasFlag(KeyModifiers.Meta);

    /// <summary>What a click does with these keys held.</summary>
    /// <param name="keys">The keys held.</param>
    internal static PickHow Pick(KeyModifiers keys) => (Ctrl(keys), keys.HasFlag(KeyModifiers.Shift)) switch
    {
        (true, true) => PickHow.Row,
        (true, false) => PickHow.Toggle,
        (false, true) => PickHow.Remove,
        _ => PickHow.Only,
    };

    /// <summary>What a zone or a box does with these keys held (Ctrl wins over Shift).</summary>
    /// <param name="keys">The keys held.</param>
    internal static ZoneHow Zone(KeyModifiers keys) =>
        Ctrl(keys) ? ZoneHow.Add : keys.HasFlag(KeyModifiers.Shift) ? ZoneHow.Remove : ZoneHow.Replace;
}
