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
        // From an empty memory: the tests before it in the run may have left favourites and recent kinds.
        p.Memory.Presets.Clear();
        p.Memory.Favourites.Clear();
        p.Memory.Recent.Clear();
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

    [AvaloniaFact]
    public void SaveLaterWritesOnceAfterAPause()
    {
        // Every share-slider tick wrote place.json on the UI thread; now a pause of 300 ms writes it once.
        string? was = PlaceMemory.PathOverride;
        string dir = Path.Combine(Path.GetTempPath(), "vwe-placelater-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            PlaceMemory.PathOverride = Path.Combine(dir, "place.json");
            var m = new PlaceMemory();
            for (int i = 1; i <= 20; i++)
            {
                m.Weights["Oak1"] = i;
                m.SaveLater();
            }
            Assert.False(File.Exists(PlaceMemory.PathOverride));
            m.Flush();
            Assert.Equal(20, PlaceMemory.Load().Weights["Oak1"]);
            // Nothing waits any more: a second flush writes nothing.
            File.Delete(PlaceMemory.PathOverride);
            m.Flush();
            Assert.False(File.Exists(PlaceMemory.PathOverride));
        }
        finally
        {
            PlaceMemory.PathOverride = was;
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
