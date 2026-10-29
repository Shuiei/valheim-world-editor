using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using ValheimGen;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Dungeons list beside the map: each kind's nearest, and how many in the world and near the start.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// The Dungeons list: each kind's nearest, how many in the world and near the start.
    /// </summary>
    private void FillDungeons()
    {
        _dungeonList.Children.Clear();
        _dungeonList.Children.Add(new TextBlock { Text = Lang.T("newWorld.head.dungeonsNearest"), FontSize = 13, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        if (Shown?.Landmarks == null)
        {
            return;
        }
        if (DungeonMarks == null && _dungeons == null)
        {
            if (_dungeonError != null)
            {
                _dungeonList.Children.Add(Ui.Hint(Lang.T("newWorld.error.dungeons", _dungeonError)));
            }
            return;
        }
        if (DungeonMarks == null)
        {
            _dungeonList.Children.Add(new StackPanel
            {
                Spacing = 6,
                Margin = new Thickness(6, 2, 0, 0),
                Children = { Ui.Hint(Lang.T("newWorld.msg.findingDungeons")), new ProgressBar { IsIndeterminate = true, Height = 4, MinHeight = 4, Width = 200, HorizontalAlignment = HorizontalAlignment.Left } },
            });
            return;
        }
        var (x, z) = Shown.Stats.Start;
        float Far(SeedPreview.Landmark m) => MathF.Sqrt((m.X - x) * (m.X - x) + (m.Z - z) * (m.Z - z));
        foreach (var (name, biome, _) in SeedPreview.Dungeons)
        {
            var all = DungeonMarks.Where(m => m.Name == name).ToList();
            var near = all.OrderBy(Far).FirstOrDefault();
            int close = all.Count(m => Far(m) <= 2000);
            string count = biome == Heightmap.Biome.None
                ? Lang.T("newWorld.dungeonCount", all.Count, close)
                : Lang.T("newWorld.dungeonCountIn", Name(biome), all.Count, close);
            _dungeonList.Children.Add(Item(DungeonMark(name), DungeonName(name), near == null ? Lang.T("newWorld.none") : Km(Far(near)), count,
                near == null ? null : () =>
                {
                    ShowDungeons[name].IsChecked = true;
                    Map.LookAt(near.X, near.Z);
                }));
        }
    }
}
