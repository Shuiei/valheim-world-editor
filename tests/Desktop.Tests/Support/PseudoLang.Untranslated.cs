using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace TerrainEditor.Desktop.Tests;

internal sealed partial class PseudoLang
{
    /// <summary>
    /// Every text <paramref name="root"/> and the controls under it show that did not come from the catalogue: text
    /// blocks (their runs), string contents and headers, placeholders, a list's string entries, string tooltips (and
    /// a tooltip's controls) and a window's title. The user's own typing in a text box is not looked at. A text passes
    /// when nothing outside its ⟦…⟧ has a letter, once numbers with their units and <paramref name="data"/> are taken
    /// out.
    /// </summary>
    /// <param name="root">The window or panel.</param>
    /// <param name="data">Texts that are data, shown as they are in every language: world, prefab and player names.</param>
    /// <returns>The texts found, in the order of the controls.</returns>
    public static List<string> Untranslated(Control root, params string[] data)
    {
        var found = new List<string>();
        var seen = new HashSet<Control>();
        void Check(object? shown)
        {
            if (shown is string text && !Translated(text, data))
            {
                found.Add(text);
            }
        }
        void Walk(Control c)
        {
            if (!seen.Add(c))
            {
                return;
            }
            if (c is TextBlock t)
            {
                if (t.Inlines is { Count: > 0 } inlines)
                {
                    RunTexts(inlines, Check, Walk);
                }
                else
                {
                    Check(t.Text);
                }
            }
            CheckControlTexts(c, Check);
            object? tip = ToolTip.GetTip(c);
            if (tip is Control tipControl)
            {
                Walk(tipControl);
            }
            else
            {
                Check(tip);
            }
            foreach (var child in c.GetLogicalChildren().OfType<Control>())
            {
                Walk(child);
            }
        }
        Walk(root);
        return found;
    }
}
