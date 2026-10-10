using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The start page's game-look card: hidden while the look is there, the copy's progress while it
// runs, how to find the Valheim folder and a field to choose it when the game is not found, Try again
// when the copy failed, and a word once it is done; a folder near the game taken as the game folder;
// and a changed game folder in Settings checking the look again. The copy
// itself never runs here: the card is given the states.
// Alone (DataDir): it swaps SettingsDialog.CheckGameLook, which other tests' Settings call too.
[Collection("DataDir")]
public class PanelGameLookTests
{
	private static (GameLookBanner B, List<(string Folder, AppSettings S)> Started, Func<GameLook.Snapshot, GameLook.Snapshot> Set) Banner(GameLook.Snapshot first)
	{
		var settings = new AppSettings();
		var now = first;
		var started = new List<(string, AppSettings)>();
		var b = new GameLookBanner(settings) { Read = () => now, StartExport = (f, s) => { started.Add((f, s)); return f.Contains("bad") ? "That folder is not a Valheim game folder." : null; } };
		var w = new Window { Content = b.View };
		w.Show();
		return (b, started, s => now = s);
	}

	[AvaloniaFact]
	public void WhenTheLookIsThereNothingIsShown()
	{
		var (b, _, _) = Banner(new("ready", null, "/games/valheim", null, 1));
		Assert.False(b.View.IsVisible);
	}

	[AvaloniaFact]
	public void TheCopyShowsItsProgressThenSaysWhenItIsDone()
	{
		var (b, _, set) = Banner(new("running", "Valheim was updated: refreshing the game's look.", "/games/valheim", "scan: 10/100 bundles", 0.04));
		Assert.True(b.View.IsVisible);
		Assert.Equal("Preparing the game's look", b.Title.Text);
		Assert.Equal("Valheim was updated: refreshing the game's look.", b.Message.Text);
		Assert.True(b.Bar.IsVisible);
		Assert.Equal(0.04, b.Bar.Value, 3);
		Assert.Equal("scan: 10/100 bundles", b.LastLine.Text);
		Assert.Contains("You can already pick a world", b.Hint.Text);
		Assert.False(b.Steps.IsVisible);
		// Its progress follows.
		set(new("running", null, "/games/valheim", "120/400 models", 0.6));
		b.Refresh();
		Assert.Equal(0.6, b.Bar.Value, 3);
		Assert.Equal("120/400 models", b.LastLine.Text);
		Assert.Equal("Reading the game's files (a few seconds, only after a game update).", b.Message.Text);
		// Done: still shown, saying where it will be seen.
		set(new("ready", null, "/games/valheim", null, 1));
		b.Refresh();
		Assert.True(b.View.IsVisible);
		Assert.Equal("The game's look is ready.", b.Title.Text);
		Assert.False(b.Bar.IsVisible);
		Assert.False(b.Hint.IsVisible);
	}

	[AvaloniaFact]
	public async Task WithoutValheimTheFolderIsChosenAndTheCopyStarted()
	{
		var (b, started, set) = Banner(new("missing", "Valheim was not found on this computer. Choose its folder to get the game's look.", null, null, null));
		Assert.True(b.View.IsVisible);
		Assert.Equal("Get the game's look", b.Title.Text);
		Assert.True(b.PathRow.IsVisible);
		// How to find the folder, step by step.
		Assert.True(b.Steps.IsVisible);
		Assert.Contains("Manage › Browse local files", b.Steps.Text);
		Assert.False(b.Bar.IsVisible);
		Assert.False(b.Retry.IsVisible);
		// Nothing typed: says what to choose, starts nothing.
		b.Start("  ");
		Assert.Contains("Choose the folder Steam installed Valheim into", b.Error.Text);
		Assert.True(b.Error.IsVisible);
		Assert.Empty(started);
		// Browse fills the field; Use starts the copy from it.
		b.PickFolder = _ => Task.FromResult<string?>("/games/valheim");
		b.Browse.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
		await Task.Delay(10);
		Assert.Equal("/games/valheim", b.PathBox.Text);
		set(new("running", null, "/games/valheim", null, 0.02));
		b.Use.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
		Assert.Equal("/games/valheim", Assert.Single(started).Folder);
		Assert.Equal("", b.Error.Text);
		Assert.False(b.Error.IsVisible);
		Assert.Equal("Preparing the game's look", b.Title.Text);
	}

