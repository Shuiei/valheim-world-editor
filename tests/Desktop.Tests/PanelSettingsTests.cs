using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Settings window: what it shows from the settings, Browse (the folder picker), Automatic, adding
// and taking out folders, a wrong choice explained without closing, Save keeping the choices, Cancel
// keeping nothing, and "~" in a path. It saves to the tests' settings file, so it runs alone.
[Collection("DataDir")]
public class PanelSettingsTests
{
	private sealed class Temp : IDisposable
	{
		public string Root { get; } = Path.Combine(Path.GetTempPath(), "vwe-settings-ui-" + Guid.NewGuid().ToString("N")[..8]);

		public string Dir(string name)
		{
			string d = Path.Combine(Root, name);
			Directory.CreateDirectory(d);
			return d;
		}

		// A folder that looks like the game's (valheim_Data/StreamingAssets/SoftRef/Bundles).
		public string Game()
		{
			string g = Dir("Valheim");
			Directory.CreateDirectory(Path.Combine(g, "valheim_Data", "StreamingAssets", "SoftRef", "Bundles"));
			return g;
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(Root, true);
			}
			catch (Exception)
			{
			}
		}
	}

	private static Window Owner()
	{
		var w = new Window { Width = 900, Height = 700 };
		w.Show();
		return w;
	}

	private static List<TextBox> Boxes(Window d) => d.GetLogicalDescendants().OfType<TextBox>().ToList();

	private static List<Button> Buttons(Window d, string label) => d.GetLogicalDescendants().OfType<Button>().Where(b => b.Content as string == label).ToList();

	private static void Click(Button b)
	{
		b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		Dispatcher.UIThread.RunJobs();
	}

	private static string Texts(Window d) => string.Join(" | ", d.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

	[AvaloniaFact]
	public async Task ItShowsTheSettingsAndSavesTheChoices()
	{
		using var t = new Temp();
		string bep = t.Dir("profile"), worlds = t.Dir("worlds"), game = t.Game();
		var settings = new AppSettings { BepInExFolders = { bep }, WorldFolders = { worlds } };
		var owner = Owner();
		var shown = SettingsDialog.Show(owner, settings);
		var d = PanelDialogsTests.Shown(owner);
		Assert.Equal("Settings: folders on this computer", d.Title);
		var boxes = Boxes(d);
		// The Valheim folder (empty: automatic), then one row per folder.
		Assert.Equal(new[] { null, bep, worlds }, boxes.Select(b => b.Text));
		Assert.Matches("^(Found automatically: |Not found automatically: choose the folder.)", Texts(d).Split(" | ").First(x => x.Contains("automatically")));
		boxes[0].Text = game;
		Click(Buttons(d, "Save").Single());
		Assert.True(await shown);
		Assert.Empty(owner.OwnedWindows);
		var kept = AppSettings.Load();
		Assert.Equal(game, kept.ValheimPath);
		Assert.Equal(new[] { bep }, kept.BepInExFolders);
		Assert.Equal(new[] { worlds }, kept.WorldFolders);
	}

	[AvaloniaFact]
	public async Task AWrongChoiceIsExplainedAndTheWindowStays()
	{
		using var t = new Temp();
		var settings = new AppSettings();
		var owner = Owner();
		var shown = SettingsDialog.Show(owner, settings);
		var d = PanelDialogsTests.Shown(owner);
		Boxes(d)[0].Text = t.Dir("NotTheGame");
		Click(Buttons(d, "Save").Single());
		Assert.Contains("The Valheim folder must be the game's folder", Texts(d));
		Assert.Single(owner.OwnedWindows);
		// Cancel: nothing kept.
		Click(Buttons(d, "Cancel").Single());
		Assert.False(await shown);
		Assert.Null(settings.ValheimPath);
	}

	[AvaloniaFact]
	public async Task BrowseAutomaticAndFolderRows()
	{
		using var t = new Temp();
		string game = t.Game(), bep = t.Dir("bep"), extra = t.Dir("extra");
		var settings = new AppSettings();
		var owner = Owner();
		var picks = new Queue<string?>(new[] { game, bep, null });
		var asked = new List<string>();
		SettingsDialog.PickFolder = title => { asked.Add(title); return Task.FromResult(picks.Dequeue()); };
		try
		{
			var shown = SettingsDialog.Show(owner, settings);
			var d = PanelDialogsTests.Shown(owner);
			// Browse beside the Valheim folder fills it; Automatic empties it.
			Click(Buttons(d, "Browse…")[0]);
			Assert.Equal(game, Boxes(d)[0].Text);
			Click(Buttons(d, "Automatic").Single());
			Assert.Equal("", Boxes(d)[0].Text);
			// Add a BepInEx folder, browse for it; add a world folder and take it out again.
			var adds = Buttons(d, "Add a folder");
			Click(adds[0]);
			Click(Buttons(d, "Browse…")[1]);
			Assert.Equal(bep, Boxes(d)[1].Text);
			Click(adds[1]);
			Assert.Equal(3, Boxes(d).Count);
			// A cancelled picker leaves the box as it was.
			Boxes(d)[2].Text = extra;
			Click(Buttons(d, "Browse…")[2]);
			Assert.Equal(extra, Boxes(d)[2].Text);
			Click(Buttons(d, "✕")[1]);
			Assert.Equal(2, Boxes(d).Count);
			Click(Buttons(d, "Save").Single());
			Assert.True(await shown);
			Assert.Null(settings.ValheimPath);
			Assert.Equal(new[] { bep }, settings.BepInExFolders);
			Assert.Empty(settings.WorldFolders);
			Assert.Equal(3, asked.Count);
			Assert.Contains("valheim_Data", asked[0]);
		}
		finally
		{
			SettingsDialog.PickFolder = null;
		}
	}

	[AvaloniaFact]
	public void AHomePathAndQuotesAreUnderstood()
	{
		var settings = new AppSettings();
		string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		string? problem = SettingsDialog.Apply(settings, null, new[] { "\"~/no-such-folder-for-vwe-tests\"" }, Array.Empty<string>());
		Assert.Contains(Path.Combine(home, "no-such-folder-for-vwe-tests"), problem);
	}
}
