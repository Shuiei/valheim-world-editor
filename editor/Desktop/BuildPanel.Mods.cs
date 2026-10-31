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

    /// <summary>Reads the installed mods' pieces again (the window does: ScanModPieces).</summary>
    public event Action? RescanAsked;

    /// <summary>Reads the installed mods' pieces again, now (Rescan mods).</summary>
    internal Button RescanButton { get; } = new() { Content = Lang.T("build.label.rescan"), FontSize = 11.5, Padding = new Thickness(8, 2), HorizontalAlignment = HorizontalAlignment.Stretch };

    /// <summary>The Mod pieces tab's left column: the mods, and Rescan mods under them.</summary>
    private readonly DockPanel _modColumn = new() { Width = 200, IsVisible = false };

    /// <summary>Whether the installed mods' pieces are being read: the tab says so, Rescan mods waits.</summary>
    internal bool ScanningMods { get; private set; }

    /// <summary>The Mod pieces tab's left column, made once: Rescan mods at its foot, the mods above.</summary>
    private DockPanel ModColumn()
    {
        RescanButton.Tip("build.rescan");
        RescanButton.Click += (_, _) => RescanAsked?.Invoke();
        RescanButton.Margin = new Thickness(0, 6, 0, 0);
        DockPanel.SetDock(RescanButton, Dock.Bottom);
        _modColumn.Children.Add(RescanButton);
        _modColumn.Children.Add(new ScrollViewer { Content = ModList });
        return _modColumn;
    }

    /// <summary>
    /// The installed mods' pieces are being read (true) or are read (false): the tab's word and Rescan mods follow, and
    /// the pieces are listed again when done.
    /// </summary>
    internal void ShowScanning(bool on)
    {
        ScanningMods = on;
        RescanButton.IsEnabled = !on;
        RescanButton.Content = Lang.T(on ? "build.label.rescanning" : "build.label.rescan");
        NoMods.Text = Lang.T(on ? "build.scanningMods" : "build.noModPieces");
        if (!on)
        {
            Render();
            RenderRecent();
        }
    }
}