	[AvaloniaFact]
	public void AFolderThatIsNotTheGamesIsRefusedWithTheReason()
	{
		var (b, started, _) = Banner(new("missing", null, "/found/valheim", null, null));
		// The folder found (not a full game) is offered in the field.
		Assert.Equal("/found/valheim", b.PathBox.Text);
		Assert.Equal("Valheim was not found on this computer.", b.Message.Text);
		b.PathBox.Text = "/bad/folder";
		b.Use.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
		Assert.Single(started);
		Assert.Equal("That folder is not a Valheim game folder.", b.Error.Text);
		Assert.True(b.Error.IsVisible);
	}

	[AvaloniaFact]
	public void AFailedCopyIsTriedAgainFromTheSameFolder()
	{
		var (b, started, set) = Banner(new("failed", "The exporter stopped (exit 1).", "/games/valheim", null, null));
		Assert.Equal("The game's look could not be read", b.Title.Text);
		Assert.Equal("The exporter stopped (exit 1).", b.Message.Text);
		Assert.True(b.Retry.IsVisible);
		Assert.False(b.PathRow.IsVisible);
		set(new("running", null, "/games/valheim", null, 0.02));
		b.Retry.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
		Assert.Equal("/games/valheim", Assert.Single(started).Folder);
		Assert.False(b.Retry.IsVisible);
	}

	[AvaloniaFact]
	public async Task AFailedCopyOffersTheLog()
	{
		var (b, _, _) = Banner(new("failed", "Copying the game's look failed: ValueError: 15 is not in list", "/games/valheim", null, null));
		int opened = 0;
		b.OpenLog = () => { opened++; return Task.FromResult(true); };
		Assert.True(b.ShowLog.IsVisible);
		Assert.Contains("Open log", b.Hint.Text);
		b.ShowLog.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
		await Task.Delay(10);
		Assert.Equal(1, opened);
		// Not while it runs.
		var (r, _, _) = Banner(new("running", null, "/games/valheim", null, 0.1));
		Assert.False(r.ShowLog.IsVisible);
	}

	[AvaloniaFact]
	public async Task TheStartPageOpensTheLogFile()
	{
		var opened = new List<Uri>();
		var page = new StartPage(new AppSettings()) { OpenUrl = u => { opened.Add(u); return Task.FromResult(true); } };
		page.LogLink.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
		await Task.Delay(10);
		Assert.Equal(Path.GetFullPath(Log.FilePath), Assert.Single(opened).LocalPath);
		// The game-look card opens the same file.
		await page.LookBanner.OpenLog();
		Assert.Equal(2, opened.Count);
		Assert.True(opened[1].IsFile);
	}

	[AvaloniaFact]
	public void AFailedCopyWithNoFolderKnownUsesTheSettingsOne()
	{
		var settings = new AppSettings { ValheimPath = "/from/settings" };
		var started = new List<string>();
		var b = new GameLookBanner(settings) { Read = () => new("failed", null, null, null, null), StartExport = (f, _) => { started.Add(f); return null; } };
		b.Retry.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
		Assert.Equal("/from/settings", Assert.Single(started));
		Assert.Equal("", b.Message.Text);
	}

	[AvaloniaFact]
	public void TheStartPageShowsTheCardAndPassesItsFolderPicker()
	{
		var page = new StartPage(new AppSettings()) { PickFolder = _ => Task.FromResult<string?>("/picked") };
		Assert.Contains(page.LookBanner.View, ((StackPanel)((ScrollViewer)page.View).Content!).Children);
		Assert.Equal("/picked", page.LookBanner.PickFolder("x").Result);
	}

