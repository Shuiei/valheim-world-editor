using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>
    /// One field's row in the card: its label, its value box and its ✕ or ↺, under which its note or decoded bytes show.
    /// </summary>
    /// <param name="f">The field.</param>
    /// <returns>The row, with its note under it when there is one.</returns>
    private Control FieldRow(Field f)
    {
        bool isItems = f.Section == "bytes" && f.Key == ObjectData.ItemsKey && Items != null && _inventoryError == null;
        // A mod's manifest can ask for a value to be shown only, and tell what it is.
        var note = ModManifests.DataOf(f.Name);
        bool ro = f.Section == "bytes";
        var box = ValueBox(f, isItems, ro, note?.ReadOnly == true, note?.Help);
        // A value a mod's manifest shows only cannot be removed either; nor can the contents while they are edited
        // above (Apply writes them again): empty them there instead.
        var rm = new Button { Content = f.Removed ? "↺" : "✕", FontSize = 11, Padding = new Thickness(4, 0), IsEnabled = note?.ReadOnly != true && !isItems };
        rm.Tip(f.Removed ? "inspect.keepValue" : "inspect.removeValue");
        rm.Click += (_, _) => { f.Removed = !f.Removed; Render(); };
        var label = new TextBlock { Text = Label(f), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, TextDecorations = f.Removed ? TextDecorations.Strikethrough : null };
        ToolTip.SetTip(label, $"{f.Name} · {SectionName(f.Section)} · key {f.Key}");
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,170,Auto"), ColumnSpacing = 4, Children = { label, Ui.Col(box, 1), Ui.Col(rm, 2) } };
        // Under a value: its note, or bytes as a mod's extension reads them.
        string? under = !ro ? f.Note : isItems ? null : Decoded(f);
        return under != null
            ? new StackPanel { Children = { row, new TextBlock { Text = under, FontSize = 10, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap } } }
            : row;
    }

    /// <summary>
    /// One added field's row in the card: its label, its value and its ✕, which takes it back out.
    /// </summary>
    /// <param name="a">The added field: its section, key and value.</param>
    /// <returns>The row.</returns>
    private Grid AddedRow((string Section, string Key, string Value) a)
    {
        var x = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(4, 0) }.Tip("inspect.unadd");
        x.Click += (_, _) => { Added.Remove(a); Render(); };
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,170,Auto"),
            ColumnSpacing = 4,
            Children = { new TextBlock { Text = Lang.T("inspect.added", PlainLabel(a.Key) ?? a.Key), FontSize = 11 }, Ui.Col(new TextBlock { Text = a.Value, FontSize = 11 }, 1), Ui.Col(x, 2) },
        };
    }
}
