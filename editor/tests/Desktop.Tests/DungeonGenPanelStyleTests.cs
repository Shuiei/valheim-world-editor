using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using TerrainEditor.Editing;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Generate panel before its first dungeon is made: Place hands nothing over, and going back to no style of its
/// own gives the biome's style's decay.
/// </summary>
public class DungeonGenPanelStyleTests
{
    [AvaloniaFact]
    public void PlaceWaitsForADungeonAndNoStyleTakesTheBiomes()
    {
        var p = new DungeonGenPanel();
        int placed = 0;
        p.PlaceAsked += (_, _) => placed++;
        p.Place.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(0, placed);
        p.Style.SelectedIndex = 1;
        p.Style.SelectedIndex = 0;
        var style = DungeonKit.StyleOf(DungeonKit.BiomeOf((string?)p.Biome.SelectedItem).Style);
        Assert.Equal(Math.Round(style.Decay / 0.05) * 0.05, p.Decay.Value, 3);
    }

    [AvaloniaFact]
    public void TheBiomesStyleBringsItsDecayWhenTheBiomeChanges()
    {
        // With the biome's style, another biome is another style: its decay came only with a change of Style.
        var p = new DungeonGenPanel();
        p.Style.SelectedIndex = 0;
        static double DecayOf(object? biome) => Math.Round(DungeonKit.StyleOf(DungeonKit.BiomeOf((string?)biome).Style).Decay / 0.05) * 0.05;
        var biomes = p.Biome.Items.Cast<object?>().ToList();
        int other = biomes.FindIndex(b => Math.Abs(DecayOf(b) - p.Decay.Value) > 0.01);
        Assert.True(other >= 0);
        p.Biome.SelectedIndex = other;
        Assert.Equal(DecayOf(biomes[other]), p.Decay.Value, 3);
        // A style of its own keeps its decay whatever the biome.
        p.Style.SelectedIndex = 1;
        double own = p.Decay.Value;
        p.Biome.SelectedIndex = other == 0 ? 1 : 0;
        Assert.Equal(own, p.Decay.Value, 3);
    }
}