	[Fact]
	public void AnotherGameFolderInSettingsChecksTheLookAgain()
	{
		string game = Path.Combine(Path.GetTempPath(), "vwe-valheim-" + Guid.NewGuid().ToString("N")[..8]);
		Directory.CreateDirectory(Path.Combine(game, "valheim_Data", "StreamingAssets", "SoftRef", "Bundles"));
		var old = SettingsDialog.CheckGameLook;
		var checkedFor = new List<string?>();
		SettingsDialog.CheckGameLook = s => checkedFor.Add(s.ValheimPath);
		try
		{
			var settings = new AppSettings();
			Assert.Null(SettingsDialog.Apply(settings, game, Array.Empty<string>(), Array.Empty<string>()));
			Assert.Equal(new[] { Path.GetFullPath(game) }, checkedFor);
			// The same folder again: nothing to check.
			Assert.Null(SettingsDialog.Apply(settings, game, Array.Empty<string>(), Array.Empty<string>()));
			Assert.Single(checkedFor);
			// Back to automatic: checked again.
			Assert.Null(SettingsDialog.Apply(settings, " ", Array.Empty<string>(), Array.Empty<string>()));
			Assert.Equal(2, checkedFor.Count);
			Assert.Null(checkedFor[1]);
		}
		finally
		{
			SettingsDialog.CheckGameLook = old;
			Directory.Delete(game, true);
		}
	}

	[Fact]
	public void TheDataFolderShowsTheHomeAsATilde()
	{
		string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (OperatingSystem.IsWindows())
		{
			Assert.Equal(Path.Combine(home, "x"), StartPage.Tilde(Path.Combine(home, "x")));
			return;
		}
		Assert.Equal("~/.local/share/ValheimWorldEditor", StartPage.Tilde(home + "/.local/share/ValheimWorldEditor"));
		Assert.Equal("~", StartPage.Tilde(home));
		// Only the whole home folder: not a folder whose name merely starts the same.
		Assert.Equal(home + "x/data", StartPage.Tilde(home + "x/data"));
		Assert.Equal("/opt/editor", StartPage.Tilde("/opt/editor"));
	}

	[AvaloniaFact]
	public void EmptyErrorsAndAnEmptyServerListTakeNoRoom()
	{
		var page = new StartPage(new AppSettings());
		page.FillServers();
		Assert.Equal(ServerConfig.Load().Count > 0, page.SavedServers.IsVisible);
		Assert.False(page.PathError.IsVisible);
		page.PathError.Text = "Not a world folder.";
		Assert.True(page.PathError.IsVisible);
		page.PathError.Text = "";
		Assert.False(page.PathError.IsVisible);
	}

	[Fact]
	public void AFolderNearTheGameLeadsToIt()
	{
		string root = Path.Combine(Path.GetTempPath(), "vwe-steam-" + Guid.NewGuid().ToString("N")[..8]);
		string game = Path.Combine(root, "steamapps", "common", "Valheim");
		Directory.CreateDirectory(Path.Combine(game, "valheim_Data", "StreamingAssets", "SoftRef", "Bundles"));
		try
		{
			foreach (string chosen in new[] { game, game + Path.DirectorySeparatorChar, Path.Combine(game, "valheim_Data"), Path.Combine(root, "steamapps", "common"), Path.Combine(root, "steamapps"), root, $"  \"{game}\" " })
			{
				Assert.Equal(Path.GetFullPath(game), GameLook.GameFolder(chosen));
			}
			Assert.Null(GameLook.GameFolder(Path.Combine(game, "valheim_Data", "StreamingAssets")));
			Assert.Null(GameLook.GameFolder(Path.Combine(root, "nowhere")));
			// Settings keeps the game folder itself.
			var old = SettingsDialog.CheckGameLook;
			SettingsDialog.CheckGameLook = _ => { };
			try
			{
				var settings = new AppSettings();
				Assert.Null(SettingsDialog.Apply(settings, Path.Combine(root, "steamapps"), Array.Empty<string>(), Array.Empty<string>()));
				Assert.Equal(Path.GetFullPath(game), settings.ValheimPath);
			}
			finally
			{
				SettingsDialog.CheckGameLook = old;
			}
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	[Fact]
	public void TheExportersStateReadsAsASnapshot()
	{
		var s = GameLook.Now();
		Assert.Contains(s.State, new[] { "ready", "missing", "running", "failed" });
	}
}
