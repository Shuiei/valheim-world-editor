using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace TerrainEditor.Desktop.Tests;

internal sealed partial class PseudoLang
{
    /// <summary>
    /// The texts a control shows in its own right, not its tip or its children: a window's title, a box's placeholder, a
    /// content control's content, headers and an items control's items.
    /// </summary>
    /// <param name="c">The control.</param>
    /// <param name="check">Checks one shown text (or object).</param>
    private static void CheckControlTexts(Control c, Action<object?> check)
    {
        if (c is Window w)
        {
            check(w.Title);
        }
        if (c is TextBox box)
        {
            check(box.PlaceholderText);
        }
        if (c is ComboBox combo)
        {
            check(combo.PlaceholderText);
        }
        if (c is ContentControl content)
        {
            check(content.Content);
        }
        if (c is HeaderedContentControl headed)
        {
            check(headed.Header);
        }
        if (c is HeaderedItemsControl headedItems)
        {
            check(headedItems.Header);
        }
        if (c is HeaderedSelectingItemsControl headedSelecting)
        {
            check(headedSelecting.Header);
        }
        if (c is ItemsControl items)
        {
            foreach (object? item in items.Items)
            {
                check(item);
            }
        }
    }
}
