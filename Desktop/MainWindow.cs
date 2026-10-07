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
	internal PathPanel PathPanel { get; }
	internal AreaPanel AreaPanel { get; }
	internal PastePanel PastePanel { get; }

	// The save bar (top middle): what is waiting to be saved, undo, redo, Save, and the last message.
	private readonly TextBlock _pending = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Text = "All saved" };
	private readonly TextBlock _message = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(240, 190, 90)), TextWrapping = TextWrapping.Wrap, MaxWidth = 520 };
	internal Button UndoButton { get; } = new() { Content = "Undo", FontSize = 12, IsEnabled = false };
	internal Button RedoButton { get; } = new() { Content = "Redo", FontSize = 12, IsEnabled = false };
	internal Button SaveButton { get; } = new() { Content = "Save", FontSize = 12, IsEnabled = false };
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
		session.Changed += () => Dispatcher.UIThread.Post(UpdateSaveBar);
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
			var (t, rect, a) = paste.Apply(g, at);
			add.AddRange(a);
			touched = t.Count;
			return (t, rect);
		}, Array.Empty<int>(), add);
		_message.Text = $"Pasted{(paste.Count > 1 ? $" {paste.Count} copies" : "")}{(touched > 0 ? " with the ground" : "")}{(add.Count > 0 ? $", {add.Count} object(s)" : "")}. Click again to paste more, Esc when done.";
		UpdateSaveBar();
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
			var (t, rect, c) = path.Apply(g, s.Brush, s.Scene.Water);
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
					new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _pending, UndoButton, RedoButton, SaveButton } },
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
		else if (!ctrl && e.Key is Avalonia.Input.Key.E or Avalonia.Input.Key.M or Avalonia.Input.Key.G or Avalonia.Input.Key.P or Avalonia.Input.Key.B)
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
		MeasurePanel = new MeasurePanel(_view);
		PathPanel = new PathPanel(_view, Tools.Brush);
		PathPanel.ApplyAsked += ApplyPath;
		AreaPanel = new AreaPanel(_view, () => _session, prefab => _models?.NameOf(prefab) ?? TerrainEditor.Terrain.PrefabCatalog.DisplayName(prefab));
		AreaPanel.Message += t => { _message.Text = t; UpdateSaveBar(); };
		AreaPanel.SwitchToSelect += () => Tools.ChooseSelect();
		AreaPanel.Confirm = text => Dialogs.Ask(this, "Reset zones", text, "Reset when saving");
		AreaPanel.PickFolder = async () =>
		{
			var picked = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
			{
				Title = "Choose a backup (or copy) of this world: the folder with _main.<n>.chunks",
			});
			return picked.Count > 0 && picked[0].Path.IsFile ? picked[0].Path.LocalPath : null;
		};
		PastePanel = new PastePanel(_view.Paste);
		PastePanel.Done += () => Tools.ChooseMode(ToolMode.Area);
		_view.PasteClicked += PasteAt;
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
			Children = { Tools.Rail, Tools.Options, SelectPanel.Card, MeasurePanel.Card, ShapePanel.Card, PathPanel.Card, AreaPanel.Card, PastePanel.Card },
		};
		SelectPanel.Card.IsVisible = MeasurePanel.Card.IsVisible = ShapePanel.Card.IsVisible = PathPanel.Card.IsVisible = AreaPanel.Card.IsVisible = PastePanel.Card.IsVisible = false;
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
		Closing += async (_, e) =>
		{
			if (_closeAnyway || _session == null || _session.Pending is (0, 0, 0, 0))
			{
				return;
			}
			e.Cancel = true;
			if (await Dialogs.Ask(this, "Unsaved changes", $"{_session.PendingText}. Quit without saving them?", "Quit without saving", "Keep editing"))
			{
				_closeAnyway = true;
				Close();
			}
		};
		// Takes the mouse for the 3D view (see GlView.Attach).
		var surface = new Border { Background = Brushes.Transparent };
		Content = new Grid { Children = { _view, surface, panel, ViewPanel(), tools, SaveBar() } };
		_view.Attach(surface, this);
		_view.Perf = _perf;
		_view.StatsChanged += s => _fps.Text = $"{s.Fps} frames/s · {s.WorkMs:0.0} ms of work each · {s.Objects:N0} objects ({s.Instances:N0} model parts in {s.Batches:N0} draws){(s.PendingModels > 0 ? $" · {s.PendingModels} kinds loading" : "")}";
		_view.SelectionChanged += _ => SelectPanel.Refresh();
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
			try
			{
				var scene = await Task.Run(() => WorldScene.Load(WorldScene.FindWorld(Options.World), Options.ZoneX, Options.ZoneZ, Options.Size));
				var models = await Task.Run(ModelStore.Open);
				_models = models;
				_info.Text = scene.LoadInfo + (models == null ? "\nNo game models copied yet: boxes stand in (open the web editor once to copy the game's look)." : "")
					+ "\nView: click picks an object (Shift adds) · right drag turns · middle or left drag slides · wheel zooms · WASD moves · F walks and flies · E: select and move · 1-9, 0: brushes";
				_view.Show(scene, models);
				if (scene.Session != null)
				{
					Edit(scene.Session);
				}
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
