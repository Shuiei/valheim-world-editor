using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The zone filter's panel: the biome toggles, the boxes, the distance from the centre, and the zone buttons.
/// </summary>
public sealed partial class MapPage
{
    /// <summary>Builds the zone section of the side panel: the filter, Show matching, Mark and Unmark.</summary>
    /// <returns>The section.</returns>
    private Expander BuildZones()
    {
        var biomes = new WrapPanel();
        foreach (var (value, name) in ZoneFilter.BiomeChoices())
        {
            var b = new ToggleButton { Content = name, FontSize = 11, Padding = new Thickness(6, 2), Margin = new Thickness(0, 0, 4, 4) }.Tip("map.biome");
            b.IsCheckedChanged += (_, _) => Refilter();
            BiomeButtons[value] = b;
            biomes.Children.Add(b);
        }
        biomes.Tip("map.biome");
        NoBuildBox.Tip("map.noBuild");
        NoEditBox.Tip("map.noEdit");
        OnlyGenBox.Tip("map.onlyGen");
        GroundBox.Tip("map.ground");
        ShowMatchButton.Tip("map.show");
        MarkButton.Tip("map.mark");
        UnmarkButton.Tip("map.unmark");
        foreach (var c in new[] { NoBuildBox, NoEditBox, OnlyGenBox })
        {
            c.IsCheckedChanged += (_, _) => Refilter();
        }
        DistBox.ValueChanged += (_, _) => Refilter();
        RMinBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { Refilter(); } };
        RMaxBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { Refilter(); } };
        ShowMatchButton.Click += async (_, _) => await ShowMatching();
        MarkButton.Click += async (_, _) => await MarkMatching();
        UnmarkButton.Click += async (_, _) => await UnmarkAll();
        return Section(Lang.T("map.head.zones"), false,
            new TextBlock { Text = Lang.T("map.hint.zones"), FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap },
            biomes,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { NoBuildBox, DistBox, new TextBlock { Text = Lang.T("map.label.zones"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } },
            NoEditBox, OnlyGenBox,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = Lang.T("map.label.fromCentre"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, RMinBox, new TextBlock { Text = Lang.T("map.label.to"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, RMaxBox, new TextBlock { Text = Lang.T("common.metres"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } },
            GroundBox,
            new WrapPanel { Children = { Spaced(ShowMatchButton), Spaced(MarkButton), Spaced(UnmarkButton) } },
            ZoneInfo);
    }
}
