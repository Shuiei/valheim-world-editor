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
}
