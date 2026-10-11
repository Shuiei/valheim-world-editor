using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What the Place tool remembers: your presets, favourites and recent kinds. Alone (DataDir): it reads
/// place.json back, which the other Place tests write too when they place something.
/// </summary>
[Collection("DataDir")]
public class PlaceMemoryTests
{
    [AvaloniaFact]
    public async Task PresetsFavouritesAndRecentAreKept()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        var p = w.PlacePanel;
        p.Memory.Presets.Clear();
        w.PlaceTool.Chosen.Clear();
        w.PlaceTool.Chosen.Add("Birch1");
        w.PlaceTool.Weights["Birch1"] = 3;
        w.PlaceTool.Density = 7;
        p.AskName = () => Task.FromResult<string?>("My birches");
        p.Confirm = _ => Task.FromResult(true);
        p.SavePresetButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        await Task.Delay(10);
        var mine = Assert.Single(p.Memory.Presets);
        Assert.Equal(7, mine.Density);
        Assert.Equal(3, mine.Kinds["Birch1"]);
        Assert.True(p.DeletePresetButton.IsEnabled);
        // Saved to the file: read back.
        var again = PlaceMemory.Load();
        Assert.Equal("My birches", again.Presets.Single().Name);
        p.Memory.ToggleFavourite("Oak1");
        Assert.Contains("Oak1", PlaceMemory.Load().Favourites);
        p.Memory.NoteRecent(["Beech1", "Oak1"]);
        p.Memory.NoteRecent(["Rock_3"]);
        Assert.Equal(new[] { "Rock_3", "Beech1", "Oak1" }, PlaceMemory.Load().Recent);
        p.DeletePresetButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        await Task.Delay(10);
        Assert.Empty(p.Memory.Presets);
    }
}
