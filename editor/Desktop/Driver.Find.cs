using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Finds the controls the driver's commands act on, by their text (as shown, or its English: the commands and the scenes
/// name controls in English whatever the language) and their state.
/// </summary>
public static partial class Driver
{
    /// <summary>The first visible, enabled control of that type in the window or its open dialogs (the newest first).</summary>
    private static T? Find<T>(MainWindow w, Func<T, bool> match) where T : Avalonia.Controls.Control
    {
        IEnumerable<Avalonia.Controls.Window> windows = new Avalonia.Controls.Window[] { w }.Concat(w.OwnedWindows);
        foreach (var win in windows.Reverse())
        {
            foreach (var c in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(win).OfType<T>())
            {
                if (c.IsEffectivelyVisible && c.IsEffectivelyEnabled && match(c))
                {
                    return c;
                }
            }
        }
        return null;
    }

    /// <summary>A control's own words: its text content, or every text inside it.</summary>
    internal static string TextOf(Avalonia.Controls.ContentControl c) => c.Content is string s ? s.Trim()
        : string.Join(" ", Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(c).OfType<Avalonia.Controls.TextBlock>().Select(t => t.Text?.Trim()).Where(t => !string.IsNullOrEmpty(t))).Trim();

    /// <summary>
    /// A control's own words in English: each of its texts taken back to its English (<c>Lang.EnglishOf</c>, or
    /// <c>Lang.EnglishOfFilled</c> for one made with values: "Niveau 2" is "Level 2"), so a command names a control by its
    /// English words whatever the language the window is shown in.
    /// </summary>
    internal static string EnglishTextOf(Avalonia.Controls.ContentControl c) => c.Content is string s ? English(s.Trim())
        : string.Join(" ", Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(c).OfType<Avalonia.Controls.TextBlock>().Select(t => t.Text?.Trim()).Where(t => !string.IsNullOrEmpty(t)).Select(t => English(t!))).Trim();

    /// <summary>A text shown, in English: a catalogue text's English, one made with values with them, else as it is.</summary>
    private static string English(string shown) => Lang.EnglishOf(shown) is var en && en != shown ? en : Lang.EnglishOfFilled(shown) ?? shown;

    /// <summary>
    /// Whether a text shown is <paramref name="name"/> (any case), as shown or in English, a text made with values too
    /// ("150 graines" is "150 seeds").
    /// </summary>
    internal static bool Says(string? shown, string name) => shown != null
        && (string.Equals(shown.Trim(), name, StringComparison.OrdinalIgnoreCase) || string.Equals(English(shown.Trim()), name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether a text shown begins with words whose English is <paramref name="name"/> (any case), followed by more after a
    /// space: a label with a value put after it by a text of places only ("Deine Gebäude 247", "{0} {1}", is "Your
    /// buildings" and a count).
    /// </summary>
    internal static bool BeginsAs(string shown, string name)
    {
        for (int i = shown.IndexOf(' ', StringComparison.Ordinal); i > 0; i = shown.IndexOf(' ', i + 1))
        {
            if (string.Equals(English(shown[..i].Trim()), name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Whether a control's words are <paramref name="name"/> (any case), as shown or in English.</summary>
    internal static bool Says(Avalonia.Controls.ContentControl c, string name) =>
        string.Equals(TextOf(c), name, StringComparison.OrdinalIgnoreCase) || string.Equals(EnglishTextOf(c), name, StringComparison.OrdinalIgnoreCase);

    /// <summary>The index of a list's entry (a drop-down's or a list box's) by its text (any case, as shown or in English), -1 when none.</summary>
    internal static int IndexOf(Avalonia.Controls.ItemsControl box, string text)
    {
        int i = 0;
        foreach (object? item in box.Items)
        {
            string? name = item is Avalonia.Controls.ContentControl cc ? cc.Content?.ToString() : item?.ToString();
            if (Says(name, text))
            {
                return i;
            }
            i++;
        }
        return -1;
    }
}
