using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class InspectorPanel
{
    /// <summary>Builds the whole card again from what is shown and changed.</summary>
    private void Render()
    {
        var z = _z!;
        var s = _session();
        string? name = PrefabCatalog.DisplayName(z.Prefab);
        _title.Text = name ?? Lang.T("inspect.prefab", z.Prefab);
        int id = Index is int ix && s != null && ix < s.Scene.Things.Count ? s.Scene.Things[ix].Id : 0;
        _where.Text = Where(z, id, name);
        RenderItems();
        RenderStand();
        Fields.Children.Clear();
        if (FieldList.Count == 0 && Added.Count == 0)
        {
            Fields.Children.Add(new TextBlock { Text = Lang.T("inspect.noData"), FontSize = 11, Foreground = Ui.Muted });
        }
        foreach (var f in FieldList)
        {
            bool isItems = f.Section == "bytes" && f.Key == ObjectData.ItemsKey && Items != null && _inventoryError == null;
            // A mod's manifest can ask for a value to be shown only, and tell what it is.
            var note = ModManifests.DataOf(f.Name);
            bool ro = f.Section == "bytes";
            var box = new TextBox { Text = isItems ? Lang.T("inspect.seeContents") : ro ? f.Note : f.Now, IsReadOnly = ro || note?.ReadOnly == true, FontSize = 11, Opacity = f.Removed ? 0.5 : 1 }.Tip("inspect.value");
            if (note?.Help is string help)
            {
                ToolTip.SetTip(box, help);
            }
            box.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty && !ro)
                {
                    f.Now = box.Text ?? "";
                    Dirty();
                }
            };
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
            Fields.Children.Add(under != null
                ? new StackPanel { Children = { row, new TextBlock { Text = under, FontSize = 10, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap } } }
                : row);
        }
        foreach (var a in Added.ToList())
        {
            var x = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(4, 0) }.Tip("inspect.unadd");
            x.Click += (_, _) => { Added.Remove(a); Render(); };
            Fields.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,170,Auto"),
                ColumnSpacing = 4,
                Children = { new TextBlock { Text = Lang.T("inspect.added", PlainLabel(a.Key) ?? a.Key), FontSize = 11 }, Ui.Col(new TextBlock { Text = a.Value, FontSize = 11 }, 1), Ui.Col(x, 2) },
            });
        }
        Dirty();
    }
}
