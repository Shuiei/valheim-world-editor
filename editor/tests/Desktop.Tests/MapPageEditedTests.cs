using Avalonia.Headless.XUnit;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.LiveSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The map's edited zones in a live world: a zone with changes waiting is marked "(not applied)", as the header says,
/// not "(not saved)".
/// </summary>
[Collection("World files")]
public class MapPageEditedTests
{
    [AvaloniaFact]
    public async Task ALiveZoneWaitingIsMarkedNotApplied()
    {
        using var game = new FakeGame();
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        try
        {
            await w.OpenWorld(() => OpenOn(game), "Connecting…");
            var world = w.World!;
            Raise(WorldScene.Load(world, 0, 0, 1).Session!);
            var map = w.MapPage!;
            map.Show(world);
            string row = Assert.Single((IEnumerable<string>)map.EditedList.ItemsSource!, r => r.StartsWith("0, 0 ", StringComparison.Ordinal));
            Assert.EndsWith("(not applied)", row);
            map.Stop();
        }
        finally
        {
            w.Close();
        }
    }
}
