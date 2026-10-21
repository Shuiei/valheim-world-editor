using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Editing;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Generate panel: changes come quickly (a seed typed, a slider dragged); the dungeon is made once they stop, away
/// from the window's thread, and only the newest is shown.
/// </summary>
public class DungeonGenPanelTests
{
    /// <summary>
    /// Waits (4 s at most) until the panel's dungeon is made and Place is on.
    /// </summary>
    private static async Task Settled(DungeonGenPanel p)
    {
        for (int i = 0; i < 200 && !p.Place.IsEnabled; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await p.Pending;
            await Task.Delay(20);
        }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task TheNewestSettingsAreMadeOnceTheyStopChanging()
    {
        var p = new DungeonGenPanel();
        await Settled(p);
        Assert.True(p.Place.IsEnabled);
        p.Seed.Text = "1";
        p.Seed.Text = "12";
        p.Seed.Text = "123";
        Dispatcher.UIThread.RunJobs();
        Assert.False(p.Place.IsEnabled);
        await Settled(p);
        Assert.True(p.Place.IsEnabled);
        Assert.StartsWith(DungeonGen.NameFor(123, null, "Black Forest"), p.Summary.Text);
    }

    [AvaloniaFact]
    public async Task NoDungeonIsMadeBeforeTheGamesDataIsInUse()
    {
        // At the editor's start the catalogues are empty: making it then failed on the key chest ("CryptKey" is not
        // an item of the game). The panel waits instead, and makes it once the data is in use.
        bool ready = false;
        var p = new DungeonGenPanel { GameDataReady = () => ready };
        await Task.Delay(400);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Lang.T("dungeon.hint.waitingForGame"), p.Summary.Text);
        Assert.False(p.Failed);
        Assert.False(p.Place.IsEnabled);

        ready = true;
        await Settled(p);

        Assert.True(p.Place.IsEnabled);
    }

    [AvaloniaFact]
    public void TheGamesRoomsAreShownWithTheNameTheyArePlacedWith()
    {
        // A style is the building pieces' own: the game's rooms are named after the biome (DungeonGen.MakeRooms), as the
        // placed dungeon is.
        var p = new DungeonGenPanel();
        p.Style.SelectedIndex = 5;
        Assert.Equal(DungeonGen.NameFor(1, "Prison", "Black Forest"), p.Name.PlaceholderText);

        p.Made.SelectedIndex = 1;

        Assert.Equal(DungeonGen.NameFor(1, null, "Black Forest"), p.Name.PlaceholderText);
    }
}
