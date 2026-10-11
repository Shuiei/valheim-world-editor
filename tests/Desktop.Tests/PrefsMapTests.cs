using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The map's place, remembered: where a world's map first looks.
/// </summary>
[Collection("World files")]
public sealed class PrefsMapTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "vwe-prefs-map-" + Guid.NewGuid().ToString("N")[..8] + ".json");

    /// <inheritdoc/>
    public void Dispose() => File.Delete(_file);

    [AvaloniaFact]
    public void TheUnsavedMarksCanBeHiddenAndThatIsRemembered()
    {
        var w = new MainWindow(load: false, prefs: Prefs.Load(_file)) { Width = 1200, Height = 900 };
        w.Show();
        Assert.True(w.UnsavedBox.IsChecked);
        Assert.True(w.View.ShowNewMarkers);
        w.UnsavedBox.IsChecked = false;
        Assert.False(w.View.ShowNewMarkers);
        Assert.False(string.IsNullOrEmpty(ToolTip.GetTip(w.UnsavedBox) as string));
        w.Prefs.Flush();
        var v = new MainWindow(load: false, prefs: Prefs.Load(_file)) { Width = 1200, Height = 900 };
        v.Show();
        Assert.False(v.UnsavedBox.IsChecked);
        Assert.False(v.View.ShowNewMarkers);
    }

    [AvaloniaFact]
    public void TheLookSwitchesAreRemembered()
    {
        var w = new MainWindow(load: false, prefs: Prefs.Load(_file)) { Width = 1200, Height = 900 };
        w.Show();
        // Defaults: the game's look, solid buildings, sharp.
        Assert.True(w.GameLookBox.IsChecked);
        Assert.False(w.SeeThroughBox.IsChecked);
        Assert.Equal(0, w.ResolutionBox.SelectedIndex);
        w.GameLookBox.IsChecked = false;
        w.SeeThroughBox.IsChecked = true;
        w.ResolutionBox.SelectedIndex = 2;
        w.Prefs.Flush();
        var v = new MainWindow(load: false, prefs: Prefs.Load(_file)) { Width = 1200, Height = 900 };
        v.Show();
        Assert.False(v.GameLookBox.IsChecked);
        Assert.True(v.SeeThroughBox.IsChecked);
        Assert.Equal(2, v.ResolutionBox.SelectedIndex);
    }

    [AvaloniaFact]
    public async Task TheMapLooksWhereItWasLeft()
    {
        using var world = new WorldEditor.Tests.TempWorld();
        var w = new MainWindow(load: false, prefs: Prefs.Load(_file)) { Width = 1200, Height = 900 };
        w.Show();
        await w.OpenWorld(() => Task.Run(() => WorldSession.Open(world.Dir)), "Opening…");
        var map = w.MapPage!.Map;
        // Nothing remembered: the whole world.
        Assert.Equal(Vector2.Zero, map.Center);
        Assert.Equal(10500 * 2 / 900f, map.MetersPerPixel, 3);
        map.LookAt(1200, -800, 3);
        w.Prefs.Flush();
        Assert.Equal(new[] { 1200f, -800f, 3f }, Prefs.Load(_file).Get<float[]?>("map.view", null));

        var v = new MainWindow(load: false, prefs: Prefs.Load(_file)) { Width = 1200, Height = 900 };
        v.Show();
        await v.OpenWorld(() => Task.Run(() => WorldSession.Open(world.Dir)), "Opening…");
        Assert.Equal(new Vector2(1200, -800), v.MapPage!.Map.Center);
        Assert.Equal(3, v.MapPage.Map.MetersPerPixel, 3);
    }

    [AvaloniaTheory]
    [InlineData("[1, 2]")]
    [InlineData("[1, 2, -3]")]
    [InlineData("[1, 2, 0]")]
    [InlineData("\"far\"")]
    [InlineData("[1e39, 2, 3]")]
    public async Task ARememberedPlaceThatMakesNoSenseShowsTheWholeWorld(string json)
    {
        File.WriteAllText(_file, $"{{\"map.view\": {json}}}");
        using var world = new WorldEditor.Tests.TempWorld();
        var w = new MainWindow(load: false, prefs: Prefs.Load(_file)) { Width = 1200, Height = 900 };
        w.Show();
        await w.OpenWorld(() => Task.Run(() => WorldSession.Open(world.Dir)), "Opening…");
        Assert.Equal(Vector2.Zero, w.MapPage!.Map.Center);
    }

    [AvaloniaFact]
    public async Task AZoomOutOfRangeIsBroughtBack()
    {
        File.WriteAllText(_file, "{\"map.view\": [10, 20, 500]}");
        using var world = new WorldEditor.Tests.TempWorld();
        var w = new MainWindow(load: false, prefs: Prefs.Load(_file)) { Width = 1200, Height = 900 };
        w.Show();
        await w.OpenWorld(() => Task.Run(() => WorldSession.Open(world.Dir)), "Opening…");
        Assert.Equal(new Vector2(10, 20), w.MapPage!.Map.Center);
        Assert.Equal(40, w.MapPage.Map.MetersPerPixel, 3);
    }
}
