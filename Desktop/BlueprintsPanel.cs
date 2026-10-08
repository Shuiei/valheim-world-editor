using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

// Blueprints, like the web editor's (editor/blueprints.js; WorldEdit's schematics): copies saved under a
// name as files in the app's data folder (shared with the web editor), listed with a picture, pasted
// again in any area or world, written for PlanBuild (.blueprint) or as .vbuild, and those mods' files
// imported.
public sealed class BlueprintsPanel
{
	public Control Card { get; }
	public BlueprintStore Store { get; set; } = BlueprintStore.Default;
	internal TextBox Search { get; } = new() { Watermark = "Search blueprints", FontSize = 12 };
	internal Button ImportButton { get; } = new() { Content = "Import file…", FontSize = 12 };
	internal StackPanel List { get; } = new() { Spacing = 4 };
	private readonly TextBlock _folder = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
	private readonly Func<WorldScene?> _scene;
	private readonly PasteTool _paste;
	public event Action<string>? Message;
	// A blueprint went onto the clipboard: the window starts pasting.
	public event Action? Pasting;
	internal Func<string, Task<string?>> AskName { get; set; } = _ => Task.FromResult<string?>(null);
	internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);
	internal Func<Task<string?>> PickFile { get; set; } = () => Task.FromResult<string?>(null);

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
					new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4, Children = { Search, Col(ImportButton, 1) } },
					new ScrollViewer { MaxHeight = 700, Content = List },
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

	private List<BlueprintStore.Summary> _list = new();

	public void Refresh()
	{
		if (!Card.IsVisible)
		{
			return;
		}
		_list = Store.List();
		_folder.Text = $"Kept as files in {Store.Directory}. Each one can be pasted into any world.";
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

	private void Render()
	{
		List.Children.Clear();
		string q = Search.Text?.Trim() ?? "";
		var shown = _list.Where(b => q == "" || b.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
		if (shown.Count == 0)
		{
			List.Children.Add(new TextBlock
			{
				Text = _list.Count > 0 ? "Nothing matches." : "No blueprints yet. Copy something (Area or Select tool, Ctrl+C), then “Save blueprint…”.",
				FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
			});
			return;
		}
		foreach (var b in shown)
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
			Act("Paste", "Put this blueprint on the clipboard and start pasting it", () => Paste(b.Id));
			Act(".blueprint", "Write it as a PlanBuild .blueprint file", () => Export(b.Id, "blueprint"));
			Act(".vbuild", "Write it as a .vbuild file", () => Export(b.Id, "vbuild"));
			Act("Delete", "Delete this blueprint file", async () =>
			{
				if (await Confirm($"Delete the blueprint “{b.Name}”? Its file is removed."))
				{
					Store.Delete(b.Id);
					Refresh();
				}
			});
			var meta = $"{b.W} × {b.H} m · {b.Objects} object(s){(b.Ground ? " · ground" : "")}{(b.World != null ? $" · from {b.World}" : "")}{(b.Source != null ? $" · {b.Source}" : "")}";
			var grid = new Grid
			{
				ColumnDefinitions = new ColumnDefinitions("64,*"),
				RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
				ColumnSpacing = 8,
				Children =
				{
					new Image { Source = Picture(b.Thumb), Width = 64, Height = 64, [Grid.RowSpanProperty] = 3 },
					Col(new TextBlock { Text = b.Name, FontSize = 12, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis }, 1),
					Col(new TextBlock { Text = meta, FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap, [Grid.RowProperty] = 1 }, 1),
					Col(new Border { Child = acts, [Grid.RowProperty] = 2 }, 1),
				},
			};
			List.Children.Add(new Border { Padding = new Thickness(4), CornerRadius = new CornerRadius(6), Child = grid });
		}
	}

	// Saves the clipboard as a blueprint (asks for the name; one with that name is replaced after asking).
	public async Task<string?> Save()
	{
		var clip = _paste.Clip;
		if (clip == null)
		{
			Message?.Invoke("Copy something first (Area or Select tool, Ctrl+C), then save it as a blueprint.");
			return null;
		}
		string? name = (await AskName(clip.Name ?? $"Blueprint {DateTime.Now:yyyy-MM-dd HH.mm}"))?.Trim();
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		if (Store.Exists(name) && !await Confirm($"A blueprint called “{name}” already exists. Replace it?"))
		{
			return null;
		}
		clip.Name = name;
		Store.Save(name, _scene()?.World?.Name, CopyFormat.Thumb(clip), CopyFormat.ToJson(clip));
		Message?.Invoke($"Saved the blueprint “{name}” ({clip.Objects.Count} object(s)). Find it under Blueprints… in any world.");
		Refresh();
		return name;
	}

	// Loads a blueprint onto the clipboard and starts pasting. Object ids only mean something in the
	// world the copy came from; kinds this world cannot make are left out.
	public void Paste(string id)
	{
		if (Store.Read(id) is not string json || JsonNode.Parse(json) is not JsonObject doc || doc["clip"] is not JsonObject clip)
		{
			Message?.Invoke("That blueprint could not be read.");
			Refresh();
			return;
		}
		var world = _scene()?.World;
		string name = (string?)doc["name"] ?? id;
		var data = CopyFormat.FromJson(clip, name, keepSources: world != null && (string?)doc["world"] == world.Name);
		var missing = data.Objects.Where(o => world != null && !world.CanCreate(o.Prefab)).Select(o => o.Name).Distinct().ToList();
		data.Objects.RemoveAll(o => missing.Contains(o.Name));
		_paste.Clip = data;
		_paste.Notify();
		Card.IsVisible = false;
		Pasting?.Invoke();
		Message?.Invoke($"“{name}” is on the clipboard: click to place it.{(missing.Count > 0 ? $" {missing.Count} kind(s) are unknown here and are left out: {string.Join(", ", missing.Take(5))}{(missing.Count > 5 ? "…" : "")}." : "")}");
	}

	// Writes a blueprint for PlanBuild (or as .vbuild) into the export folder (objects only).
	public string? Export(string id, string format)
	{
		if (Store.Read(id) is not string json || JsonNode.Parse(json) is not JsonObject doc || doc["clip"] is not JsonObject clip)
		{
			Message?.Invoke("Could not export that blueprint.");
			return null;
		}
		string name = (string?)doc["name"] ?? id;
		string text = BlueprintFormats.Write(clip, format, name, n => TerrainEditor.Terrain.PieceCatalog.Get(StableHash.Of(n))?.Category ?? 0);
		string dir = Path.Combine(Store.Directory, "export");
		Directory.CreateDirectory(dir);
		string path = Path.Combine(dir, id + "." + format);
		File.WriteAllText(path, text);
		Message?.Invoke($"Written to {path}.{(format == "blueprint" ? " For PlanBuild, copy it into BepInEx/config/PlanBuild/blueprints." : "")} Only the objects are written, not the ground.");
		return path;
	}

	// Other mods' blueprint files: read, then kept as a blueprint like any copy.
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
			Message?.Invoke("Could not import that file: no pieces found in it. Is it a PlanBuild .blueprint or a .vbuild file?");
			return null;
		}
		var world = _scene()?.World;
		var clip = BlueprintFormats.ToClip(parsed, n => world?.CanCreate(StableHash.Of(n)) ?? TerrainEditor.Terrain.PrefabCatalog.Get(StableHash.Of(n)) != null, out var unknown);
		string baseName = string.IsNullOrWhiteSpace(parsed.Name) ? "Imported" : parsed.Name, name = baseName;
		for (int n = 2; Store.Exists(name); n++)
		{
			name = $"{baseName} ({n})";
		}
		var data = CopyFormat.FromJson(clip, name);
		Store.Save(name, null, CopyFormat.Thumb(data), CopyFormat.ToJson(data), path.EndsWith(".vbuild", StringComparison.OrdinalIgnoreCase) ? "vbuild" : "PlanBuild");
		Refresh();
		Message?.Invoke($"Imported “{name}”: {data.Objects.Count} piece(s){(parsed.Terrain.Count > 0 ? $", ground from {parsed.Terrain.Count} terrain mark(s)" : "")}."
			+ (unknown.Count > 0 ? $" {unknown.Count} kind(s) the game does not know were left out (mods?): {string.Join(", ", unknown.Take(5))}{(unknown.Count > 5 ? "…" : "")}." : "")
			+ (parsed.SkippedLines > 0 ? $" {parsed.SkippedLines} unreadable line(s) skipped." : ""));
		return name;
	}
}
