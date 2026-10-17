using Avalonia.Controls;
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
            Fields.Children.Add(FieldRow(f));
        }
        foreach (var a in Added.ToList())
        {
            Fields.Children.Add(AddedRow(a));
        }
        Dirty();
    }
}
