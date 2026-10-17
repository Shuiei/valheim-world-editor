
namespace TerrainEditor.Desktop;

/// <summary>
/// Finds the controls the driver's commands act on, by their text and their state.
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

    /// <summary>The index of a list's entry by its text (any case), -1 when none.</summary>
    internal static int IndexOf(Avalonia.Controls.ComboBox box, string text)
    {
        int i = 0;
        foreach (object? item in box.Items)
        {
            string? name = item is Avalonia.Controls.ContentControl cc ? cc.Content?.ToString() : item?.ToString();
            if (string.Equals(name?.Trim(), text, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
            i++;
        }
        return -1;
    }
}
