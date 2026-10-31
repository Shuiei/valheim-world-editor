using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

public sealed partial class BuildPanel
{
    /// <summary>
    /// The Mod pieces tab's list of mods, down its left side: a button per mod with its number of pieces, the chosen one
    /// on (the first, when none is chosen yet or it has none any more).
    /// </summary>
    /// <param name="list">The Mod pieces tab's pieces; empty: the list hidden (another tab, a search, or no mod pieces).</param>
    private void ShowMods(List<(string Prefab, string Name, int Category)> list)
    {
        var mods = list.GroupBy(p => ModGroup(p.Prefab)).Select(g => (Mod: g.Key, Count: g.Count())).ToList();
        ModList.IsVisible = mods.Count > 0;
        ((Control)ModList.Parent!).IsVisible = ModList.IsVisible;
        if (!ModList.IsVisible)
        {
            return;
        }
        if (_mod == null || !mods.Any(m => m.Mod == _mod))
        {
            _mod = mods[0].Mod;
        }
        ModList.Children.Clear();
        ModList.Children.Add(Ui.Heading(Lang.T("build.head.mods"), 0));
        foreach (var (mod, count) in mods)
        {
            var b = new Button
            {
                Tag = mod,
                FontSize = 12,
                Padding = new Thickness(8, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = Ui.Counted(mod, count),
            };
            b.Classes.Set("on", mod == _mod);
            ToolTip.SetTip(b, Lang.T("build.modTip", mod));
            b.Click += (_, _) => { _mod = mod; Render(); };
            ModList.Children.Add(b);
        }
    }
}
