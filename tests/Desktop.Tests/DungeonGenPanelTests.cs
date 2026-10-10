using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.Editing;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Generate panel: changes come quickly (a seed typed, a slider dragged); the dungeon is made once
// they stop, away from the window's thread, and only the newest is shown.
public class DungeonGenPanelTests
{
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
}
