using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The prototype's window: the 3D view filling it, and a small panel with the frame rate, what is
// loaded, and the Record frame rates switch.
public sealed class MainWindow : Window
{
	private readonly GlView _view = new();
	private readonly TextBlock _fps = new() { FontSize = 13 }, _info = new() { FontSize = 12, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
	private readonly PerfLog _perf = new();
	private readonly TextBlock _eye = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(143, 240, 180)), TextWrapping = TextWrapping.Wrap };
	private readonly TextBlock _selection = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(224, 166, 75)), TextWrapping = TextWrapping.Wrap };
	private ModelStore? _models;
	private string Name(WorldScene.Thing t) => _models?.NameOf(t.Prefab) ?? TerrainEditor.Terrain.PrefabCatalog.DisplayName(t.Prefab) ?? t.Prefab.ToString();

	public GlView View => _view;
	internal ToolPanel Tools { get; } = new();
	internal SelectPanel SelectPanel { get; }
	internal MeasurePanel MeasurePanel { get; }
	internal ShapePanel ShapePanel { get; } = new();
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
	private readonly TextBlock _message = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(240, 190, 90)), TextWrapping = TextWrapping.Wrap, MaxWidth = 520 };
	internal Button UndoButton { get; } = new() { Content = "Undo", FontSize = 12, IsEnabled = false };
	internal Button RedoButton { get; } = new() { Content = "Redo", FontSize = 12, IsEnabled = false };
	internal Button SaveButton { get; } = new() { Content = "Save", FontSize = 12, IsEnabled = false };
	internal Button HistoryButton { get; } = new() { Content = "History", FontSize = 12 };
	internal HistoryPanel History { get; }
	internal Button MapButton { get; } = new() { Content = "◂ Map", FontSize = 12, IsVisible = false };

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
		Title = "Valheim World Editor (native preview)";
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
		if (_map == null)
		{
			_map = new MapPage();
			_map.BackToWorlds += async () => await LeaveWorld();
			_map.EditRequested += async (x, z, size) => await EditArea(x, z, size);
			_map.SaveRequested += async () => await SaveWorld();
			_map.DiscardRequested += async () => await DiscardWorld();
			_map.ReloadRequested += async () => await ReloadWorld();
			_map.Confirm = text => Dialogs.Ask(this, "Valheim World Editor", text, "Yes");
			_map.Tell = text => Dialogs.Tell(this, "Valheim World Editor", text);
		}
		Title = $"{_world.World.Name} · Valheim World Editor (native preview)";
		_map.Show(_world);
		_pages.Content = _map.View;
	}

	// An area of the world in the 3D editor.
	internal async Task EditArea(int x, int z, int size)
	{
		if (_world is not { } world)
		{
			return;
		}
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
		_info.Text = scene.LoadInfo + (_models == null ? "\nNo game models copied yet: boxes stand in (open the web editor once to copy the game's look)." : "")
			+ "\nView: click picks an object (Shift adds) · right drag turns · middle or left drag slides · wheel zooms · WASD moves · F walks and flies · E: select and move · 1-9, 0: brushes";
		_view.Show(scene, _models);
		if (scene.Session != null)
		{
			Edit(scene.Session);
		}
		MapButton.IsVisible = scene.Owner != null && !Options.Direct;
		Title = $"{scene.Name} · Valheim World Editor (native preview)";
		_pages.Content = _editorPage;
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
			if (!await ConfirmSave($"Write {what} into the world files?\n\nWorld folder: {w.World.Directory}\n\n"
				+ "• A full backup of the folder is made first, next to it.\n"
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
				if (r.BackupDirectory != null) msg += $"\n\nBackup: {r.BackupDirectory}";
				if (r.Skipped.Count > 0) msg += "\n\nNot saved:\n• " + string.Join("\n• ", r.Skipped);
			}
			await Tell(msg);
		}
		_map?.Show(w);
	}

	internal async Task DiscardWorld()
	{
		if (_world is not { } w || !await Dialogs.Ask(this, "Discard", $"Discard the unsaved changes ({Describe(w.Pending)})? The world is read again{(w.IsLive ? " from the game" : " from disk")}.", "Discard", "Keep them"))
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
			&& !await Dialogs.Ask(this, "Leave the world", $"{(w.IsLive ? "Not applied" : "Unsaved")}: {Describe(w.Pending)}. Leave the world without {(w.IsLive ? "applying" : "saving")} them?", "Leave anyway", "Stay"))
		{
			return;
		}
		_world = null;
		_session = null;
		Tunnel.Close();
		ShowStart();
	}
	internal InspectorPanel Inspector { get; }
	internal BlueprintsPanel Blueprints { get; }
	private Control _viewPanel = null!;
	internal TextBlock PendingText => _pending;
	internal TextBlock MessageText => _message;
	// Asks before writing into the world (replaced by tests).
	internal Func<string, Task<bool>> ConfirmSave { get; set; }
	internal Func<string, Task> Tell { get; set; }
	private EditSession? _session;
	internal EditSession? Session => _session;
	private bool _closeAnyway;

	// Starts editing a loaded scene (also used by tests with a scene of their own).
	internal void Edit(EditSession session)
	{
		_session = session;
		session.Brush = Tools.Brush;
		if (session.Scene.World is { } bw)
		{
			// The builder chosen for this world before, else the world's main builder.
			TerrainEditor.Save.WorldSave.Builder = PlacePanel.Memory.Builders.TryGetValue(bw.Name, out long known) ? known : Builders.Default(bw);
			FillBuilders();
		}
		session.Mask = Mask;
		session.Changed += () => Dispatcher.UIThread.Post(() => { UpdateSaveBar(); History.Refresh(); });
		// Saved: the objects were read again, with new indices.
		session.ThingsReset += () => Dispatcher.UIThread.Post(() => { if (Inspector.IsOpen) Inspector.Close(); });
		// The kinds the Select tool can replace with.
		var kinds = session.Scene.World?.Creatable.Where(p => NameOfPrefab(p) != null).OrderBy(p => NameOfPrefab(p), StringComparer.OrdinalIgnoreCase).ToList() ?? new();
		SelectPanel.ReplaceKinds = kinds;
		SelectPanel.ReplaceBox.ItemsSource = kinds.Select(p => NameOfPrefab(p)!).ToList();
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
		SaveButton.IsEnabled = z + d + a + r > 0;
		SaveButton.Content = s.IsLive ? "Apply live" : "Save";
		UndoButton.IsEnabled = s.CanUndo;
		RedoButton.IsEnabled = s.CanRedo;
		ToolTip.SetTip(UndoButton, s.UndoLabel is string u ? $"Undo {u} (Ctrl+Z)" : "Nothing to undo");
		ToolTip.SetTip(RedoButton, s.RedoLabel is string rl ? $"Redo {rl} (Ctrl+Shift+Z)" : "Nothing to redo");
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
			SaveButton.IsEnabled = false;
			_message.Text = "Applying to the running game…";
			var o = await s.ApplyLive();
			_message.Text = o.Message;
			UpdateSaveBar();
			return;
		}
		string what = s.PendingText.Replace("Unsaved: ", "");
		if (!await ConfirmSave($"Write {what} into the world files?\n\nWorld folder: {s.Scene.World.Directory}\n\n"
			+ "• A full backup of the folder is made first, next to it.\n"
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
			if (res.BackupDirectory != null)
			{
				msg += $"\n\nBackup: {res.BackupDirectory}";
			}
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

	private string? NameOfPrefab(int prefab) => _models?.NameOf(prefab) ?? TerrainEditor.Terrain.PrefabCatalog.DisplayName(prefab);

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

	private Control SaveBar()
	{
		UndoButton.Click += (_, _) => Undo();
		MapButton.Click += (_, _) => ShowMap();
		ToolTip.SetTip(MapButton, "Back to the world map (what is not saved stays pending)");
		HistoryButton.Click += (_, _) => History.Toggle();
		ToolTip.SetTip(HistoryButton, "Every change of this session: go back to one, or take out only one");
		RedoButton.Click += (_, _) => Redo();
		SaveButton.Click += async (_, _) => await Save();
		ToolTip.SetTip(SaveButton, "Write the changes into the world's files (Ctrl+S)");
		return new Border
		{
			Background = new SolidColorBrush(Color.FromArgb(235, 24, 28, 34)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(46, 53, 63)),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(10),
			Padding = new Thickness(10, 6),
			Margin = new Thickness(10),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			Child = new StackPanel
			{
				Spacing = 4,
				Children =
				{
					new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { MapButton, _pending, UndoButton, RedoButton, HistoryButton, SaveButton } },
					_message,
				},
			},
		};
	}

	private void OnKey(object? sender, Avalonia.Input.KeyEventArgs e)
	{
		// Typing in a box: its keys are its own.
		if (e.Source is TextBox)
		{
			return;
		}
		var mods = e.KeyModifiers;
		bool ctrl = mods.HasFlag(Avalonia.Input.KeyModifiers.Control) || mods.HasFlag(Avalonia.Input.KeyModifiers.Meta);
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
			// , . turn by 1° (Shift: 15°); . is clockwise seen from above, like the other tools.
			float step = mods.HasFlag(Avalonia.Input.KeyModifiers.Shift) ? 15 : 1;
			switch (e.Key)
			{
				case Avalonia.Input.Key.R: _view.Paste.TurnBy(90); break;
				case Avalonia.Input.Key.F: _view.Paste.Mirror = !_view.Paste.Mirror; _view.Paste.Notify(); break;
				case Avalonia.Input.Key.OemComma: _view.Paste.TurnBy(step); break;
				case Avalonia.Input.Key.OemPeriod: _view.Paste.TurnBy(-step); break;
				default: Tools.ChooseMode(ToolMode.Area); break;
			}
			e.Handled = true;
		}
		else if (ctrl && e.Key == Avalonia.Input.Key.S)
		{
			_ = Save();
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
		else if (!ctrl && e.Key is Avalonia.Input.Key.E or Avalonia.Input.Key.M or Avalonia.Input.Key.G or Avalonia.Input.Key.P or Avalonia.Input.Key.B or Avalonia.Input.Key.O or Avalonia.Input.Key.T)
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
	private readonly Dictionary<Overlays.Layer, CheckBox> _overlayBoxes = new();
	internal IReadOnlyDictionary<Overlays.Layer, CheckBox> OverlayBoxes => _overlayBoxes;
	internal CheckBox SlopeBox { get; } = new() { Content = "Slope colours", FontSize = 12 };
	// Building: who new pieces are built by (the player ids: the list's entries).
	internal ComboBox BuilderBox { get; } = new() { FontSize = 12, MinWidth = 200, MaxWidth = 260 };
	internal List<long> BuilderIds { get; } = new();
	private bool _fillingBuilders;
	internal CheckBox ContourBox { get; } = new() { Content = "Height lines every", FontSize = 12 };
	internal ComboBox ContourStepBox { get; } = new() { ItemsSource = new[] { "1", "2", "5", "10" }, SelectedIndex = 1, FontSize = 12, MinWidth = 60 };

	private static TextBlock Heading(string t) => new() { Text = t, FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Brushes.Gray, Margin = new Thickness(0, 8, 0, 2) };

	private Control ViewPanel()
	{
		var list = new StackPanel { Spacing = 2 };
		list.Children.Add(new TextBlock { Text = "VIEW", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 4) });
		foreach (var k in ObjectKinds.All)
		{
			var box = new CheckBox { Content = ObjectKinds.Label(k), IsChecked = _view.IsShown(k), FontSize = 12 };
			box.IsCheckedChanged += (_, _) => _view.SetShown(k, box.IsChecked == true);
			_kindBoxes[k] = box;
			list.Children.Add(box);
		}
		WaterBox.IsCheckedChanged += (_, _) => _view.ShowWater = WaterBox.IsChecked == true;
		list.Children.Add(WaterBox);
		list.Children.Add(Heading("OVERLAYS"));
		foreach (var (layer, label) in new[] { (Overlays.Layer.Borders, "Zone borders"), (Overlays.Layer.Markers, "Location markers"), (Overlays.Layer.Wards, "Ward areas"),
			(Overlays.Layer.Stations, "Build ranges"), (Overlays.Layer.Flatten, "Location flattening") })
		{
			var box = new CheckBox { Content = label, IsChecked = _view.IsOverlayShown(layer), FontSize = 12, Tag = label };
			box.IsCheckedChanged += (_, _) => _view.SetOverlay(layer, box.IsChecked == true);
			_overlayBoxes[layer] = box;
			list.Children.Add(box);
		}
		_view.OverlaysBuilt += o =>
		{
			void Count(Overlays.Layer l, int n) => _overlayBoxes[l].Content = $"{_overlayBoxes[l].Tag}  ({n})";
			Count(Overlays.Layer.Markers, o.Locations);
			Count(Overlays.Layer.Wards, o.Wards);
			Count(Overlays.Layer.Stations, o.Stations);
			Count(Overlays.Layer.Flatten, o.Flattened);
		};
		list.Children.Add(Heading("BUILDING"));
		ToolTip.SetTip(BuilderBox, "The player new pieces are built by: the game then treats them as player built (materials back, wards and private chests answer to that player)");
		list.Children.Add(new StackPanel { Spacing = 2, Children = { new TextBlock { Text = "Built by", FontSize = 12 }, BuilderBox } });
		BuilderBox.SelectionChanged += async (_, _) => await BuilderChosen();
		list.Children.Add(Heading("LOOK (game look)"));
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
				box.Content = $"{ObjectKinds.Label(k)}  ({counts.GetValueOrDefault(k):N0})";
			}
		};
		return new Border
		{
			Background = new SolidColorBrush(Color.FromArgb(235, 24, 28, 34)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(46, 53, 63)),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(10),
			Padding = new Thickness(12, 10),
			Margin = new Thickness(10),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Child = list,
		};
	}

	// load: false opens the window without a world (tests).
	public MainWindow(bool load = true)
	{
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
				FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Blueprints") { Patterns = new[] { "*.blueprint", "*.vbuild" } } },
			});
			return picked.Count > 0 && picked[0].Path.IsFile ? picked[0].Path.LocalPath : null;
		};
		Inspector.Message += t => { _message.Text = t; UpdateSaveBar(); };
		Inspector.Replaced += i => _view.Select(new[] { i });
		Inspector.Confirm = text => Dialogs.Ask(this, "Contents", text, "Apply anyway");
		Inspector.Closed += () => _viewPanel.IsVisible = true;
		SelectPanel.InspectButton.Click += (_, _) => Inspect();
		SelectPanel.ClaimButton.Click += (_, _) => Claim();
		ToolTip.SetTip(SelectPanel.ClaimButton, "Give the selected pieces placed without a builder the player chosen in View, Building");
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
		Tools.LoadStampAsked += async () =>
		{
			var picked = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
			{
				Title = "Use a grayscale picture as the brush shape",
				FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Pictures") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp" } } },
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
				FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("PNG pictures") { Patterns = new[] { "*.png" } } },
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
		_view.ScriptedClick += (at, size) =>
		{
			if (Tools.Mode == ToolMode.Place)
			{
				PlaceInput.Moved(at, size);
				int shown = PlaceInput.Shown.Length;
				if (Options.HoverOnly)
				{
					Options.Say($"place: {shown} shown, {string.Join(", ", PlaceTool.Chosen)} (hover only)");
					return;
				}
				PlaceInput.Down(at, size, false, false, false, 1);
				PlaceInput.Up(at, size);
				Options.Say($"place: {shown} shown, {string.Join(", ", PlaceTool.Chosen)}: {_message.Text} {_session?.PendingText}");
			}
		};
		_view.PathScripted += () =>
		{
			PathPanel.Refresh();
			ApplyPath();
			Options.Say($"path: {_view.Path.Points.Count} points, {PathPanel.Info.Text} {_message.Text} {_session?.PendingText}");
		};
		Title = "Valheim World Editor (native preview)";
		Width = 1500;
		Height = 950;
		Background = new SolidColorBrush(Color.FromRgb(20, 23, 28));
		var record = new CheckBox { Content = "Record frame rates", FontSize = 12 };
		record.IsCheckedChanged += (_, _) => { _perf.On = record.IsChecked == true; _perf.Restart(); if (!_perf.On) _perf.Flush(); };
		ToolTip.SetTip(record, $"A line every 0.2 s while the view is used, in {PerfLog.FilePath}");
		var panel = new Border
		{
			Background = new SolidColorBrush(Color.FromArgb(235, 24, 28, 34)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(46, 53, 63)),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(10),
			Padding = new Thickness(12, 10),
			Margin = new Thickness(10),
			MaxWidth = 380,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Bottom,
			Child = new StackPanel { Spacing = 6, Children = { _fps, _info, _eye, _selection, record } },
		};
		ConfirmSave = text => Dialogs.Ask(this, "Save into the world", text, "Save");
		Tell = text => Dialogs.Tell(this, "Save", text);
		var tools = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 8,
			Margin = new Thickness(10),
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Top,
			Children = { Tools.Rail, Tools.Options, SelectPanel.Card, MeasurePanel.Card, ShapePanel.Card, PathPanel.Card, AreaPanel.Card, PastePanel.Card, PlacePanel.Card, PlacePanel.Chooser, MaskPanel.Card },
		};
		SelectPanel.Card.IsVisible = MeasurePanel.Card.IsVisible = ShapePanel.Card.IsVisible = PathPanel.Card.IsVisible = AreaPanel.Card.IsVisible = PastePanel.Card.IsVisible = MaskPanel.Card.IsVisible = PlacePanel.Card.IsVisible = false;
		ShapePanel.Changed += () => _view.ShapeRadius = ShapePanel.Radius;
		_view.ShapeClicked += PutShape;
		Tools.Options.VerticalAlignment = VerticalAlignment.Top;
		Tools.Rail.VerticalAlignment = VerticalAlignment.Top;
		Tools.ToolChanged += t =>
		{
			_view.Tool = t;
			_view.Mode = Tools.Mode;
			SelectPanel.Card.IsVisible = Tools.SelectMode;
			MeasurePanel.Card.IsVisible = Tools.Mode == ToolMode.Measure;
			ShapePanel.Card.IsVisible = Tools.Mode == ToolMode.Shape;
			PathPanel.Card.IsVisible = Tools.Mode == ToolMode.Path;
			AreaPanel.Card.IsVisible = Tools.Mode == ToolMode.Area;
			PastePanel.Card.IsVisible = Tools.Mode == ToolMode.Paste;
			PlacePanel.Card.IsVisible = Tools.Mode == ToolMode.Place;
			if (Tools.Mode != ToolMode.Place)
			{
				PlacePanel.Chooser.IsVisible = false;
				PlacePanel.KindsButton.Content = "+ Add kinds";
			}
			PlaceInput.Refresh();
			MaskPanel.Card.IsVisible = Tools.Mode is ToolMode.Brush or ToolMode.Path or ToolMode.Area or ToolMode.Shape or ToolMode.Place;
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
			if (await Dialogs.Ask(this, "Unsaved changes", $"{(_world?.IsLive == true ? "Not applied" : "Unsaved")}: {Describe(pending)}. Quit without {(_world?.IsLive == true ? "applying" : "saving")} them?", "Quit anyway", "Keep editing"))
			{
				_closeAnyway = true;
				Close();
			}
		};
		// Takes the mouse for the 3D view (see GlView.Attach).
		var surface = new Border { Background = Brushes.Transparent };
		_viewPanel = ViewPanel();
		_editorPage = new Grid { Children = { _view, surface, panel, _viewPanel, tools, SaveBar(), History.Card, Inspector.Card, Blueprints.Card } };
		_busy.Child = _busyText;
		_pages.Content = _editorPage;
		Content = new Grid { Children = { _pages, _busy } };
		_view.Attach(surface, this);
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
			? $"Selected: {Name(things[0])} at {things[0].Position.X:0.0}, {things[0].Position.Z:0.0} (height {things[0].Position.Y:0.0})"
			: $"Selected: {things.Count} objects ({string.Join(", ", things.GroupBy(Name).OrderByDescending(g => g.Count()).Take(4).Select(g => $"{g.Key} ×{g.Count()}"))})";
		_view.EyeChanged += e => _eye.Text = e switch
		{
			GlView.EyeMode.Walk => "Walking at eye height: WASD moves (Shift runs), right drag looks around · F flies",
			GlView.EyeMode.Fly => "Flying: WASD moves, Space up, C down (Shift faster) · F back to the usual view",
			_ => "",
		};
		_view.Status += t => { Options.Say(t); Dispatcher.UIThread.Post(() => _info.Text = t + "\n" + _info.Text); };
		Closing += (_, _) => _perf.Flush();
		_info.Text = "Loading the world…";
		Opened += async (_, _) =>
		{
			if (!load)
			{
				return;
			}
			Options.Say("window open");
			if (Options.MapWorld is string mw)
			{
				await OpenWorld(() => Task.Run(() => WorldSession.Open(WorldScene.FindWorld(mw))), "Opening the world…");
				if (Options.MapAt is var (ax, az, ampp))
				{
					_map!.Map.LookAt(ax, az, ampp);
				}
				if (Options.Search is string q)
				{
					_map!.SearchBox.Text = q;
					await _map.Search();
					Options.Say($"search: {_map.SearchInfo.Text.Replace('\n', ' ')}");
					if (_map.Map.Pins.Count > 0)
					{
						var most = _map.Map.Pins.GroupBy(p => ((int)MathF.Floor((p.X + 32) / 64), (int)MathF.Floor((p.Y + 32) / 64))).MaxBy(g => g.Count())!;
						Options.Say($"search: most in zone {most.Key.Item1},{most.Key.Item2} ({most.Count()})");
					}
					_map.Hits.SelectedIndex = 0;
				}
				if (Options.MapEdit is var (mx, mz))
				{
					_map!.Pick(mx, mz);
					await EditArea(mx, mz, _map.Size);
				}
				return;
			}
			if (!Options.Direct)
			{
				ShowStart();
				if (Options.Shot is string shot)
				{
					// The start page is not drawn with OpenGL: Avalonia renders it into a picture.
					await Task.Delay(1500);
					var size = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
					using var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(size);
					rtb.Render(this);
					rtb.Save(shot);
					Options.Say($"picture: {shot}");
					(Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
				}
				return;
			}
			try
			{
				var world = await Task.Run(() => WorldSession.Open(WorldScene.FindWorld(Options.World)));
				_world = world;
				var scene = await Task.Run(() => WorldScene.Load(world, Options.ZoneX, Options.ZoneZ, Options.Size));
				await ShowEditor(scene);
				if (Options.AllOverlays)
				{
					foreach (var b in _overlayBoxes.Values) b.IsChecked = true;
					SlopeBox.IsChecked = ContourBox.IsChecked = true;
				}
				if (Options.StartTool is "select")
				{
					Tools.ChooseSelect();
				}
				else if (Options.StartTool is "measure")
				{
					Tools.ChooseMode(ToolMode.Measure);
				}
				else if (Options.StartTool is "shape")
				{
					Tools.ChooseMode(ToolMode.Shape);
				}
				else if (Options.StartTool is "place")
				{
					Tools.ChooseMode(ToolMode.Place);
				}
				else if (Options.StartTool is "area")
				{
					Tools.ChooseMode(ToolMode.Area);
				}
				else if (Options.StartTool is "path")
				{
					Tools.ChooseMode(ToolMode.Path);
				}
				else if (Enum.TryParse<BrushTool>(Options.StartTool, ignoreCase: true, out var startBrush))
				{
					Tools.Choose(startBrush);
				}
				for (int n = Options.EyeStart == "fly" ? 2 : Options.EyeStart == "walk" ? 1 : 0; n > 0; n--) _view.CycleEye();
			}
			catch (Exception ex)
			{
				_info.Text = "Could not open the world: " + ex.Message;
				Options.Say(_info.Text);
			}
		};
	}

}
