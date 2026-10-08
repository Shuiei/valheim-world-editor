using System.Numerics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// The Workshop in the window (Workshop.cs): a blank plot opened like an area, its building saved as a
// Homestead blueprint (Save blueprint), back to the start page (Start), and the support check: each
// building piece outlined in the colour the game gives its support, pieces that would fall in pink.
public partial class MainWindow
{
	// The cut: the building seen from a height (1 m steps, a half wall's height), to build inside it.
	internal Slider CutSlider { get; } = new() { Minimum = 0, Maximum = 40, SmallChange = 1, LargeChange = 2, TickFrequency = 1, IsSnapToTickEnabled = true, Width = 120, VerticalAlignment = VerticalAlignment.Center };
	internal TextBlock CutText { get; } = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Width = 74 };
	internal StackPanel CutBox { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 6, IsVisible = false };

	internal CheckBox SupportBox { get; } = new CheckBox { Content = "Support check", FontSize = 12, IsVisible = false, IsChecked = true, VerticalAlignment = VerticalAlignment.Center }.Classed("switch");
	private bool _inWorkshop;
	private string? _workshopName;
	// The blueprint's details (name, description, tags) and the file it was opened from (saving there again
	// does not ask before replacing it).
	private Homestead.Details? _workshopDetails;
	// How far the blueprint opened was lifted to put its lowest point on the ground (taken off again
	// when it is saved).
	private float _workshopLift;
	private string? _workshopFile;
	// The edits' version when the building was last saved (or opened).
	private int _workshopSaved;

	internal bool InWorkshop => _inWorkshop;
	private bool WorkshopDirty => _inWorkshop && _session != null && _session.Edits.Version != _workshopSaved;

	// The last support check (null: off, or not in the Workshop).
	internal Stability.Result? LastSupport { get; private set; }

	private void SetUpWorkshop()
	{
		SupportBox.Tip("workshop.support");
		CutBox.Children.Add(CutSlider);
		CutBox.Children.Add(CutText);
		CutSlider.Tip("workshop.cut");
		CutSlider.PropertyChanged += (_, e) =>
		{
			if (e.Property == Slider.ValueProperty)
			{
				SetCut((float)CutSlider.Value);
			}
		};
		CutText.Text = "Cut: off";
		SupportBox.IsCheckedChanged += (_, _) => RefreshSupport();
		Blueprints.EditAsked += async path => await OpenWorkshop(path);
	}

	// The Build panel's Library (once the panel is made).
	private void SetUpLibrary()
	{
		BuildPanel.Folder = () => Blueprints.Status.Folder;
		BuildPanel.OpenAsked += async path => await OpenWorkshop(path);
		BuildPanel.AddAsked += path => AddToWorkshop(path, null);
		BuildPanel.ImportAsked += async () =>
		{
			if (await Blueprints.PickFile() is string file && Blueprints.Import(file) != null)
			{
				BuildPanel.RenderLibrary();
			}
		};
	}

	// Drag and drop onto the 3D view: a blueprint from the Library, or .blueprint and .vbuild files from
	// the computer's files (imported into the library first). In the Workshop each goes where it is
	// dropped; in a world, files are only imported.
	private void SetUpDrops(Control surface)
	{
		Avalonia.Input.DragDrop.SetAllowDrop(surface, true);
		surface.AddHandler(Avalonia.Input.DragDrop.DragOverEvent, (_, e) =>
		{
			bool ours = e.DataTransfer.Contains(BuildPanel.BlueprintFormat) && _inWorkshop || e.DataTransfer.Contains(Avalonia.Input.DataFormat.File);
			e.DragEffects = ours ? Avalonia.Input.DragDropEffects.Copy : Avalonia.Input.DragDropEffects.None;
		});
		surface.AddHandler(Avalonia.Input.DragDrop.DropEvent, (_, e) =>
		{
			var files = (e.DataTransfer.TryGetFiles() ?? Array.Empty<Avalonia.Platform.Storage.IStorageItem>())
				.Select(f => Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(f)).OfType<string>().ToList();
			Drop(e.DataTransfer.TryGetValue(BuildPanel.BlueprintFormat), files, e.GetPosition(surface), surface.Bounds.Size);
		});
	}

	// What was dropped at a point of the view.
	internal void Drop(string? blueprint, IReadOnlyList<string> files, Avalonia.Point at, Avalonia.Size size)
	{
		Vector2? world = null;
		if (_session is { } s && _view.GridAt(at, size) is { } g)
		{
			world = new Vector2(s.Scene.X0 * 64f - 32f + g.X, s.Scene.Z0 * 64f - 32f + g.Y);
		}
		var paths = new List<string>();
		if (blueprint != null)
		{
			paths.Add(blueprint);
		}
		foreach (string f in files.Where(f => f.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".vbuild", StringComparison.OrdinalIgnoreCase)))
		{
			if (Blueprints.Import(f) is string name)
			{
				paths.Add(Path.Combine(Blueprints.Status.Folder, Homestead.FileName(name)));
			}
		}
		if (files.Count > 0 && paths.Count == (blueprint != null ? 1 : 0))
		{
			_message.Text = "Drop .blueprint (Homestead, PlanBuild) or .vbuild files.";
			return;
		}
		BuildPanel.RenderLibrary();
		if (!_inWorkshop)
		{
			return;
		}
		foreach (string p in paths)
		{
			AddToWorkshop(p, world);
		}
	}

	// A blueprint added to the building on the plot (at a world point x, z; null: the middle).
	internal void AddToWorkshop(string path, Vector2? at)
	{
		if (!_inWorkshop || _session is not { } s)
		{
			return;
		}
		try
		{
			var (placed, unknown, _) = Workshop.Add(s, path, null, at);
			_message.Text = $"Added “{Homestead.Read(path)?.Name ?? Path.GetFileNameWithoutExtension(path)}”: {placed} piece(s).{(unknown.Count > 0 ? $" {unknown.Count} kind(s) the game does not know were left out (mods?): {string.Join(", ", unknown.Take(5))}." : "")} Ctrl+Z takes it back.";
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			_message.Text = "Could not add that blueprint: " + ex.Message;
		}
		UpdateSaveBar();
	}

	// The Workshop, empty or with a blueprint's pieces on it. Leaves the open world first (asking when
	// something is not saved), and a building not saved as a blueprint (asking too).
	internal async Task OpenWorkshop(string? path)
	{
		if (_world is { } w)
		{
			if (w.Pending is not (0, 0, 0, 0)
				&& !await Ask("Leave the world", $"{(w.IsLive ? "Not applied" : "Unsaved")}: {Describe(w.Pending)}. Leave the world for the Workshop without {(w.IsLive ? "applying" : "saving")} them?", "Leave anyway", "Stay"))
			{
				return;
			}
			w.Dispose();
			_world = null;
			Tunnel.Close();
		}
		if (WorkshopDirty && !await Ask("Workshop", "The building on the plot is not saved as a blueprint. Open another anyway?", "Open anyway", "Stay"))
		{
			return;
		}
		string? name = null, text = null;
		Homestead.Details? details = null;
		if (path != null)
		{
			try
			{
				text = await File.ReadAllTextAsync(path);
				var parsed = BlueprintFormats.Parse(path, text);
				name = parsed.Name;
				details = Homestead.Read(path)?.Details is { } d ? d with { Name = name } : new Homestead.Details(name, parsed.Description ?? "", new());
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				_message.Text = "Could not open that blueprint: " + ex.Message;
				return;
			}
		}
		var scene = Workshop.Create(name != null ? $"Workshop · {name}" : "Workshop");
		await ShowEditor(scene);
		_inWorkshop = true;
		_workshopName = name;
		_workshopLift = 0;
		_workshopDetails = details;
		// Only Homestead's own folder is saved back to without asking.
		_workshopFile = path != null && string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFullPath(Blueprints.Status.Folder), StringComparison.Ordinal) ? path : null;
		string msg = "The Workshop: pick a piece in Build and click to put it down (it snaps like the game's hammer); Select (E) moves, turns and deletes; then Save blueprint.";
		if (path != null)
		{
			var (placed, unknown, lift) = Workshop.Open(scene.Session!, path, text);
			_workshopLift = lift;
			msg = $"Opened “{name}”: {placed} piece(s).{(unknown.Count > 0 ? $" {unknown.Count} kind(s) the game does not know were left out (mods?): {string.Join(", ", unknown.Take(5))}." : "")} Change it, then Save blueprint.";
		}
		_workshopSaved = scene.Session!.Edits.Version;
		EnterBuilding();
		MapButton.Content = Icons.With("back", "Start");
		MapButton.IsVisible = true;
		SupportBox.IsVisible = true;
		DiscardButton.IsVisible = false;
		ShowWorkshopCost();
		Tools.ChooseMode(ToolMode.Place);
		BuildPanel.Card.IsVisible = true;
		RefreshSupport();
		UpdateSaveBar();
		_message.Text = msg;
	}

	// Leaving an area: no Workshop any more (ShowEditor calls it for every area).
	private void ResetWorkshop()
	{
		LeaveBuilding();
		_inWorkshop = false;
		ToolTip.SetTip(_subtitle, null);
		MapButton.Content = Icons.With("back", "Map");
		SupportBox.IsVisible = false;
		DiscardButton.IsVisible = true;
		LastSupport = null;
		_view.ShowSupport(null);
	}

	internal async Task LeaveWorkshop()
	{
		if (WorkshopDirty && !await Ask("Leave the Workshop", "The building on the plot is not saved as a blueprint. Leave without saving it?", "Leave anyway", "Stay"))
		{
			return;
		}
		ResetWorkshop();
		_session = null;
		ShowStart();
	}

	// Save blueprint: the building pieces on the plot, as a Homestead blueprint (asks first when the
	// support check says some would fall in game).
	internal async Task SaveWorkshop()
	{
		if (_session is not { } s)
		{
			return;
		}
		_view.SelectTool.Commit();
		if (Workshop.Pieces(s.Scene) == 0)
		{
			await Tell("There is no building piece on the plot yet: place some first (Place tool).");
			return;
		}
		RefreshSupport();
		if (LastSupport is { Breaking: > 0 } r
			&& !await Ask("Save blueprint", $"{r.Breaking} piece(s) would fall in game: nothing holds them up enough (pink outlines). Save the blueprint anyway?", "Save anyway", "Keep building"))
		{
			return;
		}
		var clip = Workshop.Building(s.Scene, _workshopName ?? "My building", _workshopLift);
		if (await Blueprints.SaveBuilding(clip, _workshopDetails ?? new Homestead.Details(_workshopName ?? "My building", "", new()), _workshopFile) is { } details)
		{
			string saved = details.Name;
			_workshopName = saved;
			_workshopDetails = details;
			_workshopFile = Path.Combine(Blueprints.Status.Folder, Homestead.FileName(saved));
			BuildPanel.RenderLibrary();
			_workshopSaved = s.Edits.Version;
			_title.Text = $"Workshop · {saved}";
			UpdateSaveBar();
		}
	}

	// The support check over the plot's building pieces (Stability), shown as coloured outlines.
	internal void RefreshSupport()
	{
		if (!_inWorkshop || SupportBox.IsChecked != true || _session is not { } s)
		{
			LastSupport = null;
			_view.ShowSupport(null);
			return;
		}
		var index = new List<int>();
		var pieces = new List<Stability.Piece>();
		lock (s.Scene.Things)
		{
			for (int i = 0; i < s.Scene.Things.Count; i++)
			{
				var t = s.Scene.Things[i];
				if (t.Gone || PieceCatalog.Get(t.Prefab) == null || PrefabCatalog.NameOf(t.Prefab) is not string name)
				{
					continue;
				}
				index.Add(i);
				pieces.Add(new Stability.Piece(name, t.Position, BlueprintFormats.FromEuler(t.Rotation), t.Scale > 0 ? t.Scale : 1));
			}
		}
		var r = Stability.Solve(pieces, (_, _) => Workshop.Ground);
		LastSupport = r;
		var show = new Dictionary<int, float>();
		for (int k = 0; k < index.Count; k++)
		{
			if (!r.Free[k])
			{
				show[index[k]] = r.Breaks[k] ? -2 : r.Colour[k];
			}
		}
		_view.ShowSupport(show);
	}

	// What the building on the plot costs to build in game, under the title (all of it in the tip).
	internal string WorkshopCost { get; private set; } = "";

	private void ShowWorkshopCost()
	{
		if (!_inWorkshop || _session is not { } s)
		{
			return;
		}
		var cost = TerrainEditor.Terrain.PieceCost.Of(Workshop.Kinds(s.Scene));
		WorkshopCost = cost.Describe(5);
		_subtitle.Text = Workshop.Pieces(s.Scene) == 0 ? "A blank plot: only the building is kept in the blueprint" : "Cost: " + WorkshopCost;
		ToolTip.SetTip(_subtitle, cost.Materials.Count == 0 ? null
			: string.Join("\n", cost.Materials.Select(m => $"{m.Name}: {m.Amount}")) + (cost.Stations.Count > 0 ? $"\nNear: {string.Join(", ", cost.Stations)}" : "")
				+ (cost.Unknown.Count > 0 ? $"\nNo recipe known for: {string.Join(", ", cost.Unknown.Keys.Take(5))}" : ""));
	}

	private void WorkshopSaveBar(EditSession s)
	{
		ShowWorkshopCost();
		int n = Workshop.Pieces(s.Scene);
		bool dirty = WorkshopDirty;
		string falls = LastSupport is { Breaking: > 0 } r ? $" · {r.Breaking} would fall" : "";
		_pending.Text = n == 0 ? "No pieces yet" : $"{n} piece(s){falls}{(dirty ? " · not saved as a blueprint" : " · saved")}";
		SaveButton.IsEnabled = n > 0;
		SaveButton.Content = "Save blueprint";
		SaveButton.Tip("top.saveBlueprint");
		_pendingPill.Background = dirty ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2c2416")) : Avalonia.Media.Brushes.Transparent;
		_pendingPill.BorderBrush = dirty ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#7a5a2a")) : Ui.Line;
		_pending.Foreground = dirty ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#f3d29b")) : Ui.Muted;
		_liveBadge.IsVisible = false;
		UndoButton.IsEnabled = s.CanUndo;
		RedoButton.IsEnabled = s.CanRedo;
	}

	// After an edit in the Workshop: the support check again (a moment later, once per batch of edits).
	private bool _supportQueued;

	private void QueueSupport()
	{
		if (!_inWorkshop || _supportQueued)
		{
			return;
		}
		_supportQueued = true;
		Dispatcher.UIThread.Post(() =>
		{
			_supportQueued = false;
			RefreshSupport();
			if (_session is { } s)
			{
				WorkshopSaveBar(s);
			}
		}, DispatcherPriority.Background);
	}

	// What the Workshop changes and gives back when it closes: the Place tool's settings (Build sets
	// them for building pieces) and the zone borders (the plot is no world).
	private sealed record Kept(List<string> Chosen, PlaceTool.Modes Mode, bool OneAtATime, bool OneAtATimeByHand, bool RandomYaw, float Tilt, float SizeMin,
		float SizeMax, PlaceTool.Elevations Elevation, bool SnapTo, bool OnTop, float Rotation, bool Borders);
	// (The unsaved marks: everything on the plot is new, so they would only cover the building.)

	private Kept? _kept;

	// The world tools away (the rail keeps Build, Select and View), the View panel and the Mask closed,
	// no zone borders; the Place tool set for building.
	private void EnterBuilding()
	{
		if (_kept == null)
		{
			var t = PlaceTool;
			_kept = new Kept(t.Chosen.ToList(), t.Mode, t.OneAtATime, t.OneAtATimeByHand, t.RandomYaw, t.Tilt, t.SizeMin, t.SizeMax, t.Elevation, t.SnapTo, t.OnTop, t.Rotation,
				_view.IsOverlayShown(Overlays.Layer.Borders));
		}
		Tools.SetWorkshop(true);
		_view.SetOverlay(Overlays.Layer.Borders, false);
		_view.ShowNewMarkers = false;
		CutBox.IsVisible = true;
		CutSlider.Value = 0;
		// Solid pieces (the View panel and its See-through switch are away).
		_view.SeeThroughBuildings = false;
		ShowRight(null);
		ViewButton.IsVisible = false;
		MaskPanel.Card.IsVisible = false;
		BuildPanel.Start();
	}

	private void LeaveBuilding()
	{
		if (_kept is not { } k)
		{
			return;
		}
		_kept = null;
		var t = PlaceTool;
		t.Chosen.Clear();
		t.Chosen.AddRange(k.Chosen);
		(t.Mode, t.OneAtATime, t.OneAtATimeByHand, t.RandomYaw, t.Tilt, t.SizeMin, t.SizeMax, t.Elevation, t.SnapTo, t.OnTop, t.Rotation) =
			(k.Mode, k.OneAtATime, k.OneAtATimeByHand, k.RandomYaw, k.Tilt, k.SizeMin, k.SizeMax, k.Elevation, k.SnapTo, k.OnTop, k.Rotation);
		t.GridStep = 0;
		t.Building = false;
		t.HeightNudge = 0;
		t.AimRay = null;
		PlaceInput.TurnStep = null;
		Tools.SetWorkshop(false);
		_view.SetOverlay(Overlays.Layer.Borders, k.Borders);
		CutBox.IsVisible = false;
		CutSlider.Value = 0;
		_view.ShowNewMarkers = UnsavedBox.IsChecked == true;
		_view.SeeThroughBuildings = SeeThroughBox.IsChecked == true;
		ViewButton.IsVisible = true;
		BuildPanel.Card.IsVisible = false;
		PlacePanel.Fill();
		t.Notify();
	}

	// The cut at so many metres above the plot (0: none): nothing drawn above it, and the cursor goes
	// through what it hides.
	private void SetCut(float metres)
	{
		float? y = metres > 0 ? Workshop.Ground + metres : null;
		_view.CutY = y;
		PlaceTool.CutY = y;
		CutText.Text = metres > 0 ? $"Cut at {metres:0} m" : "Cut: off";
		PlaceInput.Refresh();
	}
}
