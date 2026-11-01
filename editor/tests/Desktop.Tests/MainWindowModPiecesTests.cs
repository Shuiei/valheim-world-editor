using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Reading the installed mods' pieces from the window: with the tests' copies of the game data there is no Valheim, so
/// no mod of its to read: nothing is read, the dock does not wait.
/// </summary>
public class MainWindowModPiecesTests
{
    [AvaloniaFact]
    public async Task WithoutAGameNothingIsRead()
    {
        Assert.False(GameData.FromGame);
        var w = new MainWindow(load: false);

        await w.ScanModPieces();
        await w.ScanModPieces(again: true);

        Assert.False(w.BuildPanel.ScanningMods);
        Assert.True(w.BuildPanel.RescanButton.IsEnabled);
    }
}
