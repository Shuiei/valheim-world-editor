using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Hover help for every control, in one place: the "tip." texts of the app's catalogue (Core/Lang/en.json, see Lang),
/// in the same wording as the documentation (docs/), so the two stay in step. A control takes its text with
/// .Tip("key"); a row's label shows its control's tip too (Tips.Label).
/// </summary>
public static class Tips
{
    /// <summary>Every tip by key ("area.reset", "place.search"...), in English (the catalogue's "tip." texts).</summary>
    public static IReadOnlyDictionary<string, string> Texts { get; } = Lang.English
        .Where(kv => kv.Key.StartsWith("tip.", StringComparison.Ordinal) && !kv.Key.StartsWith("tip.kind.", StringComparison.Ordinal))
        .ToDictionary(kv => kv.Key["tip.".Length..], kv => kv.Value);

    /// <summary>
    /// The tip for a key, in the language in use; throws <c>KeyNotFoundException</c> for an unknown key, so a typo is
    /// caught at once.
    /// </summary>
    public static string Of(string key) => Texts.ContainsKey(key) ? Lang.T("tip." + key) : throw new KeyNotFoundException("No tip " + key);

    /// <summary>Each control's tip key, as Tip gave it (Claude finds controls by it: ClaudeTools.Controls).</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, string> Keys = [];

    /// <summary>The control explains itself on hover: sets its tooltip to the key's tip and returns it.</summary>
    public static T Tip<T>(this T c, string key) where T : Control
    {
        ToolTip.SetTip(c, Of(key));
        Keys.AddOrUpdate(c, key);
        return c;
    }

    /// <summary>The tip key a control was given (Tip), or null.</summary>
    public static string? KeyOf(Control c) => Keys.TryGetValue(c, out string? k) ? k : null;

    /// <summary>A row's label tells the same as its control (nothing when the control has no tip yet).</summary>
    public static void Label(Control label, Control input)
    {
        if (ToolTip.GetTip(input) is { } t)
        {
            ToolTip.SetTip(label, t);
        }
    }

    /// <summary>The object kinds' tips, as the View panel and the Area chips switch them.</summary>
    public static string Kind(ObjectKind k) => Lang.T("tip.kind." + k);

    /// <summary>An overlay layer's tip (the View panel's overlay switches).</summary>
    public static string Overlay(Overlays.Layer l) => l switch
    {
        Overlays.Layer.Borders => Of("overlay.borders"),
        Overlays.Layer.Markers => Of("overlay.markers"),
        Overlays.Layer.Wards => Of("overlay.wards"),
        Overlays.Layer.Stations => Of("overlay.stations"),
        _ => Of("overlay.flatten"),
    };
}
