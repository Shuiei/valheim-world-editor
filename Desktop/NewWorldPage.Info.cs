using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;
using ValheimGen;

namespace TerrainEditor.Desktop;

/// <summary>
/// The lists beside the map: the world, its biomes, the traders, the bosses and the dungeons.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// The lists beside the map: the world, its biomes, the traders, the bosses. A trader's or boss's row shows the
    /// nearest on the map.
    /// </summary>
    private void ShowInfo(SeedPreview.Preview p)
    {
        var s = p.Stats;
        Info.Children.Clear();
        Info.Children.Add(Section(Lang.T("newWorld.head.world"),
            Item(null, Lang.T("newWorld.label.land"), Lang.T("newWorld.ofWorld", s.Land * 100)),
            Item(null, Lang.T("newWorld.label.startLandmass"), Lang.T("newWorld.ofLand", s.StartContinent * 100)),
            Item(null, Lang.T("newWorld.label.largestLandmass"), Lang.T("newWorld.ofLand", s.MainContinent * 100)),
            Item(null, Lang.T("newWorld.label.landAroundStart"), Lang.T("newWorld.percent", s.LandNearStart * 100)),
            Item(Mark("start"), Lang.T("newWorld.label.start"), $"{s.Start.X:0}, {s.Start.Z:0}", s.TempleStart ? Lang.T("newWorld.hint.temple") : Lang.T("newWorld.hint.startLikely"), () => Map.LookAt(s.Start.X, s.Start.Z))));
        var biomes = SeedPreview.Biomes.Where(b => s.Shares[b] > 0.0005f || b == Heightmap.Biome.Meadows).Select(b =>
        {
            var (r, g, bl) = MapData.PixelColor(b);
            var swatch = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Avalonia.Media.Color.FromRgb(r, g, bl)) };
            string nearest = b == Heightmap.Biome.Meadows ? ""
                : s.SameLand[b] || float.IsPositiveInfinity(s.Distance[b]) ? Km(s.Distance[b])
                : Lang.T("newWorld.distanceBySea", Km(s.Distance[b]));
            return Item(swatch, Name(b), nearest, Lang.T("newWorld.ofLand", s.Shares[b] * 100));
        }).ToArray();
        Info.Children.Add(Section(Lang.T("newWorld.head.biomes"), biomes));
        if (p.Landmarks is not { } marks)
        {
            Info.Children.Add(Ui.Hint(Lang.T("newWorld.hint.noLandmarks")));
            return;
        }
        Control Of(string kind, string name)
        {
            var all = marks.Where(m => m.Name == name).ToList();
            var near = all.OrderBy(m => (m.X - s.Start.X) * (m.X - s.Start.X) + (m.Z - s.Start.Z) * (m.Z - s.Start.Z)).FirstOrDefault();
            string value = near == null ? Lang.T("newWorld.none") : Km(MathF.Sqrt((near.X - s.Start.X) * (near.X - s.Start.X) + (near.Z - s.Start.Z) * (near.Z - s.Start.Z)));
            string detail = all.Count == 0 ? "" : kind == "trader" ? Lang.T("newWorld.spots", all.Count) : all.Count == 1 ? Lang.T("newWorld.altars.one") : Lang.T("newWorld.altars.many", all.Count);
            return Item(Mark(kind, name), LandmarkName(name), value, detail, near == null ? null : () => Map.LookAt(near.X, near.Z));
        }
        Info.Children.Add(Section(Lang.T("newWorld.head.traders"), [.. SeedPreview.Landmarks.Where(l => l.Kind == "trader").Select(l => Of("trader", l.Name))]));
        Info.Children.Add(Ui.Hint(Lang.T("newWorld.hint.traderSpots")));
        Info.Children.Add(Section(Lang.T("newWorld.head.bosses"), [.. SeedPreview.Landmarks.Where(l => l.Kind == "boss").Select(l => Of("boss", l.Name))]));
        (_dungeonList.Parent as Panel)?.Children.Remove(_dungeonList);
        Info.Children.Add(_dungeonList);
    }
}
