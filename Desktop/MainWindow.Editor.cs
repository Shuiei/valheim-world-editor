using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The 3D editor's world controls, as the web editor had them: the area moved by one zone (or
// following the view), Discard and (live) Reload and Auto apply in the top bar, the players of a live
// world in the view with their names and Go to, and a note about the locations of the area.
public sealed partial class MainWindow
{
	internal Button AreaWest { get; } = NavButton("◀", "West");
	internal Button AreaNorth { get; } = NavButton("▲", "North");
	internal Button AreaSouth { get; } = NavButton("▼", "South");
	internal Button AreaEast { get; } = NavButton("▶", "East");
	internal Button FollowButton { get; } = new Button { Content = "Follow", FontSize = 12.5 }.Classed("ghost");
	internal Button DiscardButton { get; } = new Button { Content = "Discard", FontSize = 12.5, IsEnabled = false }.Classed("ghost");
	internal Button ReloadButton { get; } = new Button { Content = Icons.With("reload", "Reload"), FontSize = 12.5, IsVisible = false }.Classed("ghost");
	internal CheckBox AutoApplyBox { get; } = new CheckBox { Content = "Auto", FontSize = 12, IsVisible = false, VerticalAlignment = VerticalAlignment.Center }.Classed("switch");
	internal StackPanel AreaNav { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
	internal TextBlock LocationText { get; } = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.Parse("#f3d29b")) };
	internal Border LocationNote { get; private set; } = null!;
	internal ComboBox PlayerBox { get; } = new() { FontSize = 12, MinWidth = 130 };
	internal Button GoPlayerButton { get; } = new Button { Content = "Go", FontSize = 12 };
	internal StackPanel PlayersRow { get; } = new() { Spacing = 2, IsVisible = false };
	internal Canvas PlayerLabels { get; } = new() { IsHitTestVisible = false };
	// The live players: name and world position, as the game last said.
	internal IReadOnlyList<(string Name, Vector3 World)> LivePlayers { get; private set; } = Array.Empty<(string, Vector3)>();
	private readonly DispatcherTimer _playersTimer = new() { Interval = TimeSpan.FromSeconds(2) };
	private readonly DispatcherTimer _labelsTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
	private readonly DispatcherTimer _followTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
	private string _followSaid = "";
	internal AppSettings Settings => _settings;
	// Writes the settings (Auto, Follow); tests keep them in memory, so no other window reads them.
	internal Action<AppSettings> SaveSettings { get; set; } = s => s.Save();
	private bool _applying, _applyAgain;

	private static Button NavButton(string arrow, string where)
	{
		var b = new Button { Content = arrow, FontSize = 11, Padding = new Thickness(6, 3) }.Classed("ghost");
		ToolTip.SetTip(b, $"Move the area one zone (64 m) {where.ToLowerInvariant()}");
		return b;
	}

	// Set up once, from the constructor (after the top bar).
	private void SetUpEditorWorld()
	{
		AreaNav.Children.Add(new TextBlock { Text = "Area", FontSize = 11, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
		foreach (var (b, dx, dz) in new[] { (AreaWest, -1, 0), (AreaNorth, 0, 1), (AreaSouth, 0, -1), (AreaEast, 1, 0) })
		{
			b.Click += async (_, _) => await MoveArea(dx, dz);
			AreaNav.Children.Add(b);
		}
		AreaNav.Children.Add(FollowButton);
		ToolTip.SetTip(FollowButton, "Follow the view: when you look near the edge of the area, it moves there by itself");
		FollowButton.Classes.Set("on", _settings.AreaFollow);
		FollowButton.Click += (_, _) => SetFollow(!_settings.AreaFollow);
		_followTimer.Tick += async (_, _) => await CheckFollow();
		_followTimer.Start();

		ToolTip.SetTip(DiscardButton, "Throw away the changes not saved (or not applied) yet: the last steps in History are undone");
		DiscardButton.Click += async (_, _) => await DiscardHere();
		ToolTip.SetTip(ReloadButton, "Load the world again from the game");
		ReloadButton.Click += async (_, _) => await ReloadHere();
		ToolTip.SetTip(AutoApplyBox, "Apply every stroke and undo to the game right away");
		AutoApplyBox.IsChecked = _settings.AutoApply;
		AutoApplyBox.IsCheckedChanged += async (_, _) =>
		{
			_settings.AutoApply = AutoApplyBox.IsChecked == true;
			SaveSettings(_settings);
			if (_settings.AutoApply && _session is { IsLive: true } s && s.Pending is not (0, 0, 0, 0))
			{
				await ApplyNow();
			}
		};

		var close = new Button { Content = Icons.Make("close", 12), Padding = new Thickness(4) }.Classed("ghost");
		close.Click += (_, _) => LocationNote.IsVisible = false;
		Grid.SetColumn(close, 1);
		LocationNote = new Border
		{
			Background = new SolidColorBrush(Color.Parse("#2c2416")),
			BorderBrush = new SolidColorBrush(Color.Parse("#7a5a2a")),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(8),
			Padding = new Thickness(10, 6),
			Margin = new Thickness(10, 0, 10, 58),
			MaxWidth = 420,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Bottom,
			IsVisible = false,
			Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6, Children = { LocationText, close } },
		};

		GoPlayerButton.Click += async (_, _) => await GoToPlayer(PlayerBox.SelectedItem as string);
		_playersTimer.Tick += async (_, _) => await PollPlayers();
		_labelsTimer.Tick += (_, _) => PlaceLabels();
	}

	// The View panel's Go to a player (live worlds).
	private Control PlayersControl()
	{
		PlayersRow.Children.Add(new TextBlock { Text = "PLAYERS", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Ui.Muted, Margin = new Thickness(0, 8, 0, 2) });
		PlayersRow.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "Go to", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, PlayerBox, GoPlayerButton } });
		return PlayersRow;
	}

	// After the top bar's save line is updated: the editor's own buttons.
	private void UpdateEditorWorld(EditSession s, bool dirty)
	{
		DiscardButton.IsEnabled = dirty;
		ReloadButton.IsVisible = AutoApplyBox.IsVisible = s.IsLive;
	}

	// An area was opened: the area buttons, the locations note, the players.
	private void EditorOpened(WorldScene scene)
	{
		AreaNav.IsVisible = scene.Owner != null;
		int n = Overlays.LocationsNear(scene);
		LocationText.Text = n == 0 ? "" : $"{n} location(s) here. The ground already includes the flattening the game does around them (turn on Location markers in View to see where).";
		LocationNote.IsVisible = n > 0;
		_followSaid = "";
		bool live = scene.Owner?.IsLive == true;
		PlayersRow.IsVisible = live;
		if (live)
		{
			_ = PollPlayers();
			_playersTimer.Start();
			_labelsTimer.Start();
		}
		else
		{
			_playersTimer.Stop();
			_labelsTimer.Stop();
			SetPlayers(Array.Empty<(string, Vector3)>());
		}
	}

	// ---- Moving the area. The edits and the history stay with the world, the view comes along.

	internal async Task MoveArea(int dx, int dz)
	{
		if (_view.Scene is not { Owner: not null } s)
		{
			return;
		}
		int mid = s.Size / 2;
		await OpenAreaKeepingView(s.X0 + mid + dx, s.Z0 + mid + dz, s.Size);
	}

	private async Task OpenAreaKeepingView(int zx, int zz, int size)
	{
		var cam = _view.WorldCamera;
		await EditArea(zx, zz, size);
		if (_view.Scene is { } ns && ns.X0 + ns.Size / 2 == zx && ns.Z0 + ns.Size / 2 == zz && cam != null)
		{
			_view.WorldCamera = cam;
			}
	}

	internal void SetFollow(bool on)
	{
		_settings.AreaFollow = on;
		SaveSettings(_settings);
		FollowButton.Classes.Set("on", on);
		_message.Text = on ? "Follow: the area moves by itself when you look near its edge." : "Follow is off: move the area with the arrows.";
		_followSaid = "";
	}

	// What stops the area from moving now (Follow waits): something unfinished would be lost.
	internal string? MoveBlocker()
	{
		if (_view.Selected.Count > 0)
		{
			return "objects are selected (Esc clears)";
		}
		if (_session?.Stroking == true)
		{
			return "a stroke is on";
		}
		if (_view.Path.Points.Count > 0)
		{
			return "a path is drawn (apply or clear it)";
		}
		if (_applying)
		{
			return "changes are still being sent";
		}
		return null;
	}

	// Follow: when the view's centre comes within 12 m of the area's edge, the area moves to the zone
	// under it (one zone on when that is the middle one already). The zone it moves to, or null.
	internal async Task<(int X, int Z)?> CheckFollow()
	{
		if (!_settings.AreaFollow || _pages.Content != _editorPage || _view.Scene is not { Owner: not null } s)
		{
			return null;
		}
		var t = _view.Camera.Target;
		bool nearX = MathF.Abs(t.X) > (s.W - 1) / 2f - 12, nearZ = MathF.Abs(t.Z) > (s.H - 1) / 2f - 12;
		if (!nearX && !nearZ)
		{
			_followSaid = "";
			return null;
		}
		if (MoveBlocker() is string why)
		{
			if (_followSaid != why)
			{
				_message.Text = $"Follow waits: {why}.";
			}
			_followSaid = why;
			return null;
		}
		float wx = t.X + s.Cx, wz = -t.Z + s.Cz;
		int czx = s.X0 + s.Size / 2, czz = s.Z0 + s.Size / 2;
		int zx = (int)MathF.Floor((wx + 32) / 64), zz = (int)MathF.Floor((wz + 32) / 64);
		if (nearX && zx == czx) zx += Math.Sign(wx - czx * 64);
		if (nearZ && zz == czz) zz += Math.Sign(wz - czz * 64);
		if (!nearX) zx = czx;
		if (!nearZ) zz = czz;
		if (zx == czx && zz == czz)
		{
			return null;
		}
		_message.Text = "Moving the area…";
		await OpenAreaKeepingView(zx, zz, s.Size);
		return (zx, zz);
	}

	// ---- Discard and Reload, here in the editor.

	// Undoes the steps not saved or applied yet, keeping the view, the tool and the rest of the history.
	// Pending changes with no step here (made in another area, or before) need the world read again.
	internal async Task DiscardHere()
	{
		if (_session is not { } s)
		{
			return;
		}
		_view.SelectTool.Commit();
		if (s.Pending is (0, 0, 0, 0))
		{
			_message.Text = "There is nothing to discard.";
			return;
		}
		int steps = s.UnappliedSteps;
		if (!await Ask("Discard", $"Discard the {(s.IsLive ? "changes not applied yet" : "unsaved changes")} ({Describe(s.Pending)})?{(steps > 0 ? $" The last {steps} step(s) in History are undone." : "")}", "Discard", "Keep them"))
		{
			return;
		}
		s.UndoUnapplied();
		UpdateSaveBar();
		History.Refresh();
		if (s.Pending is (0, 0, 0, 0))
		{
			_message.Text = "Discarded. Everything else is kept.";
			return;
		}
		if (s.Scene.Owner is not { } w || !await Ask("Discard", "Some of the changes waiting have no step in this area's history (made in another area, or earlier), so they can only be discarded by reading the world again. Read it again now?", "Read it again", "Keep them"))
		{
			return;
		}
		Busy("Reading the world again…");
		try
		{
			await Task.Run(w.Discard);
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException)
		{
			_message.Text = "Could not read the world again: " + ex.Message;
			Busy(null);
			return;
		}
		Busy(null);
		await ReopenArea(s.Scene);
		_message.Text = "Discarded: the world was read again.";
	}

	// Live: the world loaded again from the game (asks first when something waits).
	internal async Task ReloadHere()
	{
		if (_session is not { IsLive: true, Scene.Owner: { } w } s)
		{
			return;
		}
		if (s.Pending is not (0, 0, 0, 0) && !await Ask("Reload", "Reload the world from the game? Your changes not applied yet are dropped.", "Reload", "Keep them"))
		{
			return;
		}
		Busy("Loading the world from the game…");
		try
		{
			await w.Reload();
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException or System.Text.Json.JsonException)
		{
			Busy(null);
			_message.Text = "Could not reach the game: " + ex.Message;
			return;
		}
		Busy(null);
		await ReopenArea(s.Scene);
		_message.Text = "The world was loaded again from the game.";
	}

	// The same area again (the world was read again), the view where it was.
	private async Task ReopenArea(WorldScene old)
	{
		_session = null;
		await OpenAreaKeepingView(old.X0 + old.Size / 2, old.Z0 + old.Size / 2, old.Size);
	}

	// ---- Live: Apply (the button, and every change when Auto is on). Calls while one runs are merged.

	internal async Task ApplyNow()
	{
		if (_applying)
		{
			_applyAgain = true;
			return;
		}
		_applying = true;
		try
		{
			do
			{
				_applyAgain = false;
				if (_session is not { IsLive: true } s)
				{
					break;
				}
				SaveButton.IsEnabled = false;
				_message.Text = "Applying to the running game…";
				var o = await s.ApplyLive();
				_message.Text = o.Message;
				UpdateSaveBar();
				History.Refresh();
			}
			while (_applyAgain);
		}
		finally
		{
			_applying = false;
		}
	}

	// A change made with Auto on: to the game at once.
	private void AutoApply(EditSession s)
	{
		if (_settings.AutoApply && s == _session && s.IsLive && !s.Stroking)
		{
			_ = ApplyNow();
		}
	}

	// ---- Live players.

	internal async Task PollPlayers()
	{
		if (_view.Scene?.Owner?.Live is not { } live)
		{
			return;
		}
		try
		{
			var json = System.Text.Json.Nodes.JsonNode.Parse(await live.Players())?.AsArray();
			var players = json?.Where(p => p?["name"] != null).Select(p => ((string)p!["name"]!, new Vector3((float)(p["x"] ?? 0), (float)(p["y"] ?? 0), (float)(p["z"] ?? 0)))).ToList() ?? new();
			SetPlayers(players);
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException or FormatException)
		{
			// The game does not answer right now: the last positions stay.
		}
	}

	internal void SetPlayers(IReadOnlyList<(string Name, Vector3 World)> players)
	{
		LivePlayers = players;
		_view.Players = players;
		var names = players.Select(p => p.Name).ToList();
		string? chosen = PlayerBox.SelectedItem as string;
		if (!(PlayerBox.ItemsSource is List<string> old && old.SequenceEqual(names)))
		{
			PlayerBox.ItemsSource = names;
			PlayerBox.SelectedItem = chosen != null && names.Contains(chosen) ? chosen : names.FirstOrDefault();
		}
		GoPlayerButton.IsEnabled = names.Count > 0;
		ToolTip.SetTip(PlayerBox, string.Join("\n", players.Select(p => $"{p.Name}: {p.World.X:0}, {p.World.Z:0}")));
		PlaceLabels();
	}

	// The players' names over their posts (4 m above their feet), where the camera sees them.
	internal void PlaceLabels()
	{
		PlayerLabels.Children.Clear();
		if (_view.Scene is not { } s || LivePlayers.Count == 0 || _pages.Content != _editorPage)
		{
			return;
		}
		var size = _view.Bounds.Size;
		foreach (var (name, w) in LivePlayers)
		{
			var q = Vector4.Transform(new Vector4(w.X - s.Cx, w.Y + 4, -(w.Z - s.Cz), 1), _view.ViewProj);
			if (q.W <= 0.01f)
			{
				continue;
			}
			double x = (q.X / q.W + 1) / 2 * size.Width, y = (1 - q.Y / q.W) / 2 * size.Height;
			if (x < 0 || y < 0 || x > size.Width || y > size.Height)
			{
				continue;
			}
			var label = new TextBlock { Text = name, FontSize = 13, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#bfeaff")), Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), Padding = new Thickness(4, 1) };
			Canvas.SetLeft(label, x - 4 * name.Length - 4);
			Canvas.SetTop(label, y - 10);
			PlayerLabels.Children.Add(label);
		}
	}

	// Go to a player: right here when they are in the area, else the area around them.
	internal async Task GoToPlayer(string? name)
	{
		if (name == null || _view.Scene is not { } s || LivePlayers.FirstOrDefault(p => p.Name == name) is not { Name: not null } p)
		{
			return;
		}
		float gx = p.World.X - (s.X0 * 64 - 32), gz = p.World.Z - (s.Z0 * 64 - 32);
		if (!(gx > 2 && gz > 2 && gx < s.W - 3 && gz < s.H - 3) && s.Owner != null)
		{
			int zx = (int)MathF.Floor((p.World.X + 32) / 64), zz = (int)MathF.Floor((p.World.Z + 32) / 64);
			await EditArea(zx, zz, s.Size);
		}
		_view.Focus(p.World);
		_message.Text = $"Looking at {name}.";
	}
}
