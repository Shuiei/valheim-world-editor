using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

// Blueprints (WorldEdit's schematics): copies kept under a name, pasted again in any area or world. They
// are Homestead's blueprints (Homestead.cs): .blueprint files with a picture in Homestead's folder of
// Valheim's save folder, the same files Homestead lists in game, so a building saved here is built in
// game with Homestead (and one saved in game is pasted here). The panel says whether Homestead is
// installed in the game's BepInEx (or a mod manager profile), and warns when it is not. Blueprints
// kept in the editor's own format before (the data folder's blueprints/) are still listed, and can be
// moved to Homestead. Other mods' files (PlanBuild .blueprint, .vbuild) are imported, and written.
// Each blueprint shows its picture (BuildingPicture), what it costs to build in game (PieceCost), its
// description and tags; the search looks through all of them.
public sealed class BlueprintsPanel
{
	public Control Card { get; }
	// The editor's own blueprints from before (blueprints/ of the data folder).
	public BlueprintStore Store { get; set; } = BlueprintStore.Default;
	// Whether Homestead is installed, and its blueprint folder (tests give their own).
	internal Func<Homestead.Status> FindHomestead { get; set; } = () => Homestead.Find(AppSettings.Load());
	internal TextBox Search { get; } = new() { PlaceholderText = "Search blueprints", FontSize = 12 };
	internal Button ImportButton { get; } = new() { Content = "Import file…", FontSize = 12 };
	internal Button NewButton { get; } = new() { Content = "New in Workshop", FontSize = 12 };
	// Edit (a blueprint file) or New in Workshop (null): the window opens the Workshop.
	public event Action<string?>? EditAsked;
	internal Button GetHomesteadButton { get; } = new() { Content = "Get Homestead", FontSize = 11, Padding = new Thickness(6, 1) };
	internal StackPanel List { get; } = new() { Spacing = 4 };
	internal TextBlock Banner { get; } = new() { FontSize = 11.5, TextWrapping = TextWrapping.Wrap };
	private readonly Border _bannerBox;
	private readonly TextBlock _folder = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
	private readonly Func<WorldScene?> _scene;
	private readonly PasteTool _paste;
	public event Action<string>? Message;
	// A blueprint went onto the clipboard: the window starts pasting.
	public event Action? Pasting;
	// Asks for a blueprint's name, description and tags (given the ones to start from, and what it costs).
	internal Func<Homestead.Details, string, Task<Homestead.Details?>> AskDetails { get; set; } = (_, _) => Task.FromResult<Homestead.Details?>(null);
	// The game's models, for the blueprints' pictures (null: their boxes are drawn).
	internal Func<ModelStore?> Models { get; set; } = () => null;
	internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);
	internal Func<Task<string?>> PickFile { get; set; } = () => Task.FromResult<string?>(null);
	internal Func<Uri, Task<bool>> OpenUrl { get; set; } = _ => Task.FromResult(false);

	// Homestead's blueprints have ids "hs:<file name>"; the editor's older ones their store id.
	private const string HomesteadId = "hs:";

	public BlueprintsPanel(PasteTool paste, Func<WorldScene?> scene)
	{
		_paste = paste;
		_scene = scene;
		var close = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(6, 0) }.Tip("card.close");
		Search.Tip("blueprints.search");
		close.Click += (_, _) => Card!.IsVisible = false;
		Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Render(); };
		ImportButton.Tip("blueprints.import");
		ImportButton.Click += async (_, _) => { if (await PickFile() is string path) Import(path); };
		NewButton.Tip("blueprints.new");
		NewButton.Click += (_, _) => EditAsked?.Invoke(null);
		GetHomesteadButton.Tip("blueprints.getHomestead");
		GetHomesteadButton.Click += async (_, _) => await OpenUrl(new Uri(Homestead.PageUrl));
		_bannerBox = new Border
		{
			CornerRadius = new CornerRadius(6),
			Padding = new Thickness(8, 6),
			Child = new StackPanel { Spacing = 4, Children = { Banner, GetHomesteadButton } },
		};
		Card = new Border
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
			IsVisible = false,
			Child = new StackPanel
			{
				Width = 340,
				Spacing = 6,
				Children =
				{
					new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = "Blueprints", FontSize = 14, FontWeight = FontWeight.SemiBold }, Col(close, 1) } },
					_bannerBox,
					new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4, Children = { Search, Col(ImportButton, 1) } },
					NewButton,
					new ScrollViewer { MaxHeight = 640, Content = List },
					_folder,
				},
			},
		};
	}

	private static Control Col(Control c, int col)
	{
		Grid.SetColumn(c, col);
		return c;
	}

	public void Toggle(bool? open = null)
	{
		Card.IsVisible = open ?? !Card.IsVisible;
		Refresh();
	}

	private List<BlueprintStore.Summary> _older = new();
	private List<Homestead.Entry> _homestead = new();

	// Homestead as found when the panel was last refreshed (looked for when first needed).
	private Homestead.Status? _status;
	internal Homestead.Status Status => _status ??= FindHomestead();

	public void Refresh()
	{
		_status = FindHomestead();
		if (!Card.IsVisible)
		{
			return;
		}
		_homestead = Homestead.List(Status.Folder);
		_older = Store.List();
		if (Status.Installed)
		{
			Banner.Text = $"Homestead {Status.Version} is installed ({string.Join(", ", Status.Where.Distinct())}): these are its blueprints, shared with its hammer tab in game.";
			Banner.Foreground = Ui.Muted;
			_bannerBox.Background = Brushes.Transparent;
			GetHomesteadButton.IsVisible = false;
		}
		else
		{
			Banner.Text = "Homestead is not installed in your Valheim's BepInEx (nor in a mod manager profile). Blueprints are still saved where it looks for them, "
				+ "so they show in its hammer tab, ready to build in game, once you install it.";
			Banner.Foreground = new SolidColorBrush(Color.FromRgb(240, 180, 90));
			_bannerBox.Background = new SolidColorBrush(Color.FromArgb(40, 240, 160, 60));
			GetHomesteadButton.IsVisible = true;
		}
		_folder.Text = $"Homestead's blueprints: {Ui.Tilde(Status.Folder)}." + (_older.Count > 0 ? $" Older editor blueprints: {Ui.Tilde(Store.Directory)}." : "");
		Render();
	}

	private static Bitmap? Picture(string? dataUrl)
	{
		if (dataUrl == null || !dataUrl.StartsWith("data:image/png;base64,", StringComparison.Ordinal))
		{
			return null;
		}
		try
		{
			using var ms = new MemoryStream(Convert.FromBase64String(dataUrl["data:image/png;base64,".Length..]));
			return new Bitmap(ms);
		}
		catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
		{
			return null;
		}
	}

	private static Bitmap? PictureFile(string? path)
	{
		if (path == null)
		{
			return null;
		}
		try
		{
			using var s = File.OpenRead(path);
			return new Bitmap(s);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
		{
			return null;
		}
	}

	// One row: picture, name, what it holds, its buttons.
	private Border Row(Bitmap? picture, string name, string meta, Action<Func<string, string, Action, Button>> buttons, string? cost = null, string? description = null, IReadOnlyList<string>? tags = null)
	{
		var acts = new WrapPanel { ItemSpacing = 4, LineSpacing = 4 };
		Button Act(string text, string tip, Action a)
		{
			var btn = new Button { Content = text, FontSize = 11, Padding = new Thickness(6, 1) };
			ToolTip.SetTip(btn, tip);
			btn.Click += (_, _) => a();
			acts.Children.Add(btn);
			return btn;
		}
		buttons(Act);
		var text = new StackPanel { Spacing = 2 };
		text.Children.Add(new TextBlock { Text = name, FontSize = 12, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
		text.Children.Add(new TextBlock { Text = meta, FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap });
		if (cost != null)
		{
			text.Children.Add(new TextBlock { Text = cost, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(214, 190, 140)), TextWrapping = TextWrapping.Wrap });
		}
		if (!string.IsNullOrWhiteSpace(description))
		{
			text.Children.Add(new TextBlock { Text = description, FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxLines = 3, TextTrimming = TextTrimming.CharacterEllipsis });
		}
		if (tags is { Count: > 0 })
		{
			var chips = new WrapPanel { ItemSpacing = 3, LineSpacing = 3 };
			foreach (string tag in tags)
			{
				chips.Children.Add(new Border
				{
					Background = new SolidColorBrush(Color.FromArgb(50, 120, 160, 220)),
					CornerRadius = new CornerRadius(8),
					Padding = new Thickness(6, 0),
					Child = new TextBlock { Text = tag, FontSize = 10.5 },
				});
			}
			text.Children.Add(chips);
		}
		text.Children.Add(acts);
		var grid = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("80,*"),
			ColumnSpacing = 8,
			Children =
			{
				new Image { Source = picture, Width = 80, Height = 80, VerticalAlignment = VerticalAlignment.Top },
				Col(text, 1),
			},
		};
		return new Border { Padding = new Thickness(4), CornerRadius = new CornerRadius(6), Child = grid };
	}

	private void Render()
	{
		List.Children.Clear();
		string q = Search.Text?.Trim() ?? "";
		bool Match(string name) => q == "" || name.Contains(q, StringComparison.OrdinalIgnoreCase);
		// Every word somewhere in the name, creator, description or tags.
		bool Matches(Homestead.Entry e) => q.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(w =>
			e.Name.Contains(w, StringComparison.OrdinalIgnoreCase) || (e.Creator ?? "").Contains(w, StringComparison.OrdinalIgnoreCase)
			|| e.Description.Contains(w, StringComparison.OrdinalIgnoreCase) || e.Tags.Any(t => t.Contains(w, StringComparison.OrdinalIgnoreCase)));
		var homestead = _homestead.Where(Matches).ToList();
		var older = _older.Where(b => Match(b.Name)).ToList();
		if (homestead.Count + older.Count == 0)
		{
			List.Children.Add(new TextBlock
			{
				Text = _homestead.Count + _older.Count > 0 ? "Nothing matches." : "No blueprints yet. Copy something (Area or Select tool, Ctrl+C), then “Save blueprint…”.",
				FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
			});
			return;
		}
		foreach (var e in homestead)
		{
			string id = HomesteadId + Path.GetFileName(e.Path);
			string meta = $"{e.Pieces} piece(s){(e.Creator is { Length: > 0 } c ? $" · by {c}" : "")}{(e.World != null ? $" · from {e.World}" : "")}";
			var cost = TerrainEditor.Terrain.PieceCost.Of(e.Kinds);
			List.Children.Add(Row(PictureFile(e.Picture), e.Name, meta, act =>
			{
				act("Paste", "Put this blueprint on the clipboard and start pasting it", () => Paste(id));
				act("Edit", "Open it in the Workshop, a blank plot, to change it", () => EditAsked?.Invoke(e.Path));
				act("Details", "Change its name, description and tags", async () => await EditDetails(e));
				act(".vbuild", "Write it as a .vbuild file (BuildShare and older tools)", () => Export(id, "vbuild"));
				act("Delete", "Delete this blueprint (its file and picture): Homestead loses it too", async () => await Delete(id, e.Name));
			}, "Cost: " + cost.Describe(), e.Description, e.Tags));
		}
		if (older.Count > 0)
		{
			List.Children.Add(new TextBlock { Text = "Older editor blueprints", FontSize = 11.5, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
		}
		foreach (var b in older)
		{
			string meta = $"{b.W} × {b.H} m · {b.Objects} object(s){(b.Ground ? " · ground" : "")}{(b.World != null ? $" · from {b.World}" : "")}{(b.Source != null ? $" · {b.Source}" : "")}";
			List.Children.Add(Row(Picture(b.Thumb), b.Name, meta, act =>
			{
				act("Paste", "Put this blueprint on the clipboard and start pasting it", () => Paste(b.Id));
				act("To Homestead", "Save it as a Homestead blueprint (objects only), to build it in game", () => ToHomestead(b.Id));
				act(".vbuild", "Write it as a .vbuild file", () => Export(b.Id, "vbuild"));
				act("Delete", "Delete this blueprint file", async () => await Delete(b.Id, b.Name));
			}));
		}
	}

	// A blueprint as a copy (the clipboard format), its name and the world it came from, or null.
	private (JsonObject Clip, string Name, string? World)? Load(string id)
	{
		if (id.StartsWith(HomesteadId, StringComparison.Ordinal))
		{
			string path = Path.Combine(Status.Folder, id[HomesteadId.Length..]);
			if (Path.GetFileName(path) != id[HomesteadId.Length..] || !File.Exists(path))
			{
				return null;
			}
			var parsed = BlueprintFormats.Parse(path, File.ReadAllText(path));
			var world = _scene()?.World;
			var clip = BlueprintFormats.ToClip(parsed, n => world?.CanCreate(StableHash.Of(n)) ?? TerrainEditor.Terrain.PrefabCatalog.Get(StableHash.Of(n)) != null, out _);
			return (clip, parsed.Name, null);
		}
		if (Store.Read(id) is not string json || JsonNode.Parse(json) is not JsonObject doc || doc["clip"] is not JsonObject c)
		{
			return null;
		}
		return (c, (string?)doc["name"] ?? id, (string?)doc["world"]);
	}

	// Writes a Homestead blueprint and its picture; the name of the file written.
	private string WriteHomestead(JsonObject clip, Homestead.Details details, string? world)
	{
		Directory.CreateDirectory(Status.Folder);
		string path = Path.Combine(Status.Folder, Homestead.FileName(details.Name));
		string tmp = path + ".tmp";
		File.WriteAllText(tmp, Homestead.Write(clip, details.Name, "Valheim World Editor", world, DateTime.Now, details.Description, details.Tags));
		File.Move(tmp, path, overwrite: true);
		File.WriteAllBytes(Path.ChangeExtension(path, ".png"), BuildingPicture.Draw(BuildingPicture.FromClip(clip), Models()));
		return path;
	}

	private static Homestead.Details Named(string name) => new(name, "", new());

	// Asks for the details (none: null), and before replacing a blueprint of that name.
	private async Task<Homestead.Details?> Ask(Homestead.Details start, JsonObject clip, string? keep = null)
	{
		var cost = TerrainEditor.Terrain.PieceCost.Of(BuildingPicture.FromClip(clip).Select(p => p.Name));
		var d = await AskDetails(start, cost.Describe(8));
		if (d == null || string.IsNullOrWhiteSpace(d.Name))
		{
			return null;
		}
		d = d with { Name = d.Name.Trim(), Description = d.Description.Trim() };
		if (!string.Equals(Homestead.FileName(d.Name), keep, StringComparison.OrdinalIgnoreCase) && HomesteadExists(d.Name)
			&& !await Confirm($"A blueprint called “{d.Name}” already exists. Replace it?"))
		{
			return null;
		}
		return d;
	}

	private bool HomesteadExists(string name) => File.Exists(Path.Combine(Status.Folder, Homestead.FileName(name)));

	private static bool HasGround(JsonObject clip) => clip["rel"] is JsonArray rel && rel.Any(v => v != null && (int)v != -32768);

	private string Reminder => Status.Installed ? "" : " Homestead is not installed in your game yet: it shows there once you install it.";

	// Saves the clipboard as a Homestead blueprint (asks for the name; one with that name is replaced
	// after asking). Homestead blueprints hold pieces only: copied ground is not kept.
	public async Task<string?> Save()
	{
		var clip = _paste.Clip;
		if (clip == null)
		{
			Message?.Invoke("Copy something first (Area or Select tool, Ctrl+C), then save it as a blueprint.");
			return null;
		}
		_status = FindHomestead();
		var json = CopyFormat.ToJson(clip);
		if (await Ask(Named(clip.Name ?? $"Blueprint {DateTime.Now:yyyy-MM-dd HH.mm}"), json) is not { } details)
		{
			return null;
		}
		string name = details.Name;
		clip.Name = name;
		json = CopyFormat.ToJson(clip);
		try
		{
			WriteHomestead(json, details, _scene()?.World?.Name);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Message?.Invoke($"Could not save the blueprint: {ex.Message}");
			return null;
		}
		Message?.Invoke($"Saved the blueprint “{name}” ({clip.Objects.Count} object(s)) for Homestead."
			+ (HasGround(json) ? " Its ground shape is not kept: Homestead blueprints hold pieces only." : "") + Reminder);
		Refresh();
		return name;
	}

	// The Workshop's building as a Homestead blueprint (asks for the details, suggested; one with that
	// name is replaced after asking, except the file it was opened from). What was saved, or null.
	public async Task<Homestead.Details?> SaveBuilding(JsonObject clip, Homestead.Details suggested, string? openedFrom = null)
	{
		_status = FindHomestead();
		if (await Ask(suggested, clip, openedFrom != null ? Path.GetFileName(openedFrom) : null) is not { } details)
		{
			return null;
		}
		string name = details.Name;
		try
		{
			WriteHomestead(clip, details, null);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Message?.Invoke($"Could not save the blueprint: {ex.Message}");
			return null;
		}
		int count = (clip["objects"] as JsonArray)?.Count ?? 0;
		Message?.Invoke($"Saved the blueprint “{name}” ({count} piece(s)) for Homestead.{Reminder}");
		Refresh();
		return details;
	}

	// Details…: a blueprint's name, description and tags, changed in its file (renamed with its
	// picture when the name changes and the new file name is free).
	public async Task<bool> EditDetails(Homestead.Entry e)
	{
		var d = await AskDetails(e.Details, TerrainEditor.Terrain.PieceCost.Of(e.Kinds).Describe(8));
		if (d == null || string.IsNullOrWhiteSpace(d.Name))
		{
			return false;
		}
		d = d with { Name = d.Name.Trim(), Description = d.Description.Trim() };
		try
		{
			string path = e.Path, target = Path.Combine(Path.GetDirectoryName(path)!, Homestead.FileName(d.Name));
			File.WriteAllText(path, Homestead.WithDetails(File.ReadAllText(path), d));
			if (!string.Equals(target, path, StringComparison.Ordinal) && !File.Exists(target))
			{
				File.Move(path, target);
				if (File.Exists(Path.ChangeExtension(path, ".png")))
				{
					File.Move(Path.ChangeExtension(path, ".png"), Path.ChangeExtension(target, ".png"));
				}
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Message?.Invoke($"Could not change the blueprint: {ex.Message}");
			return false;
		}
		Message?.Invoke($"“{d.Name}”: details saved.");
		Refresh();
		return true;
	}

	// Loads a blueprint onto the clipboard and starts pasting. Object ids only mean something in the
	// world the copy came from; kinds this world cannot make are left out.
	public void Paste(string id)
	{
		if (Load(id) is not var (clip, name, from))
		{
			Message?.Invoke("That blueprint could not be read.");
			Refresh();
			return;
		}
		var world = _scene()?.World;
		var data = CopyFormat.FromJson(clip, name, keepSources: world != null && from == world.Name);
		var missing = data.Objects.Where(o => world != null && !world.CanCreate(o.Prefab)).Select(o => o.Name).Distinct().ToList();
		data.Objects.RemoveAll(o => missing.Contains(o.Name));
		_paste.Clip = data;
		_paste.Notify();
		Card.IsVisible = false;
		Pasting?.Invoke();
		Message?.Invoke($"“{name}” is on the clipboard: click to place it.{(missing.Count > 0 ? $" {missing.Count} kind(s) are unknown here and are left out: {string.Join(", ", missing.Take(5))}{(missing.Count > 5 ? "…" : "")}." : "")}");
	}

	// An older editor blueprint as a Homestead blueprint (objects only).
	public string? ToHomestead(string id)
	{
		if (Load(id) is not var (clip, name, world))
		{
			Message?.Invoke("That blueprint could not be read.");
			return null;
		}
		string target = name;
		for (int n = 2; HomesteadExists(target); n++)
		{
			target = $"{name} ({n})";
		}
		WriteHomestead(clip, Named(target), world);
		Message?.Invoke($"“{target}” is now a Homestead blueprint.{(HasGround(clip) ? " Its ground shape is not kept: Homestead blueprints hold pieces only." : "")}{Reminder}");
		Refresh();
		return target;
	}

	public async Task Delete(string id, string name)
	{
		if (!await Confirm($"Delete the blueprint “{name}”? Its file is removed."))
		{
			return;
		}
		if (id.StartsWith(HomesteadId, StringComparison.Ordinal))
		{
			string path = Path.Combine(Status.Folder, id[HomesteadId.Length..]);
			if (Path.GetFileName(path) == id[HomesteadId.Length..])
			{
				File.Delete(path);
				File.Delete(Path.ChangeExtension(path, ".png"));
			}
		}
		else
		{
			Store.Delete(id);
		}
		Refresh();
	}

	// Writes a blueprint for PlanBuild (or as .vbuild) into the export folder (objects only).
	public string? Export(string id, string format)
	{
		if (Load(id) is not var (clip, name, _))
		{
			Message?.Invoke("Could not export that blueprint.");
			return null;
		}
		string text = BlueprintFormats.Write(clip, format, name, n => TerrainEditor.Terrain.PieceCatalog.Get(StableHash.Of(n))?.Category ?? 0);
		string dir = Path.Combine(Store.Directory, "export");
		Directory.CreateDirectory(dir);
		string path = Path.Combine(dir, Path.GetFileNameWithoutExtension(Homestead.FileName(name)) + "." + format);
		File.WriteAllText(path, text);
		Message?.Invoke($"Written to {path}.{(format == "blueprint" ? " For PlanBuild, copy it into BepInEx/config/PlanBuild/blueprints." : "")} Only the objects are written, not the ground.");
		return path;
	}

	// Other mods' blueprint files (PlanBuild .blueprint, .vbuild, Homestead's from elsewhere): read, then
	// kept as a Homestead blueprint.
	public string? Import(string path)
	{
		string text;
		try
		{
			text = File.ReadAllText(path);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Message?.Invoke($"Could not import that file: {ex.Message}");
			return null;
		}
		var parsed = BlueprintFormats.Parse(Path.GetFileName(path), text);
		if (parsed.Pieces.Count == 0 && parsed.Terrain.Count == 0)
		{
			Message?.Invoke("Could not import that file: no pieces found in it. Is it a .blueprint (Homestead, PlanBuild) or a .vbuild file?");
			return null;
		}
		_status = FindHomestead();
		var world = _scene()?.World;
		var clip = BlueprintFormats.ToClip(parsed, n => world?.CanCreate(StableHash.Of(n)) ?? TerrainEditor.Terrain.PrefabCatalog.Get(StableHash.Of(n)) != null, out var unknown);
		string baseName = string.IsNullOrWhiteSpace(parsed.Name) ? "Imported" : parsed.Name, name = baseName;
		for (int n = 2; HomesteadExists(name); n++)
		{
			name = $"{baseName} ({n})";
		}
		WriteHomestead(clip, new Homestead.Details(name, parsed.Description ?? "", Homestead.Details.ParseTags(TagsOf(text))), null);
		Refresh();
		int count = (clip["objects"] as JsonArray)?.Count ?? 0;
		Message?.Invoke($"Imported “{name}” as a Homestead blueprint: {count} piece(s)."
			+ (parsed.Terrain.Count > 0 ? " Its terrain marks are not kept: Homestead blueprints hold pieces only." : "")
			+ (unknown.Count > 0 ? $" {unknown.Count} kind(s) the game does not know were left out (mods?): {string.Join(", ", unknown.Take(5))}{(unknown.Count > 5 ? "…" : "")}." : "")
			+ (parsed.SkippedLines > 0 ? $" {parsed.SkippedLines} unreadable line(s) skipped." : "") + Reminder);
		return name;
	}

	// The "#Tags:" line of a blueprint file (null when it has none).
	private static string? TagsOf(string text) => text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("#Tags:", StringComparison.OrdinalIgnoreCase))?[6..];
}
