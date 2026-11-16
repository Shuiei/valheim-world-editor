using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Binding controls: a switch or a list remembers its choice by name, and is set from it when shown.
public sealed partial class Prefs
{
    /// <summary>A switch (CheckBox, ToggleButton): set from what is remembered, remembered when changed.</summary>
    public void Bind(ToggleButton box, string key)
    {
        if (TryGet(key, out bool on))
        {
            box.IsChecked = on;
        }
        box.IsCheckedChanged += (_, _) => Set(key, box.IsChecked == true);
    }

    /// <summary>
    /// A list of texts (ComboBox): the entry remembered by its text, so a list that changes order or grows keeps the
    /// choice; an entry no longer there leaves the list as it is. The text is kept in English (<c>Lang.EnglishOf</c>),
    /// so the choice comes back in whatever language the editor shows; a text no catalogue has (a stamp's name) is
    /// kept as it is.
    /// </summary>
    public void Bind(ComboBox box, string key)
    {
        Restore(box, key);
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is string text)
            {
                Set(key, Lang.EnglishOf(text));
            }
        };
    }

    /// <summary>
    /// Entries whose English changed since a choice of them could be remembered: the entry as it is named now, by the
    /// setting's key and the name it was kept under.
    /// </summary>
    private static readonly Dictionary<(string Key, string Was), string> Renamed = new()
    {
        [("select.zone", "A zone on the ground (all above it)")] = "An outline on the ground (all above it)",
        [("brush.falloff", "Smooth")] = "Soft",
    };

    /// <summary>
    /// Chooses the remembered entry, when the list has it (for lists filled again later), comparing the entries' English
    /// with the English kept (or with the name an entry renamed since has now); true when it did.
    /// </summary>
    public bool Restore(ComboBox box, string key)
    {
        if (TryGet(key, out string? text) && text != null && box.ItemsSource is IEnumerable<object> items)
        {
            if (Renamed.TryGetValue((key, text), out string? now))
            {
                text = now;
            }
            int i = items.Select(o => o is string s ? Lang.EnglishOf(s) : null).ToList().IndexOf(text);
            if (i >= 0)
            {
                box.SelectedIndex = i;
                return true;
            }
        }
        return false;
    }
}
