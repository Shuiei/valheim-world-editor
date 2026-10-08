using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The game's look (terrain textures, map textures, models) is copied once from the player's own
// Valheim, in the background (GameLook): the start page's card that follows it. While it runs, its
// progress; when Valheim is not found, a field to choose its folder; when it failed, Try again; once
// it is done, a word that the next area opened has it. Hidden when there is nothing to say.
public sealed class GameLookBanner
{
	public Control View => _card;
	private readonly Border _card;
	private readonly AppSettings _settings;
	private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1.5) };
	private string _state = "";
	private bool _sawRunning;

	internal TextBlock Title { get; } = new() { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
	internal TextBlock Message { get; } = new() { TextWrapping = TextWrapping.Wrap };
	internal ProgressBar Bar { get; } = new() { Minimum = 0, Maximum = 1, Height = 6, MinHeight = 6 };
	internal TextBlock LastLine { get; } = Ui.Hint("");
	internal TextBox PathBox { get; } = new() { Watermark = "…/steamapps/common/Valheim", FontSize = 13 };
	internal Button Browse { get; } = new() { Content = "Browse…" };
	internal Button Use { get; } = new Button { Content = "Use" }.Classed("primary");
	internal Button Retry { get; } = new Button { Content = "Try again" }.Classed("primary");
	internal TextBlock Error { get; } = new() { Foreground = new SolidColorBrush(Color.FromRgb(224, 96, 75)), TextWrapping = TextWrapping.Wrap, FontSize = 12 };
	internal TextBlock Hint { get; } = Ui.Hint("");
	internal Grid PathRow { get; }

	// Tests: the exporter's state, starting it, and the folder picker.
	internal Func<GameLook.Snapshot> Read { get; set; } = GameLook.Now;
	internal Func<string, AppSettings, string?> StartExport { get; set; } = (folder, settings) => GameLook.Start(folder, settings);
	internal Func<string, Task<string?>> PickFolder { get; set; } = _ => Task.FromResult<string?>(null);

	public GameLookBanner(AppSettings settings)
	{
		_settings = settings;
		Grid.SetColumn(Browse, 1);
		Grid.SetColumn(Use, 2);
		PathRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 6, Children = { PathBox, Browse, Use } };
		_card = Ui.Card(new StackPanel { Spacing = 6, Children = { Title, Message, Bar, LastLine, PathRow, Retry, Error, Hint } });
		_card.Margin = new Thickness(0, 10, 0, 0);
		_card.IsVisible = false;
		Browse.Click += async (_, _) => { if (await PickFolder("Choose the Valheim game folder (with valheim_Data)") is string p) PathBox.Text = p; };
		Use.Click += (_, _) => Start(PathBox.Text);
		Retry.Click += (_, _) => Start(Read().Valheim ?? _settings.ValheimPath);
		_timer.Tick += (_, _) => Refresh();
		_card.AttachedToVisualTree += (_, _) => { Refresh(); _timer.Start(); };
		_card.DetachedFromVisualTree += (_, _) => _timer.Stop();
	}

	internal void Start(string? folder)
	{
		if (string.IsNullOrWhiteSpace(folder))
		{
			Error.Text = "Choose the folder Steam installed Valheim into (the one with valheim_Data).";
			Error.IsVisible = true;
			return;
		}
		Error.Text = StartExport(folder, _settings) ?? "";
		Refresh();
	}

	// Shows the exporter's state (rebuilt when it changes; the progress every time).
	internal void Refresh()
	{
		var s = Read();
		bool changed = s.State != _state;
		_state = s.State;
		Bar.IsVisible = LastLine.IsVisible = PathRow.IsVisible = Retry.IsVisible = false;
		switch (s.State)
		{
			case "running":
				_sawRunning = true;
				Title.Text = "Preparing the game's look";
				Message.Text = s.Message ?? "Copying the textures and models from your Valheim, once.";
				Bar.IsVisible = LastLine.IsVisible = true;
				Bar.Value = s.Progress ?? 0;
				LastLine.Text = s.LastLine ?? "";
				Hint.Text = "You can already pick a world and edit: areas opened once this is done have the game's look.";
				break;
			case "missing":
				Title.Text = "Get the game's look";
				Message.Text = s.Message ?? "Valheim was not found on this computer.";
				PathRow.IsVisible = true;
				if (changed && string.IsNullOrEmpty(PathBox.Text))
				{
					PathBox.Text = s.Valheim ?? "";
				}
				Hint.Text = "Choose the folder Steam installed Valheim into (the one with valheim_Data): the editor copies the game's textures and models from it, once. Without it the editor works with plain colours and no models.";
				break;
			case "failed":
				Title.Text = "The game's look could not be copied";
				Message.Text = s.Message ?? "";
				Retry.IsVisible = true;
				Hint.Text = "";
				break;
			default:
				Title.Text = "The game's look is ready.";
				Message.Text = "The next world or area you open has the game's textures and models.";
				Hint.Text = "";
				break;
		}
		Hint.IsVisible = Hint.Text.Length > 0;
		Error.IsVisible = Error.Text?.Length > 0;
		_card.IsVisible = s.State != "ready" || _sawRunning;
	}
}
