using Avalonia;
using Avalonia.Controls;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

public sealed partial class BuildPanel
{
    /// <summary>
    /// The Mods tab's row of mods (shown when its pieces come from more than one): a button per mod with its number of
    /// pieces, the chosen one on (the first, when none is chosen yet or it has none any more).
    /// </summary>
    /// <param name="list">The Mods tab's pieces; empty: the row hidden (another tab, or a search).</param>
    private void ShowMods(List<(string Prefab, string Name, int Category)> list)
    {
        var mods = list.GroupBy(p => ModGroup(p.Prefab)).Select(g => (Mod: g.Key, Count: g.Count())).ToList();
        _modRow.IsVisible = mods.Count > 1;
        if (!_modRow.IsVisible)
        {
            return;
        }
        if (_mod == null || !mods.Any(m => m.Mod == _mod))
        {
            _mod = mods[0].Mod;
        }
        _modRow.Children.Clear();
        foreach (var (mod, count) in mods)
        {
            var b = new Button { Content = $"{mod} ({count})", FontSize = 11, Padding = new Thickness(7, 2), Tag = mod };
            b.Classes.Set("on", mod == _mod);
            ToolTip.SetTip(b, Lang.T("build.modTip", mod));
            b.Click += (_, _) => { _mod = mod; Render(); };
            _modRow.Children.Add(b);
        }
    }
}
