using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The prototype's window: the 3D view filling it, and a small panel with the frame rate, what is
// loaded, and the Record frame rates switch.
public sealed partial class MainWindow : Window
{
	private readonly GlView _view = new();
	private readonly TextBlock _fps = new() { FontSize = 12.5, TextWrapping = TextWrapping.Wrap }, _info = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
	private readonly PerfLog _perf = new();
	private readonly TextBlock _eye = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(143, 240, 180)), TextWrapping = TextWrapping.Wrap };
	// Help (?, F3): the controls, the graphics card, what was loaded, the frame rate.
	internal Border HelpCard { get; private set; } = null!;
	internal Button HelpButton { get; } = new Button { Content = Icons.Make("help", 18), Padding = new Thickness(7, 5) }.Classed("ghost");
	internal Button ViewButton { get; } = new Button { Content = Icons.With("view", "View"), FontSize = 12.5 }.Classed("ghost");
	// The top bar's names: the world, and the area open.
	private readonly TextBlock _title = new() { FontSize = 14, FontWeight = FontWeight.SemiBold, Text = "Valheim World Editor", TextTrimming = TextTrimming.CharacterEllipsis };
	private readonly TextBlock _subtitle = new() { FontSize = 12, Foreground = Ui.Muted, TextTrimming = TextTrimming.CharacterEllipsis };
	private readonly Border _liveBadge = new()
	{
		IsVisible = false,
		CornerRadius = new CornerRadius(999),
		BorderThickness = new Thickness(1),
		BorderBrush = new SolidColorBrush(Color.Parse("#2a6a4a")),
		Background = new SolidColorBrush(Color.Parse("#13261d")),
		Padding = new Thickness(9, 4),
		VerticalAlignment = VerticalAlignment.Center,
		Child = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 6,
			Children =
			{
				new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Color.Parse("#4be08a")), VerticalAlignment = VerticalAlignment.Center, BoxShadow = BoxShadows.Parse("0 0 6 0 #4be08a") },
				new TextBlock { Text = "LIVE", FontSize = 11.5, FontWeight = FontWeight.Bold, LetterSpacing = 0.7, Foreground = new SolidColorBrush(Color.Parse("#8ff0b4")) },
			},
		},
	};
	private readonly Border _pendingPill = new() { CornerRadius = new CornerRadius(999), BorderThickness = new Thickness(1), Padding = new Thickness(9, 4), VerticalAlignment = VerticalAlignment.Center };
	private readonly TextBlock _selection = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(224, 166, 75)), TextWrapping = TextWrapping.Wrap };
	private ModelStore? _models;
	// The file pickers' patterns.
	private static readonly string[] BlueprintPatterns = { "*.blueprint", "*.vbuild" }, PicturePatterns = { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp" }, PngPatterns = { "*.png" };
	private string ThingName(WorldScene.Thing t) => _models?.NameOf(t.Prefab) ?? TerrainEditor.Terrain.PrefabCatalog.DisplayName(t.Prefab) ?? t.Prefab.ToString();

	public GlView View => _view;
	internal ToolPanel Tools { get; } = new();
	internal SelectPanel SelectPanel { get; }
	internal MeasurePanel MeasurePanel { get; }
	internal ShapePanel ShapePanel { get; } = new();
	internal MountainPanel MountainPanel { get; } = new();
	internal ScriptPanel ScriptPanel { get; } = new();
	// Stops the running script (null: none runs).
	private Action? _stopScript;
	internal Mask Mask { get; } = new();
	internal MaskPanel MaskPanel { get; }
	internal PathPanel PathPanel { get; }
	internal AreaPanel AreaPanel { get; }
	internal PastePanel PastePanel { get; }
	internal PlaceTool PlaceTool { get; } = new();
	internal PlaceInput PlaceInput { get; }
	internal PlacePanel PlacePanel { get; }

	// The save bar (top middle): what is waiting to be saved, undo, redo, Save, and the last message.
	private readonly TextBlock _pending = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Text = "All saved" };
	// The status bar's message (the driver clears it before a documentation picture).
	internal string StatusMessage { get => _message.Text ?? ""; set => _message.Text = value; }
	private readonly TextBlock _message = new() { FontSize = 12.5, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
	internal Button UndoButton { get; } = new Button { Content = Icons.Make("undo", 18), IsEnabled = false, Padding = new Thickness(7, 5) }.Classed("ghost");
	internal Button RedoButton { get; } = new Button { Content = Icons.Make("redo", 18), IsEnabled = false, Padding = new Thickness(7, 5) }.Classed("ghost");
	internal Button SaveButton { get; } = new Button { Content = "Save to world", FontSize = 12.5, IsEnabled = false }.Classed("primary");
	internal Button HistoryButton { get; } = new Button { Content = Icons.With("history", "History"), FontSize = 12.5 }.Classed("ghost");
	internal HistoryPanel History { get; }
	internal Button MapButton { get; } = new Button { Content = Icons.With("back", "Map"), FontSize = 12.5, IsVisible = false }.Classed("ghost");

	// ---- Pages: the start page, the world map, and the 3D editor of an area.
	private readonly ContentControl _pages = new();
	private Control _editorPage = null!;
	private readonly Border _busy = new() { Background = new SolidColorBrush(Color.FromArgb(200, 10, 12, 16)), IsVisible = false };
	private readonly TextBlock _busyText = new() { FontSize = 16, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
	private WorldSession? _world;
	internal WorldSession? World => _world;
	private StartPage? _start;
	private MapPage? _map;
	internal StartPage? StartPage => _start;
	internal MapPage? MapPage => _map;
	internal bool MapShown => _map != null && _pages.Content == _map.View;
	// The transparent control over the 3D view that takes the mouse (the test driver sends it input).
	internal Control Surface { get; private set; } = null!;
	private readonly AppSettings _settings = AppSettings.Load();

	private static string Describe((int Zones, int Deleted, int Added, int Resets) p)
	{
		var (z, d, a, r) = p;
		return string.Join(", ", new[] { z > 0 ? $"{z} zone(s) of ground" : "", d > 0 ? $"{d} deleted" : "", a > 0 ? $"{a} added" : "", r > 0 ? $"{r} zone reset" : "" }.Where(x => x != ""));
	}

	private void Busy(string? text)
	{
		_busyText.Text = text ?? "";
		_busy.IsVisible = text != null;
	}

	// The start page: how to edit, and which world.
	internal void ShowStart(string? error = null)
	{
		_map?.Stop();
		if (_start == null || error != null)
		{
			_start?.Stop();
			_start = new StartPage(_settings, error);
			_start.OpenRequested += async (open, what) => await OpenWorld(open, what);
			_start.OpenUrl = uri => Launcher.LaunchUriAsync(uri);
			_start.SettingsRequested += async () => { if (await SettingsDialog.Show(this, _settings)) _start!.SetMode(_start.Mode); };
			_start.Confirm = text => Dialogs.Ask(this, "Valheim World Editor", text, "Yes");
			_start.PickFolder = async title =>
			{
				var picked = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions { Title = title });
				return picked.Count > 0 && picked[0].Path.IsFile ? picked[0].Path.LocalPath : null;
			};
			_start.PickFile = async title =>
			{
				var picked = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = title });
				return picked.Count > 0 && picked[0].Path.IsFile ? picked[0].Path.LocalPath : null;
			};
		}
		else
		{
			_start.SetMode(_start.Mode);
		}
		Title = $"Valheim World Editor {BuildInfo.Version}";
		_pages.Content = _start.View;
	}

	// Opens a world (offline or live), then shows its map.
	internal async Task OpenWorld(Func<Task<WorldSession>> open, string what)
	{
		Busy(what);
		try
		{
			_world = await open();
		}
		catch (Exception ex)
		{
			Busy(null);
			Tunnel.Close();
			ShowStart($"Could not open the world: {ex.Message}");
			return;
		}
		finally
		{
			Busy(null);
		}
		_start?.Stop();
		ShowMap();
	}

	// The world map (what is not saved stays pending).
	internal void ShowMap()
	{
		if (_world == null)
		{
			ShowStart();
			return;
		}
		_view.SelectTool.Commit();
		KeepHistory();
		_playersTimer.Stop();
		_labelsTimer.Stop();
		PlayerLabels.Children.Clear();
		if (_map == null)
		{
			_map = new MapPage();
			_map.StartView = () => Prefs.TryGet("map.view", out float[]? v) && v is { Length: 3 } && v.All(float.IsFinite) && v[2] > 0 ? (v[0], v[1], Math.Clamp(v[2], 0.25f, 40f)) : null;
			_map.Map.ViewChanged += () => Prefs.Set("map.view", new[] { _map.Map.Center.X, _map.Map.Center.Y, _map.Map.MetersPerPixel });
			_map.BackToWorlds += async () => await LeaveWorld();
			_map.EditRequested += async (x, z, size) => await EditArea(x, z, size);
			_map.SaveRequested += async () => await SaveWorld();
			_map.DiscardRequested += async () => await DiscardWorld();
			_map.ReloadRequested += async () => await ReloadWorld();
			_map.Confirm = text => Dialogs.Ask(this, "Valheim World Editor", text, "Yes");
			_map.Tell = text => Dialogs.Tell(this, "Valheim World Editor", text);
		}
		Title = $"{_world.World.Name} · Valheim World Editor {BuildInfo.Version}";
		_map.Show(_world);
		_pages.Content = _map.View;
	}

	// Leaving an area: its history stays with the world, for the next area opened.
	private void KeepHistory()
	{
		if (_session is { Scene.Owner: { } owner } s && _pages.Content == _editorPage)
		{
			owner.History = s.Export();
		}
	}

	// An area of the world in the 3D editor.
	internal async Task EditArea(int x, int z, int size)
	{
		if (_world is not { } world)
		{
			return;
		}
		_view.SelectTool.Commit();
		KeepHistory();
		Busy($"Loading {size} × {size} zones around zone {x}, {z}…");
		try
		{
			var scene = await Task.Run(() => WorldScene.Load(world, x, z, size));
			await ShowEditor(scene);
			// Found by the map's search: selected, the camera on it (items and texts: in the inspector).
			if (_map?.Target is var (id, inspect) && _map.Spot == (x, z))
			{
				int i = scene.Things.FindIndex(t => t.Id == id && !t.Gone);
				if (i >= 0)
				{
					_view.Focus(scene.Things[i].Position);
					_view.Select(new[] { i });
					if (inspect)
					{
						Inspect();
					}
				}
			}
		}
		catch (Exception ex)
		{
			_message.Text = "Could not open that area: " + ex.Message;
		}
		finally
		{
			Busy(null);
		}
	}

	internal async Task ShowEditor(WorldScene scene)
	{
		_models ??= await Task.Run(ModelStore.Open);
		// Lines and shapes drawn in the last area are left behind.
		_view.Path.Clear();
		_view.Area.Clear();
		_view.Tape.Clear();
		PlaceTool.ClearShape();
		Inspector.Close();
		_info.Text = scene.LoadInfo + (_models == null ? "\nNo game models copied yet: boxes stand in until the game's look is copied (see the start page)." : "");
		_view.Show(scene, _models);
		if (scene.Session != null)
		{
			Edit(scene.Session);
		}
		MapButton.IsVisible = scene.Owner != null && !Options.Direct;
		_title.Text = scene.Name;
		int mid = scene.Size / 2;
		_subtitle.Text = $"{scene.Size} × {scene.Size} zones around zone {scene.X0 + mid}, {scene.Z0 + mid} · {scene.Things.Count(t => !t.Gone):N0} objects";
		Title = $"{scene.Name} · Valheim World Editor {BuildInfo.Version}";
		_pages.Content = _editorPage;
		EditorOpened(scene);
	}

	// Map: Save (or Apply live) for the whole world, after asking.
	internal async Task SaveWorld()
	{
		if (_world is not { } w)
		{
			return;
		}
		if (w.IsLive)
		{
			Busy("Applying to the running game…");
			var o = await w.ApplyLive();
			Busy(null);
			await Tell(o.Message);
		}
		else
		{
			string what = Describe(w.Pending);
			if (!await ConfirmSave($"Write {what} into the world files?\n\nWorld folder: {Ui.Tilde(w.World.Directory)}\n\n"
				+ "• Like Apply live: the world stays open with its history; undo a step and save again to take it back. The save it was opened from is kept until you leave the world.\n"
				+ "• Valheim (server or game) must NOT be running with this world, or it will overwrite these changes when it saves.\n"
				+ "• Test on a copy first: open it as a local world, or upload it to a test server."))
			{
				return;
			}
			Busy("Saving…");
			var o = await Task.Run(w.Save);
			Busy(null);
			string msg = o.Message;
			if (o.Saved is { } r)
			{
				if (r.Skipped.Count > 0) msg += "\n\nNot saved:\n• " + string.Join("\n• ", r.Skipped);
			}
			await Tell(msg);
		}
		_map?.Show(w);
	}

	internal async Task DiscardWorld()
	{
		if (_world is not { } w || !await Ask("Discard", $"Discard the unsaved changes ({Describe(w.Pending)})? The world is read again{(w.IsLive ? " from the game" : " from disk")}.", "Discard", "Keep them"))
		{
			return;
		}
		Busy("Reading the world again…");
		await Task.Run(w.Discard);
		Busy(null);
		_map?.Show(w);
	}

	internal async Task ReloadWorld()
	{
		if (_world is not { IsLive: true } w)
		{
			return;
		}
		Busy("Loading the world from the game…");
		try
		{
			await w.Reload();
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
		{
			_message.Text = "Could not reach the game: " + ex.Message;
		}
		Busy(null);
		_map?.Show(w);
	}

	// Worlds: back to the start page (asks first when something is not saved).
	internal async Task LeaveWorld()
	{
		if (_world is { } w && w.Pending is not (0, 0, 0, 0)
			&& !await Ask("Leave the world", $"{(w.IsLive ? "Not applied" : "Unsaved")}: {Describe(w.Pending)}. Leave the world without {(w.IsLive ? "applying" : "saving")} them?", "Leave anyway", "Stay"))
		{
			return;
		}
		_world?.Dispose();
		_world = null;
		_session = null;
		Tunnel.Close();
		ShowStart();
	}
	internal InspectorPanel Inspector { get; }
	internal BlueprintsPanel Blueprints { get; }
	private Control _viewPanel = null!;
	internal Control ViewPanelCard => _viewPanel;
	internal TextBlock PendingText => _pending;
	internal TextBlock MessageText => _message;
	// Asks before writing into the world (replaced by tests).
	internal Func<string, Task<bool>> ConfirmSave { get; set; }
	// A yes / no question (title, text, yes, no): Discard, Leave, Quit with unsaved changes. Tests answer it.
	internal Func<string, string, string, string, Task<bool>> Ask { get; set; }
	internal Func<string, Task> Tell { get; set; }
	private EditSession? _session;
	internal EditSession? Session => _session;
	private bool _closeAnyway;

	// The test driver's quit: what is pending is dropped without asking.
	internal void CloseWithoutAsking()
	{
		_closeAnyway = true;
		Close();
	}

	// Starts editing a loaded scene (also used by tests with a scene of their own).
	internal void Edit(EditSession session)
	{
		_session = session;
		session.Brush = Tools.Brush;
		MountainPanel.FitTo(session.Ground.W);
		if (MountainPanel.Spec.Radius * 1.15f > (session.Ground.W - 1) / 2f - 2)
		{
			MountainPanel.Randomize();
		}
		session.Ground.NoLimit = Tools.NoLimitBox.IsChecked == true;
		if (session.Scene.World is { } bw)
		{
			// The builder chosen for this world before, else the world's main builder.
			TerrainEditor.Save.WorldSave.Builder = PlacePanel.Memory.Builders.TryGetValue(bw.Name, out long known) ? known : Builders.Default(bw);
			FillBuilders();
		}
		session.Mask = Mask;
		// New objects keep a green marker until saved, or until the game has them (live).
		_view.InGame = id => session.Scene.Owner?.IsLive == true && session.Scene.Owner.LiveSync.IsLive(id);
		// What is placed stays visible: its kinds switched off in View are switched on.
		session.ThingsAdded += indices => Dispatcher.UIThread.Post(() => ShowKindsOf(session.Scene, indices));
		session.Changed += () => Dispatcher.UIThread.Post(() =>
		{
			UpdateSaveBar();
			History.Refresh();
			// The ground changed: the Area tool's cut and fill follows.
			if (Tools.Mode == ToolMode.Area)
			{
				AreaPanel.RefreshVolume();
			}
		});
		session.EditMade += () => Dispatcher.UIThread.Post(() => AutoApply(session));
		// Saved: the objects were read again, with new indices.
		session.ThingsReset += () => Dispatcher.UIThread.Post(() => { if (Inspector.IsOpen) Inspector.Close(); });
		// The kinds the Select tool can replace with.
		var kinds = session.Scene.World?.Creatable.Where(p => NameOfPrefab(p) != null).OrderBy(p => NameOfPrefab(p), StringComparer.OrdinalIgnoreCase).ToList() ?? new();
		SelectPanel.ReplaceKinds = kinds;
		SelectPanel.ReplaceBox.ItemsSource = kinds.Select(p => NameOfPrefab(p)!).ToList();
		SelectPanel.FillSaved();
		UpdateSaveBar();
	}

	private void UpdateSaveBar()
	{
		var s = _session;
		if (s == null)
		{
			return;
		}
		_pending.Text = s.PendingText;
		var (z, d, a, r) = s.Pending;
		bool dirty = z + d + a + r > 0;
		SaveButton.IsEnabled = dirty;
		SaveButton.Content = s.IsLive ? "Apply live" : "Save to world";
		SaveButton.Tip(s.IsLive ? "top.apply" : "top.save");
		// The pill: amber while something waits, grey when all is saved.
		_pendingPill.Background = dirty ? new SolidColorBrush(Color.Parse("#2c2416")) : Brushes.Transparent;
		_pendingPill.BorderBrush = dirty ? new SolidColorBrush(Color.Parse("#7a5a2a")) : Ui.Line;
		_pending.Foreground = dirty ? new SolidColorBrush(Color.Parse("#f3d29b")) : Ui.Muted;
		_liveBadge.IsVisible = s.IsLive;
		UndoButton.IsEnabled = s.CanUndo;
		RedoButton.IsEnabled = s.CanRedo;
		ToolTip.SetTip(UndoButton, s.UndoLabel is string u ? $"Undo {u} (Ctrl+Z)" : "Nothing to undo");
		ToolTip.SetTip(RedoButton, s.RedoLabel is string rl ? $"Redo {rl} (Ctrl+Shift+Z)" : "Nothing to redo");
		UpdateEditorWorld(s, dirty);
	}

	internal void Undo()
	{
		_view.SelectTool.Commit();
		_session?.Undo();
		UpdateSaveBar();
	}

	internal void Redo()
	{
		_view.SelectTool.Commit();
		_session?.Redo();
		UpdateSaveBar();
	}

	// Like the web editor's Save: asks first, writes (with a backup), then says what was done.
	internal async Task Save()
	{
		var s = _session;
		if (s == null)
		{
			return;
		}
		_view.SelectTool.Commit();
		var (z, d, a, r) = s.Pending;
		if (z + d + a + r == 0)
		{
			await Tell("There are no unsaved changes.");
			return;
		}
		if (s.IsLive)
		{
			await ApplyNow();
			return;
		}
		string what = s.PendingText.Replace("Unsaved: ", "");
		if (!await ConfirmSave($"Write {what} into the world files?\n\nWorld folder: {Ui.Tilde(s.Scene.World.Directory)}\n\n"
			+ "• Like Apply live: the world stays open with its history; undo a step and save again to take it back. The save it was opened from is kept until you leave the world.\n"
			+ "• Valheim (server or game) must NOT be running with this world, or it will overwrite these changes when it saves.\n"
			+ "• Test on a copy first: open it as a local world, or upload it to a test server."))
		{
			return;
		}
		SaveButton.IsEnabled = false;
		_message.Text = "Saving…";
		try
		{
			var res = await Task.Run(s.Save);
			string msg = res.Message;
			if (res.Skipped.Count > 0)
			{
				msg += "\n\nNot saved:\n• " + string.Join("\n• ", res.Skipped);
			}
			_message.Text = res.Message;
			Options.Say("Save: " + res.Message);
			await Tell(msg);
		}
		catch (Exception ex)
		{
			_message.Text = "Could not save: " + ex.Message;
		}
		UpdateSaveBar();
	}

	internal string? NameOfPrefab(int prefab) => _models?.NameOf(prefab) ?? TerrainEditor.Terrain.PrefabCatalog.DisplayName(prefab);

	// A kind's model box in its own frame, in Unity's axes (the models are stored with z mirrored) and
	// scaled like the model; null while unknown.
	private readonly Dictionary<string, (System.Numerics.Vector3, System.Numerics.Vector3)?> _boxes = new();
	private (System.Numerics.Vector3 Min, System.Numerics.Vector3 Max)? ModelBox(string name)
	{
		if (_boxes.TryGetValue(name, out var known))
		{
			return known;
		}
		if (_models?.LoadModel(name) is not { } model)
		{
			return _boxes[name] = null;
		}
		System.Numerics.Vector3 lo = new(float.MaxValue), hi = new(float.MinValue);
		foreach (var part in model.Parts)
		{
			if (_models.LoadMesh(part.Mesh) is { } md)
			{
				var (a, b) = Picking.Transform(md.Bounds.Min, md.Bounds.Max, part.Matrix);
				lo = System.Numerics.Vector3.Min(lo, a);
				hi = System.Numerics.Vector3.Max(hi, b);
			}
		}
		if (lo.X > hi.X)
		{
			return _boxes[name] = null;
		}
		var sc = model.RootScale;
		return _boxes[name] = (new System.Numerics.Vector3(lo.X * sc.X, lo.Y * sc.Y, -hi.Z * sc.Z), new System.Numerics.Vector3(hi.X * sc.X, hi.Y * sc.Y, -lo.Z * sc.Z));
	}

	// Ctrl+C: the Area selection (ground and shown objects), or the Select tool's objects.
	internal void Copy()
	{
		if (_session is not { } s)
		{
			return;
		}
		CopyData? clip;
		if (Tools.Mode == ToolMode.Select)
		{
			_view.SelectTool.Commit();
			clip = CopyData.FromThings(s.Scene, _view.Selected.ToList(), NameOfPrefab, _view.GroundHeight);
			if (clip == null)
			{
				_message.Text = "Select objects to copy first.";
				return;
			}
		}
		else
		{
			var poly = _view.Area.Polygon();
			var inside = poly == null ? new List<int>() : AreaPanel.ThingsInside(poly, chosenKindsOnly: false);
			clip = CopyData.FromArea(_view.Area, s.Ground, s.Scene, inside, NameOfPrefab);
			if (clip == null)
			{
				_message.Text = "Select an area first.";
				return;
			}
		}
		_view.Paste.Clip = clip;
		_view.Paste.Notify();
		_message.Text = Tools.Mode == ToolMode.Select ? $"Copied {clip.Objects.Count} object(s). Ctrl+V to paste (R turns, F mirrors)."
			: $"Copied {clip.W} × {clip.H} m and {clip.Objects.Count} object(s). Ctrl+V to paste, here or in another area.";
	}

	// Ctrl+V: the Paste tool.
	internal void StartPaste()
	{
		if (_view.Paste.Clip == null)
		{
			_message.Text = "Copy an area first (Area tool, Ctrl+C).";
			return;
		}
		_view.SelectTool.Commit();
		Tools.ChooseMode(ToolMode.Paste);
		PastePanel.Refresh();
	}

	// A click in the Paste tool: one paste (with its repeats) is one undo step.
	internal void PasteAt(System.Numerics.Vector2 at)
	{
		if (_session is not { } s || _view.Paste.Clip == null)
		{
			return;
		}
		// The objects' heights come from the ground as the paste leaves it: the ground step fills the list,
		// which Commit reads after it (one undo step for both).
		var add = new List<(TerrainEditor.Editing.NewObject, bool)>();
		int touched = 0;
		var paste = _view.Paste;
		var label = paste.Count > 1 ? $"Paste ×{paste.Count}" : "Paste";
		s.Commit(label, g =>
		{
			var (t, rect, a) = paste.Apply(g, at, s.MaskNow());
			add.AddRange(a);
			touched = t.Count;
			return (t, rect);
		}, Array.Empty<int>(), add);
		_message.Text = $"Pasted{(paste.Count > 1 ? $" {paste.Count} copies" : "")}{(touched > 0 ? " with the ground" : "")}{(add.Count > 0 ? $", {add.Count} object(s)" : "")}. Click again to paste more, Esc when done.";
		UpdateSaveBar();
	}

	// Built by: the world's builders, this computer's characters, named players, another id, or nobody.
	private void FillBuilders()
	{
		if (_session?.Scene.World is not { } w)
		{
			return;
		}
		_fillingBuilders = true;
		var players = Builders.Players(w);
		BuilderIds.Clear();
		BuilderIds.AddRange(players.Select(p => p.Id));
		BuilderIds.Add(-1);
		BuilderIds.Add(0);
		BuilderBox.ItemsSource = players.Select(p => p.Label).Append("Other player id…").Append("Nobody (not player built)").ToList();
		BuilderBox.SelectedIndex = BuilderIds.IndexOf(TerrainEditor.Save.WorldSave.Builder);
		_fillingBuilders = false;
	}

	// Asks for a player id ("Other player id…"; replaced by tests).
	internal Func<Task<string?>> AskPlayerId { get; set; }

	private async Task BuilderChosen()
	{
		if (_fillingBuilders || BuilderBox.SelectedIndex < 0 || _session?.Scene.World is not { } w)
		{
			return;
		}
		long id = BuilderIds[BuilderBox.SelectedIndex];
		if (id == -1)
		{
			string? text = (await AskPlayerId())?.Trim();
			if (!long.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out id) || id == 0)
			{
				if (!string.IsNullOrEmpty(text))
				{
					_message.Text = "A player id is a whole number other than 0.";
				}
				FillBuilders();
				return;
			}
		}
		TerrainEditor.Save.WorldSave.Builder = id;
		PlacePanel.Memory.Builders[w.Name] = id;
		PlacePanel.Memory.Save();
		FillBuilders();
		_message.Text = id == 0 ? "New pieces get no builder: the game takes them for parts of a ruin."
			: $"New pieces are built by {Builders.Players(w).FirstOrDefault(p => p.Id == id)?.Label ?? $"player {id}"}.";
	}

	// Make player built: the selected pieces players build that have no builder get the chosen one.
	internal void Claim()
	{
		_view.SelectTool.Commit();
		if (_session is not { Scene.World: { } world } s)
		{
			return;
		}
		if (TerrainEditor.Save.WorldSave.Builder == 0)
		{
			_message.Text = "Built by is set to Nobody: choose a player first (View panel, Building).";
			return;
		}
		var sel = _view.Selected.ToList();
		var remove = new List<int>();
		var adds = new List<(TerrainEditor.Editing.NewObject, bool)>();
		foreach (int i in sel)
		{
			var t = s.Scene.Things[i];
			if (ObjectData.Bytes(world, s.Edits, t.Id) is { } bytes && Builders.Claimed(bytes, TerrainEditor.Save.WorldSave.Builder) is { } z)
			{
				remove.Add(i);
				adds.Add((new TerrainEditor.Editing.NewObject(0, z.Prefab, z.Position, z.Rotation, 0, null, false, z.Serialize()), true));
			}
		}
		if (adds.Count == 0)
		{
			_message.Text = "Nothing to change: the selected pieces already have a builder, or are not things players build.";
			return;
		}
		var copies = s.Commit($"Made {adds.Count} piece(s) player built", null, remove, adds);
		_view.Select(sel.Where(i => !remove.Contains(i)).Concat(copies));
		_message.Text = $"{adds.Count} piece(s) are now player built ({sel.Count - adds.Count} left as they were). Save writes it.";
		UpdateSaveBar();
	}

	// The inspector on the one selected object (the View panel makes room for it).
	internal void Inspect()
	{
		_view.SelectTool.Commit();
		if (_view.Selected.Count == 1 && _session?.Scene.Things[_view.Selected.First()].Runestone == true)
		{
			_message.Text = "A runestone holds no data of its own: the game builds it from its location. It can only be deleted.";
			return;
		}
		if (_view.Selected.Count == 1 && Inspector.Open(_view.Selected.First()))
		{
			_viewPanel.IsVisible = false;
		}
	}

	// The Path tool's Apply: the action along the line, in one undo step. The line is kept.
	internal void ApplyPath()
	{
		if (_session is not { } s)
		{
			return;
		}
		var path = _view.Path;
		if (path.Points.Count < 2)
		{
			_message.Text = "Draw a line with at least two points first.";
			return;
		}
		bool clamped = false;
		if (path.Act == PathTool.Action.Cave)
		{
			ApplyCave(s, path);
			return;
		}
		var touched = s.EditGround($"Path: {PathTool.Label(path.Act)}", g =>
		{
			var (t, rect, c) = path.Apply(g, s.Brush, s.Scene.Water, s.MaskNow());
			clamped = c;
			return (t, rect);
		});
		float total = PathTool.Length(path.Curve());
		_message.Text = touched.Count == 0 ? "Nothing changed along the line."
			: clamped ? "Applied, but part of the path reached the game limit of ±8 m from the original ground (red points)."
			: $"Applied “{PathTool.Label(path.Act)}” along {total:0} m. The line is kept, so you can apply another action (Clear to start over).";
		PathPanel.Refresh();
		UpdateSaveBar();
	}

	// The Cave action: the trench, the trees and rocks in it taken away and the roof's boulders, in one
	// undo step.
	private void ApplyCave(EditSession s, PathTool path)
	{
		var scene = s.Scene;
		var cave = path.PlanCave(s.Ground);
		float ox = scene.X0 * 64f - 32f, oz = scene.Z0 * 64f - 32f, water = scene.Water;
		PathTool.CaveRock RockAt(float x, float z)
		{
			if (path.Rock != PathTool.CaveRock.Auto)
			{
				return path.Rock;
			}
			var biome = scene.Terrain?.BiomeAt(x, z);
			return biome switch
			{
				ValheimGen.Heightmap.Biome.Plains => PathTool.CaveRock.Heath,
				ValheimGen.Heightmap.Biome.Mountain or ValheimGen.Heightmap.Biome.DeepNorth => PathTool.CaveRock.Mountain,
				ValheimGen.Heightmap.Biome.Ocean => PathTool.CaveRock.Coast,
				_ => PathTool.CaveRock.Forest,
			};
		}
		var built = CaveRoof.Build(path, cave, s.Ground.W, s.Ground.H, ox, oz, RockAt);
		var roof = built.Rocks
			.Select(r => (r.Prefab, Hash: TerrainEditor.Save.StableHash.Of(r.Prefab), r.Position, r.Rotation, r.Scale))
			.Where(r => scene.World?.CanCreate(r.Hash) != false)
			.Select(r => (new TerrainEditor.Editing.NewObject(0, r.Hash, r.Position, r.Rotation, r.Scale), false)).ToList();
		// Natural objects in the trench (deeper than half a metre there) would float or stand in the way.
		float reach = path.Width / 2 + path.Soft;
		var remove = new List<int>();
		lock (scene.Things)
		{
			for (int i = 0; i < scene.Things.Count; i++)
			{
				var t = scene.Things[i];
				if (t.Gone || !EditSession.Natural(t))
				{
					continue;
				}
				var at = new System.Numerics.Vector2(t.Position.X - ox, t.Position.Z - oz);
				for (int k = 0; k < cave.Curve.Count; k++)
				{
					if (cave.Depth[k] > 0.5f && System.Numerics.Vector2.Distance(cave.Curve[k].P, at) <= reach)
					{
						remove.Add(i);
						break;
					}
				}
			}
		}
		bool clamped = false;
		int open = 0;
		s.Commit($"Cave along {PathTool.Length(cave.Curve):0} m", g =>
		{
			var (t, rect, c) = path.Apply(g, s.Brush, water, s.MaskNow(), cave);
			clamped = c;
			// The walls up into the rock where its underside is above them (no daylight at the sides).
			foreach (var (p, top) in built.Seal)
			{
				if (!g.Locked(p % g.W, p / g.W) && g.HeightOf(p) < top)
				{
					g.SetHeight(p, top);
					t.Add(p);
					if (g.HeightOf(p) < top - 0.5f)
					{
						open++;
					}
				}
			}
			return (t, (rect.X0 - 4, rect.Z0 - 4, rect.X1 + 4, rect.Z1 + 4));
		}, remove, roof);
		_message.Text = built.Uncovered > 0 && roof.Count > 0
			? $"Dug a cave roofed with {roof.Count} boulder(s), but {built.Uncovered} point(s) of its floor stay open to the sky (at the area's edge?)."
			: roof.Count == 0
			? "Dug the cave's trench, but it is nowhere deep enough for a roof: make Depth larger than Headroom by 1.5 m, or the line longer (its ends slope up to the ground)."
			: $"Dug a cave {PathTool.Length(cave.Curve):0} m long, roofed with {roof.Count} boulder(s){(remove.Count > 0 ? $"; {remove.Count} tree(s) and rock(s) in the way taken away" : "")}."
				+ (open > 0 ? $" At {open} point(s) the walls could not rise to the rock (the game's ±8 m: No limit in the brush options lets them)." : "")
				+ (clamped ? " Part of it reached the game's ±8 m limit (switch on No limit in the brush options to dig deeper)." : " Ctrl+Z takes it back.");
		PathPanel.Refresh();
		UpdateSaveBar();
	}

	// Stamp once (Raise or Lower with a stamp): the whole picture into the ground, one undo step.
	internal void StampAt(float gx, float gz)
	{
		if (_session is not { } s)
		{
			return;
		}
		var b = Tools.Brush;
		float amount = b.StampHeight * (Tools.Tool == BrushTool.Lower ? -1 : 1);
		bool clamped = false;
		s.EditGround($"Stamp: {b.StampLabel}", g =>
		{
			var (t, rect, c) = Sculpt.StampOnce(g, b, gx, gz, amount, s.MaskNow());
			clamped = c;
			return (t, rect);
		});
		_message.Text = clamped ? "Stamped, but part of it reached the game limit of ±8 m from the original ground (red points)."
			: $"Stamped {MathF.Abs(amount)} m {(amount >= 0 ? "up" : "down")}. Ctrl+Z undoes it.";
		UpdateSaveBar();
	}

	// The Script tool's Run: compiled and run in the background on a snapshot of the area, then what it
	// changed goes in as one undo step (nothing when it fails or is stopped).
	internal async Task RunScript()
	{
		if (_session is not { } s)
		{
			_message.Text = "Open an area first: a script works on the open area.";
			return;
		}
		if (_stopScript != null)
		{
			return;
		}
		var panel = ScriptPanel;
		string name = panel.ScriptBox.SelectedItem as string ?? "script";
		panel.Running(true);
		panel.Output.Text = "Compiling…";
		using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		_stopScript = cancel.Cancel;
		string code = panel.CodeBox.Text ?? "";
		var snap = ScriptHost.Take(s, NameOfPrefab);
		var watch = System.Diagnostics.Stopwatch.StartNew();
		try
		{
			var (image, pdb, errors) = await Task.Run(() => ScriptHost.Compile(code));
			if (image == null)
			{
				panel.Output.Text = "The script has mistakes:\n" + string.Join("\n", errors);
				_message.Text = "The script has mistakes (see below it): nothing changed.";
				return;
			}
			panel.Output.Text = "Running…";
			var ch = await Task.Run(() => ScriptHost.Run(image, pdb, snap, cancel.Token), cancel.Token);
			if (_session != s)
			{
				panel.Output.Text = ch.Output + "Another area was opened while the script ran: nothing changed.";
				return;
			}
			string what;
			if (ch.Empty)
			{
				what = "The script ran and changed nothing.";
			}
			else
			{
				int clamped = ScriptHost.Apply(s, ch, $"Script: {name.Replace("Example: ", "", StringComparison.Ordinal)}");
				var parts = new List<string>();
				if (ch.Heights.Count > 0) parts.Add($"{ch.Heights.Count:N0} ground point(s)");
				if (ch.Paint.Count > 0) parts.Add($"{ch.Paint.Count:N0} painted");
				if (ch.Add.Count > 0) parts.Add($"{ch.Add.Count:N0} object(s) placed");
				if (ch.Remove.Count > 0) parts.Add($"{ch.Remove.Count:N0} taken away");
				what = $"The script changed {string.Join(", ", parts)} in {watch.Elapsed.TotalSeconds:0.0} s; Ctrl+Z takes it all back."
					+ (clamped > 0 ? $" {clamped:N0} point(s) stopped at the game's ±8 m (Ground.NoLimit = true lets them go further)." : "");
			}
			panel.Output.Text = ch.Output + what;
			_message.Text = what;
			UpdateSaveBar();
		}
		catch (OperationCanceledException)
		{
			panel.Output.Text = "Stopped: nothing changed.";
			_message.Text = "The script was stopped (or ran 2 minutes): nothing changed.";
		}
		catch (Exception ex)
		{
			panel.Output.Text = $"The script stopped with an error, nothing changed:\n{ScriptHost.Where(ex)}{ex.GetType().Name}: {ex.Message}";
			_message.Text = "The script stopped with an error (see below it): nothing changed.";
		}
		finally
		{
			_stopScript = null;
			panel.Running(false);
		}
	}

	// The Mountain tool's click: the mountain goes into the ground there (grid point), then the biome's
	// trees and rocks grow on it (a second undo step).
	internal async Task PutMountain(float gx, float gz)
	{
		if (_session is not { } s)
		{
			return;
		}
		var m = MountainPanel.Spec;
		string name = MountainPanel.Preset.Name;
		if (s.Mountain(gx, gz, m, MountainPanel.Clear, $"Mountain: {name}") is not { } done)
		{
			_message.Text = $"The {name.ToLowerInvariant()} reaches {m.Reach:0} m around the click and does not fit in the open area: click nearer its middle, make it smaller, or open a bigger area.";
			return;
		}
		UpdateSaveBar();
		string placed = $"Placed a {name.ToLowerInvariant()}, {m.Height:0} m high{(done.Cleared > 0 ? $"; {done.Cleared} tree(s) and rock(s) it buried taken away" : "")}.";
		_message.Text = placed;
		if (!MountainPanel.Grow)
		{
			_message.Text = placed + " Ctrl+Z takes it back; saving turns it into ground discs.";
			return;
		}
		var shape = Mountain.Shape(m);
		float ox = s.Scene.X0 * 64f - 32f, oz = s.Scene.Z0 * 64f - 32f, wx = ox + gx, wz = oz + gz;
		// On the slopes it raised by more than a metre (not around its foot, where the old ones stand).
		var spots = await Growth.Spots(s, wx - m.Reach, wz - m.Reach, wx + m.Reach, wz + m.Reach, o => shape(o.X - wx, o.Z - wz) > 1,
			NameOfPrefab, t => _message.Text = placed + " " + t, maxZones: 49);
		if (spots is { Count: > 0 })
		{
			Growth.Commit(s, $"Mountain: grew {spots.Count} object(s)", spots);
			placed += $" Grew {spots.Count} of the biome's trees and rocks on it.";
		}
		_message.Text = placed + " Ctrl+Z takes it back (twice with what grew); saving turns it into ground discs.";
		UpdateSaveBar();
	}

	// The Shape tool's click: the shape goes into the ground there (grid point).
	internal void PutShape(float gx, float gz)
	{
		if (_session is not { } s)
		{
			return;
		}
		if (ShapePanel.Function is not { } f)
		{
			_message.Text = "Fix the formula first.";
			return;
		}
		var (touched, clamped, bad) = s.Shape(gx, gz, f, ShapePanel.Radius, ShapePanel.Height, ShapePanel.Label);
		_message.Text = touched == 0 ? "The formula gives 0 everywhere within the radius: nothing changed." + (bad > 0 ? $" ({bad} point(s) gave no number.)" : "")
			: clamped ? "Placed, but part of the shape reached the game limit of ±8 m from the original ground (red points)."
			: $"Placed the shape on {touched} point(s). Ctrl+Z undoes it.";
		UpdateSaveBar();
	}

	// The right-hand panels share the place under the top bar: one at a time (null: none).
	// The driver: the right-hand panel by name (view, history, help or none).
	internal void ShowRightPanel(string name) => ShowRight(name switch { "view" => _viewPanel, "history" => History.Card, "help" => HelpCard, _ => null });

	internal void ShowRight(Control? panel)
	{
		if (panel == _viewPanel || panel == HelpCard || panel == History.Card || panel == null)
		{
			Prefs.Set("panel.right", panel == _viewPanel ? "view" : panel == HelpCard ? "help" : panel == History.Card ? "history" : "none");
		}
		_viewPanel.IsVisible = panel == _viewPanel;
		HelpCard.IsVisible = panel == HelpCard;
		if (History.Card.IsVisible != (panel == History.Card))
		{
			History.Toggle();
		}
		ViewButton.Classes.Set("on", _viewPanel.IsVisible);
		HistoryButton.Classes.Set("on", History.Card.IsVisible);
		HelpButton.Classes.Set("on", HelpCard.IsVisible);
	}

	private static Border Sep() => new Border { Width = 1, Height = 26, Background = Ui.Line, Margin = new Thickness(4, 0), VerticalAlignment = VerticalAlignment.Center };

	// The top bar (the web editor's): back to the map, the names, undo and redo, what is waiting to be
	// saved and Save, and the right-hand panels.
	private Border TopBar()
	{
		UndoButton.Click += (_, _) => Undo();
		MapButton.Click += (_, _) => ShowMap();
		MapButton.Tip("top.map");
		ToolTip.SetTip(UndoButton, "Undo the last change (Ctrl+Z).");
		ToolTip.SetTip(RedoButton, "Redo the change you just undid (Ctrl+Y or Ctrl+Shift+Z).");
		HistoryButton.Click += (_, _) => ShowRight(History.Card.IsVisible ? null : History.Card);
		History.Closed += () => HistoryButton.Classes.Set("on", false);
		HistoryButton.Tip("top.history");
		ViewButton.Click += (_, _) => ShowRight(_viewPanel.IsVisible ? null : _viewPanel);
		ViewButton.Tip("top.view");
		HelpButton.Click += (_, _) => ShowRight(HelpCard.IsVisible ? null : HelpCard);
		HelpButton.Tip("top.help");
		RedoButton.Click += (_, _) => Redo();
		SaveButton.Click += async (_, _) => await Save();
		SaveButton.Tip("top.save");
		ToolTip.SetTip(_liveBadge, "Connected to the running game through the WorldEditorBridge plugin");
		_pendingPill.Child = _pending;
		var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { _title, _subtitle } };
		var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
		var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { MapButton, names, AreaNav } };
		var right = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 4,
			VerticalAlignment = VerticalAlignment.Center,
			Children = { UndoButton, RedoButton, Sep(), _liveBadge, ReloadButton, AutoApplyBox, _pendingPill, DiscardButton, SaveButton, Sep(), HistoryButton, ViewButton, HelpButton },
		};
		Grid.SetColumn(right, 2);
		bar.Children.Add(left);
		bar.Children.Add(right);
		var card = Ui.Card(bar);
		card.Height = 50;
		card.Padding = new Thickness(Ui.Pad.Left, 0);
		card.Margin = new Thickness(10, 10, 10, 0);
		card.VerticalAlignment = VerticalAlignment.Top;
		return card;
	}

	// The status bar's cursor readout: where the pointer is on the ground (see CursorReadout).
	internal TextBlock CursorText { get; } = new() { FontSize = 12, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.NoWrap };
	private Point? _cursorAt;

	internal void ShowCursor(Point? at)
	{
		_cursorAt = at;
		CursorText.Text = at is Point p && _view.Scene is { } s && Surface is { } surface && _view.GridAt(p, surface.Bounds.Size) is { } g ? CursorReadout.Text(s, g.X, g.Y) : "";
	}

	// The status bar (bottom): the cursor, the last message, the selection, walking or flying, and the controls.
	private Border StatusBar()
	{
		var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"), ColumnSpacing = 18 };
		var tips = new TextBlock { Text = "Right drag turns · Wheel zooms · Middle drag slides · ? help", FontSize = 12, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center };
		_selection.VerticalAlignment = _eye.VerticalAlignment = VerticalAlignment.Center;
		_selection.TextWrapping = _eye.TextWrapping = TextWrapping.NoWrap;
		_selection.MaxWidth = 380;
		_selection.TextTrimming = TextTrimming.CharacterEllipsis;
		Grid.SetColumn(_message, 1);
		Grid.SetColumn(_selection, 2);
		Grid.SetColumn(_eye, 3);
		Grid.SetColumn(tips, 4);
		bar.Children.Add(CursorText);
		bar.Children.Add(_message);
		bar.Children.Add(_selection);
		bar.Children.Add(_eye);
		bar.Children.Add(tips);
		var card = Ui.Card(bar);
		card.Margin = new Thickness(10, 0, 10, 10);
		card.VerticalAlignment = VerticalAlignment.Bottom;
		return card;
	}

	// Help: the controls (the web editor's list), then what the program knows about this computer.
	private Border Help(CheckBox record)
	{
		var list = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12, RowSpacing = 5 };
		var rows = new (string Keys, string What)[]
		{
			("Left drag", "Use the tool"), ("Right drag", "Turn the view"), ("Wheel", "Zoom"),
			("Middle drag", "Slide the view (also Space + left drag, Shift + right drag, the View tool)"), ("W A S D", "Move around"),
			("F", "Walk at eye height · fly (Space up, C down) · back to the usual view"), ("1–9, 0, O", "Sculpt and paint tools (O: Erode)"),
			("B / P / T / G", "Area, Path, Place, Shape"), ("E / M / H", "Select, Measure, View (Esc too)"), ("[  ]", "Brush size"),
			("Ctrl+C / Ctrl+V", "Copy the area / paste it (R turns 90°, , . turn 1°, F mirrors)"),
			("Alt + wheel", "Turn the selection, the paste or the Place preview (, . too; Shift: 15°)"), ("Place: R", "New layout"),
			("Alt + click", "Pick the ground height"), ("Del", "Delete the selected objects"), ("I", "Inspect the selected object"),
			("PgUp / PgDn / End", "Lift, lower or drop the selection"),
			("Ctrl+Z / Ctrl+Y", "Undo / redo"), ("Ctrl+S", "Save"), ("V / L / ?", "View panel / history / this help"),
		};
		for (int i = 0; i < rows.Length; i++)
		{
			list.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			var k = new TextBlock { Text = rows[i].Keys, FontSize = 12.5 };
			var w = new TextBlock { Text = rows[i].What, FontSize = 12.5, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
			Grid.SetRow(k, i);
			Grid.SetRow(w, i);
			Grid.SetColumn(w, 1);
			list.Children.Add(k);
			list.Children.Add(w);
		}
		var close = new Button { Content = Icons.Make("close", 14), Padding = new Thickness(5) }.Classed("ghost").Tip("card.close");
		close.Click += (_, _) => ShowRight(null);
		var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = "Controls", FontSize = 14, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center } } };
		Grid.SetColumn(close, 1);
		head.Children.Add(close);
		record.Classes.Add("switch");
		var card = Ui.Card(new ScrollViewer
		{
			Content = new StackPanel
			{
				Spacing = 8,
				Children =
				{
					head, list,
					Ui.Hint("Edits stay within the game's limit of ±8 m from the original ground; points at the limit turn red (View: Limit marks)."),
					Ui.Hint("The outer line of points is locked so the area always joins its neighbours seamlessly."),
					Ui.Heading("This computer"),
					_fps, _info, record,
				},
			},
		});
		card.Width = 330;
		card.IsVisible = false;
		return card;
	}

	// What is remembered between runs (Prefs, as the web editor kept it in the browser): each control set
	// from it now and remembered as it changes.
	private void RememberPrefs()
	{
		var p = Prefs;
		// View: the kinds drawn, water, overlays, the look's slope colours and height lines.
		foreach (var (k, box) in _kindBoxes)
		{
			p.Bind(box, $"view.show.{k}");
		}
		p.Bind(WaterBox, "view.water");
		p.Bind(UnsavedBox, "view.unsaved");
		p.Bind(LimitBox, "view.limit");
		foreach (var (layer, box) in _overlayBoxes)
		{
			p.Bind(box, $"view.overlay.{layer}");
		}
		p.Bind(SlopeBox, "view.slope");
		p.Bind(ContourBox, "view.contour");
		p.Bind(ContourStepBox, "view.contourStep");
		// The look: the game's or plain, buildings see-through, the 3D view's resolution.
		p.Bind(GameLookBox, "view.gameLook");
		p.Bind(SeeThroughBox, "view.seeThrough");
		p.Bind(ResolutionBox, "view.res3d");
		// The brush's shape and falloff, the Area action, Select's options.
		p.Bind(Tools.ShapeBox, "brush.shape");
		p.Bind(Tools.FalloffBox, "brush.falloff");
		p.Bind(AreaPanel.ActionBox, "area.action");
		p.Bind(SelectPanel.GroundBox, "select.onGround");
		p.Bind(SelectPanel.SnapBox, "select.snap");
		// Shape: the preset, and the formula when it is one's own.
		p.Bind(ShapePanel.PresetBox, "shape.preset");
		bool Own() => ShapePanel.PresetBox.SelectedIndex == ShapePanel.Presets.Length;
		if (Own() && p.TryGet("shape.formula", out string? formula) && formula != null)
		{
			ShapePanel.FormulaBox.Text = formula;
		}
		ShapePanel.FormulaBox.PropertyChanged += (_, e) =>
		{
			if (e.Property == TextBox.TextProperty && Own())
			{
				p.Set("shape.formula", ShapePanel.FormulaBox.Text ?? "");
			}
		};
		// The Place tool's list: which categories are open.
		if (p.TryGet("place.openKinds", out string[]? open) && open != null)
		{
			PlacePanel.OpenKinds.Clear();
			foreach (var name in open)
			{
				if (Enum.TryParse<ObjectKind>(name, out var k))
				{
					PlacePanel.OpenKinds.Add(k);
				}
			}
		}
		PlacePanel.OpenKindsChanged += () => p.Set("place.openKinds", PlacePanel.OpenKinds.Select(k => k.ToString()).Order().ToArray());
		// The right-hand panel open.
		ShowRight(p.Get("panel.right", "view") switch { "help" => HelpCard, "history" => History.Card, "none" => null, _ => _viewPanel });
		// The clipboard (its objects' ids only meant something in the world it came from).
		if (p.TryGet("clipboard", out System.Text.Json.Nodes.JsonObject? clip) && clip != null)
		{
			try
			{
				_view.Paste.Clip = CopyFormat.FromJson(clip, keepSources: false);
				_keptClip = _view.Paste.Clip;
			}
			catch (Exception ex) when (ex is FormatException or InvalidOperationException or NullReferenceException or ArgumentException or System.Text.Json.JsonException)
			{
				p.Remove("clipboard");
			}
		}
		_view.Paste.Changed += () =>
		{
			if (_view.Paste.Clip is { } c && !ReferenceEquals(c, _keptClip))
			{
				_keptClip = c;
				p.Set("clipboard", CopyFormat.ToJson(c));
			}
		};
	}

	private CopyData? _keptClip;

	// Placing (or pasting) a kind switched off in View switches it on, so what was placed is seen; said
	// after what the tool says about the placing, as the web editor did.
	internal void ShowKindsOf(WorldScene scene, IReadOnlyList<int> indices)
	{
		var turned = new List<string>();
		foreach (var k in indices.Where(i => i < scene.Things.Count).Select(i => ObjectKinds.Of(NameOfPrefab(scene.Things[i].Prefab), scene.Things[i].Piece, scene.Things[i].Tamed)).Distinct())
		{
			if (_kindBoxes.TryGetValue(k, out var box) && box.IsChecked != true)
			{
				box.IsChecked = true;
				turned.Add(ObjectKinds.Label(k));
			}
		}
		if (turned.Count == 0)
		{
			return;
		}
		Dispatcher.UIThread.Post(() =>
		{
			string said = _message.Text ?? "";
			_message.Text = $"{(said != "" ? said + " " : "")}Switched on {string.Join(", ", turned)} in View, so what you placed stays visible.";
		}, DispatcherPriority.Background);
	}

	// Eyedropper for the Replace lists: the next click on an object gives its kind (Esc cancels).
	internal void PickKind(string label, Action<int> done)
	{
		_view.PickObjectOnce = i =>
		{
			if (i is int t && _view.Scene is { } sc && t < sc.Things.Count)
			{
				done(sc.Things[t].Prefab);
			}
			else
			{
				_message.Text = "Nothing picked: click right on an object (only things that are shown can be picked).";
			}
		};
		_message.Text = $"Click an object to pick its kind for {label}. Esc cancels.";
	}
	// , . turn the paste by 1° (Shift: 15°); . is clockwise seen from above, like the other tools.
	private bool TurnPaste(Avalonia.Input.Key key, bool shift)
	{
		float step = shift ? 15 : 1;
		_view.Paste.TurnBy(key == Avalonia.Input.Key.OemComma ? step : -step);
		return true;
	}

	// Alt + wheel: the tool that turns things turns them, as its , and . keys do; otherwise (false) it zooms.
	internal bool AltWheel(Avalonia.Input.Key key, bool shift) => Tools.Mode switch
	{
		ToolMode.Paste => TurnPaste(key, shift),
		ToolMode.Place => PlaceInput.Key(key, shift, false),
		_ when Tools.SelectMode => _view.SelectTool.Key(key, shift, false),
		_ => false,
	};

	private void OnKey(object? sender, Avalonia.Input.KeyEventArgs e)
	{
		// Typing in a box: its keys are its own.
		if (e.Source is TextBox)
		{
			return;
		}
		var mods = e.KeyModifiers;
		bool ctrl = mods.HasFlag(Avalonia.Input.KeyModifiers.Control) || mods.HasFlag(Avalonia.Input.KeyModifiers.Meta);
		if (!ctrl && e.Key == Avalonia.Input.Key.Escape && _view.PickObjectOnce != null)
		{
			_view.PickObjectOnce = null;
			_message.Text = "Picking cancelled.";
			e.Handled = true;
			return;
		}
		// The right-hand panels, as in the web editor: V the View panel, L the history, ? (or F3) the help.
		if (!ctrl && e.Key is Avalonia.Input.Key.F3 or Avalonia.Input.Key.OemQuestion || (!ctrl && mods.HasFlag(Avalonia.Input.KeyModifiers.Shift) && e.Key == Avalonia.Input.Key.Oem2))
		{
			ShowRight(HelpCard.IsVisible ? null : HelpCard);
			e.Handled = true;
			return;
		}
		if (!ctrl && mods == Avalonia.Input.KeyModifiers.None && e.Key == Avalonia.Input.Key.V)
		{
			ShowRight(_viewPanel.IsVisible ? null : _viewPanel);
			e.Handled = true;
			return;
		}
		if (!ctrl && mods == Avalonia.Input.KeyModifiers.None && e.Key == Avalonia.Input.Key.L)
		{
			ShowRight(History.Card.IsVisible ? null : History.Card);
			e.Handled = true;
			return;
		}
		if (ctrl && e.Key == Avalonia.Input.Key.Z)
		{
			if (mods.HasFlag(Avalonia.Input.KeyModifiers.Shift)) Redo(); else Undo();
			e.Handled = true;
		}
		else if (ctrl && e.Key == Avalonia.Input.Key.Y)
		{
			Redo();
			e.Handled = true;
		}
		else if (ctrl && e.Key == Avalonia.Input.Key.C && Tools.Mode is ToolMode.Area or ToolMode.Select)
		{
			Copy();
			e.Handled = true;
		}
		else if (ctrl && e.Key == Avalonia.Input.Key.V)
		{
			StartPaste();
			e.Handled = true;
		}
		else if (Tools.Mode == ToolMode.Paste && !ctrl && e.Key is Avalonia.Input.Key.R or Avalonia.Input.Key.F or Avalonia.Input.Key.OemComma or Avalonia.Input.Key.OemPeriod or Avalonia.Input.Key.Escape)
		{
			switch (e.Key)
			{
				case Avalonia.Input.Key.R: _view.Paste.TurnBy(90); break;
				case Avalonia.Input.Key.F: _view.Paste.Mirror = !_view.Paste.Mirror; _view.Paste.Notify(); break;
				case Avalonia.Input.Key.OemComma or Avalonia.Input.Key.OemPeriod: TurnPaste(e.Key, mods.HasFlag(Avalonia.Input.KeyModifiers.Shift)); break;
				default: Tools.ChooseMode(ToolMode.Area); break;
			}
			e.Handled = true;
		}
		else if (ctrl && e.Key == Avalonia.Input.Key.S)
		{
			_ = Save();
			e.Handled = true;
		}
		else if (!ctrl && e.Key is Avalonia.Input.Key.OemOpenBrackets or Avalonia.Input.Key.OemCloseBrackets)
		{
			Tools.ResizeBrush(e.Key == Avalonia.Input.Key.OemOpenBrackets ? -1 : 1);
			e.Handled = true;
		}
		else if (!ctrl && e.Key == Avalonia.Input.Key.Escape && Inspector.IsOpen)
		{
			Inspector.Close();
			e.Handled = true;
		}
		else if (!ctrl && e.Key == Avalonia.Input.Key.I && Tools.SelectMode && _view.Selected.Count == 1)
		{
			Inspect();
			e.Handled = true;
		}
		else if (Tools.Mode == ToolMode.Place && PlaceInput.Key(e.Key, mods.HasFlag(Avalonia.Input.KeyModifiers.Shift), ctrl))
		{
			e.Handled = true;
		}
		else if (Tools.SelectMode && _view.SelectTool.Key(e.Key, mods.HasFlag(Avalonia.Input.KeyModifiers.Shift), ctrl))
		{
			e.Handled = true;
		}
		else if (Tools.Mode == ToolMode.Area && !ctrl && e.Key is Avalonia.Input.Key.Enter or Avalonia.Input.Key.Return)
		{
			if (!_view.Area.CloseWithEnter())
			{
				_ = AreaPanel.Apply();
			}
			e.Handled = true;
		}
		else if (Tools.Mode == ToolMode.Area && !ctrl && e.Key == Avalonia.Input.Key.Back)
		{
			_view.Area.RemoveLast();
			e.Handled = true;
		}
		else if (Tools.Mode == ToolMode.Area && !ctrl && e.Key == Avalonia.Input.Key.Escape && _view.Area.Points.Count > 0)
		{
			_view.Area.Clear();
			e.Handled = true;
		}
		else if (Tools.Mode == ToolMode.Path && !ctrl && e.Key is Avalonia.Input.Key.Enter or Avalonia.Input.Key.Return)
		{
			ApplyPath();
			e.Handled = true;
		}
		else if (Tools.Mode == ToolMode.Path && !ctrl && e.Key == Avalonia.Input.Key.Back)
		{
			_view.Path.RemoveLast();
			e.Handled = true;
		}
		else if (Tools.Mode == ToolMode.Path && !ctrl && e.Key == Avalonia.Input.Key.Escape && _view.Path.Points.Count > 0)
		{
			_view.Path.Clear();
			e.Handled = true;
		}
		else if (Tools.Mode == ToolMode.Measure && !ctrl && e.Key == Avalonia.Input.Key.Escape && _view.Tape.A != null)
		{
			_view.Tape.Clear();
			e.Handled = true;
		}
		else if (!ctrl && e.Key == Avalonia.Input.Key.Escape)
		{
			Tools.Key("Escape");
		}
		else if (!ctrl && e.Key is Avalonia.Input.Key.E or Avalonia.Input.Key.M or Avalonia.Input.Key.G or Avalonia.Input.Key.P or Avalonia.Input.Key.B or Avalonia.Input.Key.O or Avalonia.Input.Key.T or Avalonia.Input.Key.H)
		{
			Tools.Key(e.Key.ToString());
		}
		else if (!ctrl && e.Key >= Avalonia.Input.Key.D0 && e.Key <= Avalonia.Input.Key.D9)
		{
			Tools.Key(((int)(e.Key - Avalonia.Input.Key.D0)).ToString());
		}
		else if (!ctrl && e.Key >= Avalonia.Input.Key.NumPad0 && e.Key <= Avalonia.Input.Key.NumPad9)
		{
			Tools.Key(((int)(e.Key - Avalonia.Input.Key.NumPad0)).ToString());
		}
	}

	// The View panel (top right): which kinds of objects are drawn, with how many the area has, and the water.
	private readonly Dictionary<ObjectKind, CheckBox> _kindBoxes = new();
	internal IReadOnlyDictionary<ObjectKind, CheckBox> KindBoxes => _kindBoxes;
	internal CheckBox WaterBox { get; } = new() { Content = "Water", IsChecked = true, FontSize = 12 };
	internal CheckBox UnsavedBox { get; } = new() { Content = "Unsaved marks", IsChecked = true, FontSize = 12 };
	internal CheckBox LimitBox { get; } = new() { Content = "Limit marks", IsChecked = true, FontSize = 12 };
	private readonly Dictionary<Overlays.Layer, CheckBox> _overlayBoxes = new();
	internal IReadOnlyDictionary<Overlays.Layer, CheckBox> OverlayBoxes => _overlayBoxes;
	internal CheckBox SlopeBox { get; } = new() { Content = "Slope colours", FontSize = 12 };
	// Building: who new pieces are built by (the player ids: the list's entries).
	internal ComboBox BuilderBox { get; } = new() { FontSize = 12, MinWidth = 200, MaxWidth = 260 };
	internal List<long> BuilderIds { get; } = new();
	private bool _fillingBuilders;
	internal CheckBox ContourBox { get; } = new() { Content = "Height lines every", FontSize = 12 };
	internal ComboBox ContourStepBox { get; } = new() { ItemsSource = new[] { "1", "2", "5", "10" }, SelectedIndex = 1, FontSize = 12, MinWidth = 60 };

	private static TextBlock Heading(string t) => Ui.Heading(t, 12);

	// View, Look: the game's look or plain colours and boxes, see-through buildings, and the 3D
	// resolution (GlView). Presets switch every kind and overlay to the defaults, all on, or only the
	// ground (the web editor's buttons).
	internal CheckBox GameLookBox { get; } = new() { Content = "Game look", IsChecked = true, FontSize = 12 };
	internal CheckBox SeeThroughBox { get; } = new() { Content = "See-through buildings", FontSize = 12 };
	internal ComboBox ResolutionBox { get; } = new() { ItemsSource = new[] { "Sharp (the screen's)", "Balanced", "Fast" }, SelectedIndex = 0, FontSize = 12, MinWidth = 150 };
	internal Button DefaultsButton { get; } = new() { Content = "Defaults", FontSize = 12 };
	internal Button AllButton { get; } = new() { Content = "All", FontSize = 12 };
	internal Button GroundButton { get; } = new() { Content = "Ground", FontSize = 12 };
	private readonly Dictionary<CheckBox, bool> _viewDefaults = new();

	internal enum ViewPreset { Defaults, All, Ground }

	internal void ShowPreset(ViewPreset preset)
	{
		foreach (var (box, on) in _viewDefaults)
		{
			box.IsChecked = preset switch { ViewPreset.All => true, ViewPreset.Ground => false, _ => on };
		}
	}

	private Border ViewPanel()
	{
		var list = new StackPanel { Spacing = 2 };
		list.Children.Add(Ui.Heading("View", 0));
		list.Children.Add(Heading("LOOK"));
		GameLookBox.Tip("view.gameLook");
		WaterBox.Tip("view.water");
		SlopeBox.Tip("view.slope");
		ContourBox.Tip("view.contour");
		ContourStepBox.Tip("view.contourStep");
		DefaultsButton.Tip("view.defaults");
		AllButton.Tip("view.all");
		GameLookBox.IsCheckedChanged += (_, _) => _view.GameLookOn = GameLookBox.IsChecked == true;
		SeeThroughBox.Tip("view.seeThrough");
		SeeThroughBox.IsCheckedChanged += (_, _) => _view.SeeThroughBuildings = SeeThroughBox.IsChecked == true;
		ResolutionBox.Tip("view.res3d");
		ResolutionBox.SelectionChanged += (_, _) =>
		{
			_view.Resolution3D = (GlView.Resolution)Math.Max(0, ResolutionBox.SelectedIndex);
			var (w, h) = _view.RenderSize();
			_message.Text = $"3D resolution: {ResolutionBox.SelectedItem}, {w}×{h} pixels.";
		};
		list.Children.Add(GameLookBox);
		list.Children.Add(SeeThroughBox);
		list.Children.Add(new StackPanel { Spacing = 2, Children = { new TextBlock { Text = "3D resolution", FontSize = 12 }, ResolutionBox } });
		list.Children.Add(Heading("OBJECTS"));
		foreach (var k in ObjectKinds.All)
		{
			var box = new CheckBox { Content = ObjectKinds.Label(k), IsChecked = _view.IsShown(k), FontSize = 12 };
			ToolTip.SetTip(box, Tips.Kind(k));
			box.IsCheckedChanged += (_, _) => _view.SetShown(k, box.IsChecked == true);
			_kindBoxes[k] = box;
			list.Children.Add(box);
		}
		WaterBox.IsCheckedChanged += (_, _) => _view.ShowWater = WaterBox.IsChecked == true;
		UnsavedBox.IsCheckedChanged += (_, _) => _view.ShowNewMarkers = UnsavedBox.IsChecked == true;
		LimitBox.IsCheckedChanged += (_, _) => _view.LimitMarks = LimitBox.IsChecked == true;
		list.Children.Add(WaterBox);
		list.Children.Add(Heading("OVERLAYS"));
		foreach (var (layer, label) in new[] { (Overlays.Layer.Borders, "Zone borders"), (Overlays.Layer.Markers, "Location markers"), (Overlays.Layer.Wards, "Ward areas"),
			(Overlays.Layer.Stations, "Build ranges"), (Overlays.Layer.Flatten, "Location flattening") })
		{
			var box = new CheckBox { Content = label, IsChecked = _view.IsOverlayShown(layer), FontSize = 12, Tag = label };
			ToolTip.SetTip(box, Tips.Overlay(layer));
			box.IsCheckedChanged += (_, _) => _view.SetOverlay(layer, box.IsChecked == true);
			_overlayBoxes[layer] = box;
			list.Children.Add(box);
		}
		UnsavedBox.Tip("view.unsaved");
		list.Children.Add(UnsavedBox);
		LimitBox.Tip("view.limit");
		list.Children.Add(LimitBox);
		// The presets cover what is shown: kinds, water and overlays (as they are now: the defaults).
		foreach (var box in _kindBoxes.Values.Append(WaterBox).Concat(_overlayBoxes.Values))
		{
			_viewDefaults[box] = box.IsChecked == true;
		}
		DefaultsButton.Click += (_, _) => ShowPreset(ViewPreset.Defaults);
		AllButton.Click += (_, _) => ShowPreset(ViewPreset.All);
		GroundButton.Click += (_, _) => ShowPreset(ViewPreset.Ground);
		GroundButton.Tip("view.ground");
		list.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 6, 0, 0), Children = { DefaultsButton, AllButton, GroundButton } });
		_view.OverlaysBuilt += o =>
		{
			void Count(Overlays.Layer l, int n) => _overlayBoxes[l].Content = Ui.Counted((string)_overlayBoxes[l].Tag!, n);
			Count(Overlays.Layer.Markers, o.Locations);
			Count(Overlays.Layer.Wards, o.Wards);
			Count(Overlays.Layer.Stations, o.Stations);
			Count(Overlays.Layer.Flatten, o.Flattened);
		};
		list.Children.Add(Heading("BUILDING"));
		BuilderBox.Tip("view.builder");
		list.Children.Add(new StackPanel { Spacing = 2, Children = { new TextBlock { Text = "Built by", FontSize = 12 }, BuilderBox } });
		BuilderBox.SelectionChanged += async (_, _) => await BuilderChosen();
		list.Children.Add(Heading("GROUND (game look)"));
		SlopeBox.IsCheckedChanged += (_, _) => _view.SlopeColours = SlopeBox.IsChecked == true;
		void Contour() => _view.ContourStep = ContourBox.IsChecked == true ? float.Parse((string)ContourStepBox.SelectedItem!) : 0;
		ContourBox.IsCheckedChanged += (_, _) => Contour();
		ContourStepBox.SelectionChanged += (_, _) => Contour();
		list.Children.Add(SlopeBox);
		list.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { ContourBox, ContourStepBox, new TextBlock { Text = "m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } });
		_view.KindCounts += counts =>
		{
			foreach (var (k, box) in _kindBoxes)
			{
				box.Content = Ui.Counted(ObjectKinds.Label(k), counts.GetValueOrDefault(k));
			}
		};
		list.Children.Add(PlayersControl());
		// The web editor's switches.
		foreach (var box in Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(list).OfType<CheckBox>())
		{
			box.Classes.Add("switch");
			box.FontSize = 12.5;
		}
		list.Width = 240;
		return new Border
		{
			Background = Ui.Panel,
			BorderBrush = Ui.Line,
			BoxShadow = BoxShadows.Parse("0 6 24 0 #59000000"),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(10),
			Padding = Ui.Pad,
			Margin = new Thickness(10),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			// Scrolls when the window is too short for it (a bar only then).
			Child = new ScrollViewer { VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = list },
		};
	}

	// load: false opens the window without a world (tests).
	// What is remembered between runs (the tests' windows: in memory only, unless a test gives one).
	internal Prefs Prefs { get; }

	public MainWindow(bool load = true, Prefs? prefs = null)
	{
		Prefs = prefs ?? (load ? Prefs.Load() : Prefs.InMemory());
		SelectPanel = new SelectPanel(_view.SelectTool);
		History = new HistoryPanel(() => _session);
		Inspector = new InspectorPanel(() => _session);
		Blueprints = new BlueprintsPanel(_view.Paste, () => _view.Scene);
		Blueprints.Message += t => _message.Text = t;
		Blueprints.Pasting += () => { _viewPanel.IsVisible = true; StartPaste(); };
		Blueprints.AskName = initial => Dialogs.AskText(this, "Save blueprint", "Name of the blueprint:", initial);
		Blueprints.Confirm = text => Dialogs.Ask(this, "Blueprints", text, "Yes");
		Blueprints.PickFile = async () =>
		{
			var picked = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
			{
				Title = "Import a PlanBuild .blueprint or .vbuild file",
				FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Blueprints") { Patterns = BlueprintPatterns } },
			});
			return picked.Count > 0 && picked[0].Path.IsFile ? picked[0].Path.LocalPath : null;
		};
		Inspector.Message += t => { _message.Text = t; UpdateSaveBar(); };
		Inspector.Replaced += i => _view.Select(new[] { i });
		Inspector.Confirm = text => Dialogs.Ask(this, "Contents", text, "Apply anyway");
		Inspector.Closed += () => _viewPanel.IsVisible = true;
		SelectPanel.Message += t => _message.Text = t;
		SelectPanel.PickKindAsked += PickKind;
		SelectPanel.NameOf = NameOfPrefab;
		SelectPanel.WorldName = () => _world?.World.Name is { Length: > 0 } n ? n : _view.Scene?.World?.Name is { Length: > 0 } m ? m : null;
		SelectPanel.Things = () => _view.Scene?.Things;
		SelectPanel.SelectedIds = () => _view.Selected;
		SelectPanel.SelectIds = ids => { Tools.ChooseSelect(); _view.Select(ids); };
		SelectPanel.AskName = initial => Dialogs.AskText(this, "Keep the selection", "Name of the selection:", initial);
		SelectPanel.Confirm = text => Dialogs.Ask(this, "Saved selections", text, "Forget");
		SelectPanel.InspectButton.Click += (_, _) => Inspect();
		SelectPanel.ClaimButton.Click += (_, _) => Claim();
		AskPlayerId = () => Dialogs.AskText(this, "Built by", "Player id to write as the builder (the number Valheim keeps for a character):");
		History.Message += t => { _message.Text = t; UpdateSaveBar(); };
		SelectPanel.ReplaceAsked += prefab =>
		{
			_view.SelectTool.Commit();
			var sel = _view.Selected.ToList();
			if (_session is not { } s || sel.Count == 0)
			{
				_message.Text = "Select objects to replace first.";
				return;
			}
			AreaPanel!.Replace(s, sel, prefab);
			_view.Select(Array.Empty<int>());
			UpdateSaveBar();
		};
		MaskPanel = new MaskPanel(Mask);
		_view.StampClicked += StampAt;
		Tools.Message += t => _message.Text = t;
		Tools.NoLimitChanged += on =>
		{
			if (_session != null)
			{
				_session.Ground.NoLimit = on;
			}
			_message.Text = on ? "No limit: the ground tools go past the game's ±8 m. Saving turns that ground into ground discs every player's game counts as generated ground." : "The ground tools keep to the game's ±8 m.";
		};
		Tools.LoadStampAsked += async () =>
		{
			var picked = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
			{
				Title = "Use a grayscale picture as the brush shape",
				FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Pictures") { Patterns = PicturePatterns } },
			});
			if (picked.Count == 0 || !picked[0].Path.IsFile)
			{
				return;
			}
			string path = picked[0].Path.LocalPath;
			if (Stamps.FromPicture(File.ReadAllBytes(path)) is { } data)
			{
				Tools.AddStamp(Path.GetFileNameWithoutExtension(path), data);
			}
			else
			{
				_message.Text = "That picture could not be read.";
			}
		};
		_view.BrushAltClick += (h, shift) =>
		{
			if (shift)
			{
				Mask.PickHeight(h);
				_message.Text = $"Mask: ground between {h - 2:0.0} and {h + 2:0.0} m.";
			}
			else
			{
				Tools.FlattenTo(h);
				_message.Text = $"Flatten to {h:0.0} m.";
			}
		};
		MeasurePanel = new MeasurePanel(_view);
		PathPanel = new PathPanel(_view, Tools.Brush);
		PathPanel.ApplyAsked += ApplyPath;
		AreaPanel = new AreaPanel(_view, () => _session, prefab => _models?.NameOf(prefab) ?? TerrainEditor.Terrain.PrefabCatalog.DisplayName(prefab));
		AreaPanel.Message += t => { _message.Text = t; UpdateSaveBar(); };
		AreaPanel.PickKindAsked += PickKind;
		AreaPanel.SwitchToSelect += () => Tools.ChooseSelect();
		AreaPanel.CopyAsked += Copy;
		AreaPanel.PasteAsked += StartPaste;
		AreaPanel.SaveBlueprintAsked += async () => await Blueprints.Save();
		AreaPanel.LibraryAsked += () =>
		{
			// The View panel makes room for the list.
			Inspector.Close();
			Blueprints.Toggle();
			_viewPanel.IsVisible = !Blueprints.Card.IsVisible;
		};
		_view.Paste.Changed += () =>
		{
			var c = _view.Paste.Clip;
			AreaPanel.ClipInfo.Text = c == null ? "Copies the ground (shape and paint) and the shown objects inside the selection."
				: $"Clipboard{(c.Name != null ? $" ({c.Name})" : "")}: {c.W} × {c.H} m, {c.Objects.Count} object(s).";
		};
		AreaPanel.Confirm = text => Dialogs.Ask(this, "Reset zones", text, "Reset when saving");
		AreaPanel.PickPicture = async () =>
		{
			var picked = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
			{
				Title = "Import a heightmap (grayscale PNG)",
				FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("PNG pictures") { Patterns = PngPatterns } },
			});
			return picked.Count > 0 && picked[0].Path.IsFile ? picked[0].Path.LocalPath : null;
		};
		AreaPanel.PickFolder = async () =>
		{
			var picked = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
			{
				Title = "Choose a backup (or copy) of this world: the folder with _main.<n>.chunks",
			});
			return picked.Count > 0 && picked[0].Path.IsFile ? picked[0].Path.LocalPath : null;
		};
		PastePanel = new PastePanel(_view.Paste);
		PlaceTool.Brush = Tools.Brush;
		PlaceTool.Scene = () => _view.Scene;
		PlaceTool.Mask = () => _session?.MaskNow();
		PlaceTool.NameOf = NameOfPrefab;
		PlaceTool.ModelBox = ModelBox;
		PlaceInput = new PlaceInput(_view, PlaceTool) { Session = () => _session };
		PlaceInput.Message += t => { _message.Text = t; UpdateSaveBar(); };
		_view.Place = PlaceInput;
		PlacePanel = new PlacePanel(PlaceInput, () => _view.Scene, NameOfPrefab);
		PlacePanel.Message += t => _message.Text = t;
		PlacePanel.AskName = () => Dialogs.AskText(this, "Save as preset", "Name of the preset:");
		PlacePanel.Confirm = text => Dialogs.Ask(this, "Delete the preset", text, "Delete");
		PastePanel.Done += () => Tools.ChooseMode(ToolMode.Area);
		_view.PasteClicked += PasteAt;
		Title = $"Valheim World Editor {BuildInfo.Version}";
		Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri($"avares://{typeof(MainWindow).Assembly.GetName().Name}/Assets/icon.png")));
		Width = Options.WindowWidth > 0 ? Options.WindowWidth : 1500;
		Height = Options.WindowHeight > 0 ? Options.WindowHeight : 950;
		Background = Ui.Bg;
		var record = new CheckBox { Content = "Record frame rates", FontSize = 12.5 };
		record.IsCheckedChanged += (_, _) => { _perf.On = record.IsChecked == true; _perf.Restart(); if (!_perf.On) _perf.Flush(); };
		ToolTip.SetTip(record, $"{Tips.Of("view.perf")} The file: {Ui.Tilde(PerfLog.FilePath)}");
		HelpCard = Help(record);
		ConfirmSave = text => Dialogs.Ask(this, "Save into the world", text, "Save");
		Ask = (title, text, yes, no) => Dialogs.Ask(this, title, text, yes, no);
		Tell = text => Dialogs.Tell(this, "Save", text);
		var tools = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = Ui.Gap,
			// Under the top bar, above the status bar.
			Margin = new Thickness(10, 70, 10, 58),
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			Children = { Tools.Rail, Tools.Options, SelectPanel.Card, MeasurePanel.Card, ShapePanel.Card, MountainPanel.Card, ScriptPanel.Card, PathPanel.Card, AreaPanel.Card, PastePanel.Card, PlacePanel.Card, PlacePanel.Chooser, MaskPanel.Card },
		};
		// Every panel of the column scrolls when the window is too short for it (a bar only then).
		foreach (var card in tools.Children.OfType<Border>())
		{
			if (card.Child is Control inner and not ScrollViewer)
			{
				card.Child = null;
				card.Child = new ScrollViewer { VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = inner };
			}
		}
		SelectPanel.Card.IsVisible = MeasurePanel.Card.IsVisible = ShapePanel.Card.IsVisible = MountainPanel.Card.IsVisible = ScriptPanel.Card.IsVisible = PathPanel.Card.IsVisible = AreaPanel.Card.IsVisible = PastePanel.Card.IsVisible = MaskPanel.Card.IsVisible = PlacePanel.Card.IsVisible = false;
		ShapePanel.Changed += () => _view.ShapeRadius = ShapePanel.Radius;
		MountainPanel.Changed += () =>
		{
			if (Tools.Mode == ToolMode.Mountain)
			{
				_view.ShapeRadius = MountainPanel.Spec.Reach;
				_view.MountainPreview = Mountain.Shape(MountainPanel.Spec);
			}
		};
		_view.ShapeClicked += (x, z) =>
		{
			if (Tools.Mode == ToolMode.Mountain)
			{
				_ = PutMountain(x, z);
			}
			else
			{
				PutShape(x, z);
			}
		};
		Tools.CaveChosen += () =>
		{
			PathPanel.ChooseCave();
			Tools.MarkCave(true);
		};
		PathPanel.ActionChanged += a => Tools.MarkCave(a == PathTool.Action.Cave);
		ScriptPanel.RunAsked += () => _ = RunScript();
		ScriptPanel.StopAsked += () => _stopScript?.Invoke();
		ScriptPanel.Message += t => _message.Text = t;
		Tools.Options.VerticalAlignment = VerticalAlignment.Top;
		Tools.Rail.VerticalAlignment = VerticalAlignment.Top;
		Tools.ToolChanged += t =>
		{
			_view.Tool = t;
			_view.Mode = Tools.Mode;
			SelectPanel.Card.IsVisible = Tools.SelectMode;
			MeasurePanel.Card.IsVisible = Tools.Mode == ToolMode.Measure;
			ShapePanel.Card.IsVisible = Tools.Mode == ToolMode.Shape;
			MountainPanel.Card.IsVisible = Tools.Mode == ToolMode.Mountain;
			ScriptPanel.Card.IsVisible = Tools.Mode == ToolMode.Script;
			_view.ShapeRadius = Tools.Mode == ToolMode.Mountain ? MountainPanel.Spec.Reach : ShapePanel.Radius;
			_view.MountainPreview = Tools.Mode == ToolMode.Mountain ? Mountain.Shape(MountainPanel.Spec) : null;
			PathPanel.Card.IsVisible = Tools.Mode == ToolMode.Path;
			if (Tools.Mode == ToolMode.Path)
			{
				Tools.MarkCave(_view.Path.Act == PathTool.Action.Cave);
			}
			AreaPanel.Card.IsVisible = Tools.Mode == ToolMode.Area;
			PastePanel.Card.IsVisible = Tools.Mode == ToolMode.Paste;
			PlacePanel.Card.IsVisible = Tools.Mode == ToolMode.Place;
			if (Tools.Mode != ToolMode.Place)
			{
				PlacePanel.Chooser.IsVisible = false;
				PlacePanel.KindsButton.Content = "+ Add kinds";
			}
			PlaceInput.Refresh();
			MaskPanel.Card.IsVisible = Tools.Mode is ToolMode.Brush or ToolMode.Path or ToolMode.Area or ToolMode.Shape or ToolMode.Mountain or ToolMode.Place;
			if (Tools.Mode == ToolMode.Area)
			{
				AreaPanel.Refresh();
			}
		};
		_view.SelectTool.Message += (text, _) => _message.Text = text;
		_view.StrokeEnded += msg =>
		{
			_message.Text = msg;
			if (Tools.Tool == BrushTool.Flatten && Tools.Brush.TargetFromClick)
			{
				Tools.ShowTarget(Tools.Brush.Target);
			}
			UpdateSaveBar();
		};
		KeyDown += OnKey;
		// Shift held: the Place brush shows that a drag removes.
		KeyDown += (_, e) => { if (e.Key is Avalonia.Input.Key.LeftShift or Avalonia.Input.Key.RightShift) PlaceInput.ShiftHeld(true); };
		KeyUp += (_, e) => { if (e.Key is Avalonia.Input.Key.LeftShift or Avalonia.Input.Key.RightShift) PlaceInput.ShiftHeld(false); };
		Closing += async (_, e) =>
		{
			var pending = _world?.Pending ?? _session?.Pending ?? (0, 0, 0, 0);
			if (_closeAnyway || pending is (0, 0, 0, 0))
			{
				Tunnel.Close();
				return;
			}
			e.Cancel = true;
			if (await Ask("Unsaved changes", $"{(_world?.IsLive == true ? "Not applied" : "Unsaved")}: {Describe(pending)}. Quit without {(_world?.IsLive == true ? "applying" : "saving")} them?", "Quit anyway", "Keep editing"))
			{
				_closeAnyway = true;
				Close();
			}
		};
		// Takes the mouse for the 3D view (see GlView.Attach).
		var surface = new Border { Background = Brushes.Transparent };
		Surface = surface;
		SetUpEditorWorld();
		_viewPanel = ViewPanel();
		// The panels on the right: under the top bar, one at a time.
		foreach (var right in new[] { _viewPanel, History.Card, Inspector.Card, Blueprints.Card, HelpCard })
		{
			right.Margin = new Thickness(10, 70, 10, 58);
			right.HorizontalAlignment = HorizontalAlignment.Right;
			right.VerticalAlignment = VerticalAlignment.Top;
		}
		ViewButton.Classes.Add("on");
		_editorPage = new Grid { Children = { _view, surface, PlayerLabels, tools, LocationNote, _viewPanel, History.Card, Inspector.Card, Blueprints.Card, HelpCard, TopBar(), StatusBar() } };
		_busy.Child = _busyText;
		_pages.Content = _editorPage;
		Content = new Grid { Children = { _pages, _busy } };
		_view.Attach(surface, this);
		_view.AltWheel = AltWheel;
		surface.PointerMoved += (_, e) => ShowCursor(e.GetPosition(surface));
		surface.PointerExited += (_, _) => ShowCursor(null);
		_view.StrokeEnded += _ => ShowCursor(_cursorAt);
		_view.Perf = _perf;
		_view.StatsChanged += s => _fps.Text = $"{s.Fps} frames/s · {s.WorkMs:0.0} ms of work each · {s.Objects:N0} objects ({s.Instances:N0} model parts in {s.Batches:N0} draws){(s.PendingModels > 0 ? $" · {s.PendingModels} kinds loading" : "")}";
		_view.SelectionChanged += _ =>
		{
			SelectPanel.Refresh();
			// Open: it follows the selection.
			var sel = _view.Selected;
			if (Inspector.IsOpen && sel.Count == 1 && sel.First() != Inspector.Index)
			{
				Inspect();
			}
		};
		_view.SelectionChanged += things => _selection.Text = things.Count == 0 ? "" : things.Count == 1
			? $"Selected: {ThingName(things[0])} at {things[0].Position.X:0.0}, {things[0].Position.Z:0.0} (height {things[0].Position.Y:0.0})"
			: $"Selected: {things.Count} objects ({string.Join(", ", things.GroupBy(ThingName).OrderByDescending(g => g.Count()).Take(4).Select(g => $"{g.Key} ×{g.Count()}"))})";
		_view.EyeChanged += e => _eye.Text = e switch
		{
			GlView.EyeMode.Walk => "Walking at eye height: WASD moves (Shift runs), right drag looks around · F flies",
			GlView.EyeMode.Fly => "Flying: WASD moves, Space up, C down (Shift faster) · F back to the usual view",
			_ => "",
		};
		_view.Status += t => { Options.Say(t); Dispatcher.UIThread.Post(() => _info.Text = t + "\n" + _info.Text); };
		Closing += (_, _) => { _perf.Flush(); GameLook.StopExport(); Prefs.Flush(); };
		RememberPrefs();
		_info.Text = "Loading the world…";
		Opened += async (_, _) =>
		{
			if (!load)
			{
				return;
			}
			Options.Say("window open");
			// The game's look: copied from the player's Valheim when missing or after a game update
			// (when driven by the tests, only whether it is there: they never copy from the game).
			GameLook.Check(_settings, export: !Options.Driver);
			if (Options.Direct)
			{
				// --world (and --zone): that area in the 3D editor at once.
				try
				{
					var world = await Task.Run(() => WorldSession.Open(WorldScene.FindWorld(Options.World)));
					_world = world;
					var scene = await Task.Run(() => WorldScene.Load(world, Options.ZoneX, Options.ZoneZ, Options.Size));
					await ShowEditor(scene);
				}
				catch (Exception ex)
				{
					_info.Text = "Could not open the world: " + ex.Message;
					Options.Say(_info.Text);
				}
			}
			else
			{
				ShowStart();
				// A world folder or a live game given on the command line: as if chosen on the start page.
				if (Options.LiveUrl is string url)
				{
					_start!.SetMode("server");
					_start.LiveUrl.Text = url;
					_start.LiveToken.Text = Options.LiveToken ?? "";
					await _start.ConnectUrl();
				}
				else if (Options.Folder is string folder)
				{
					_start!.SetMode("offline");
					_start.OpenFolder(folder);
				}
			}
			if (Options.Driver)
			{
				Driver.Start(this);
			}
		};
	}

}
