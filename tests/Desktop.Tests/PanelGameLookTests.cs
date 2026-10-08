using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The start page's game-look card: hidden while the look is there, the copy's progress while it
// runs, the Valheim folder to choose when the game is not found, Try again when the copy failed, and
// a word once it is done; and a changed game folder in Settings checking the look again. The copy
// itself never runs here: the card is given the states.
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
		// Its progress follows.
		set(new("running", null, "/games/valheim", "120/400 models", 0.6));
		b.Refresh();
		Assert.Equal(0.6, b.Bar.Value, 3);
		Assert.Equal("120/400 models", b.LastLine.Text);
		Assert.Equal("Copying the textures and models from your Valheim, once.", b.Message.Text);
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
		Assert.Equal("The game's look could not be copied", b.Title.Text);
		Assert.Equal("The exporter stopped (exit 1).", b.Message.Text);
		Assert.True(b.Retry.IsVisible);
		Assert.False(b.PathRow.IsVisible);
		set(new("running", null, "/games/valheim", null, 0.02));
		b.Retry.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
		Assert.Equal("/games/valheim", Assert.Single(started).Folder);
		Assert.False(b.Retry.IsVisible);
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
	public void TheExportersStateReadsAsASnapshot()
	{
		var s = GameLook.Now();
		Assert.Contains(s.State, new[] { "ready", "missing", "running", "failed" });
	}
}
