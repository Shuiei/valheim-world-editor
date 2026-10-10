using System.ComponentModel;
using System.Text.Json;
using Avalonia.Threading;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

// What Claude can do in the editor (ClaudeServer): look at what is open, and change it as the user would,
// on the window's thread. Coordinates are the game's: x east, z north, y up, in metres. Every change
// stays pending, one step of the history each: the user saves, applies live or saves the blueprint.
[McpServerToolType]
public sealed class ClaudeTools
{
	private readonly MainWindow _w;

	internal ClaudeTools(MainWindow window)
	{
		_w = window;
	}

	private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

	internal static string ToJson(object o) => JsonSerializer.Serialize(o, Json);

	// On the window's thread, as a click would be.
	private static Task<T> OnUi<T>(Func<Task<T>> f) => Dispatcher.UIThread.InvokeAsync(f);

	private static Task<T> OnUi<T>(Func<T> f) => Dispatcher.UIThread.InvokeAsync(f).GetTask();

	[McpServerTool(Name = "editor_state", ReadOnly = true, Title = "What the editor shows")]
	[Description("What the editor shows now: the start page, a world's map, an area of a world in 3D, or the Workshop (a blank plot to build a blueprint on); the world, the area's bounds in world coordinates, the changes not saved yet, and the editor's last message. Call it first.")]
	public Task<string> EditorState() => OnUi(() => ToJson(StateOf(_w)));

	// ---- Looking.

	[McpServerTool(Name = "list_worlds", ReadOnly = true, Title = "Worlds on this computer")]
	[Description("The Valheim worlds on this computer (the start page's list): name, folder, last save. Open one with open_world.")]
	public Task<string> ListWorlds() => OnUi(() => ToJson(TerrainEditor.App.Worlds.Find(_w.Settings)
		.Select(i => new { name = i.Name, folder = i.Path, saved = i.Saved, where = i.Where, usable = i.Usable, problem = i.Problem })));

	[McpServerTool(Name = "open_world", Title = "Open a world")]
	[Description("Opens a world from its folder (from list_worlds), offline, and shows its map. Refused while the open world or the Workshop has changes not saved: ask the user to save or discard them first.")]
	public Task<string> OpenWorld([Description("The world's folder, as list_worlds gives it.")] string folder) => OnUi(async () =>
	{
		if (Unsaved() is string why)
		{
			return why;
		}
		await _w.OpenWorld(() => Task.Run(() => WorldSession.Open(folder)), "Opening the world for Claude…");
		return _w.World != null ? ToJson(StateOf(_w)) : "Could not open it: " + _w.MessageText.Text;
	});

	[McpServerTool(Name = "open_area", Title = "Open an area in 3D")]
	[Description("Opens the square area around a point of the open world in the 3D editor, where it can be looked at and changed. The world is made of 64 m zones; size is how many zones across (1 to 5; 3 = 192 m is usual). Changes made in another area stay pending.")]
	public Task<string> OpenArea(
		[Description("World x (east) of the area's middle, in metres.")] float x,
		[Description("World z (north) of the area's middle, in metres.")] float z,
		[Description("Zones across: 1 to 5.")] int size = 3) => OnUi(async () =>
	{
		if (_w.World == null)
		{
			return "Open a world first (open_world).";
		}
		int zx = (int)MathF.Floor((x + 32) / 64), zz = (int)MathF.Floor((z + 32) / 64);
		await _w.EditArea(zx, zz, Math.Clamp(size, 1, 5));
		return ToJson(StateOf(_w));
	});

	[McpServerTool(Name = "describe_area", ReadOnly = true, Title = "Describe the open area")]
	[Description("The open area (or the Workshop's plot): its bounds, ground heights (lowest, highest, average; the water level), its biomes, and its objects counted by kind and by name.")]
	public Task<string> DescribeArea() => OnUi(() =>
	{
		if (_w.Session?.Scene is not { } scene)
		{
			return "No area is open (open_area, or the Workshop).";
		}
		var h = scene.Heights;
		var biomes = scene.Biomes.GroupBy(b => ((ValheimGen.Heightmap.Biome)b).ToString()).OrderByDescending(g => g.Count())
			.Select(g => new { biome = g.Key, share = MathF.Round(100f * g.Count() / scene.Biomes.Length, 1) });
		List<(string Name, string Kind)> things;
		lock (scene.Things)
		{
			things = scene.Things.Where(t => !t.Gone).Select(t => (Name: _w.NameOfPrefab(t.Prefab) ?? t.Prefab.ToString(), Kind: KindOf(t))).ToList();
		}
		return ToJson(new
		{
			area = StateOf(_w),
			ground = new { lowest = h.Min(), highest = h.Max(), average = MathF.Round(h.Average(), 2), water = scene.Water },
			biomes,
			objectsByKind = things.GroupBy(t => t.Kind).OrderByDescending(g => g.Count()).Select(g => new { kind = g.Key, count = g.Count() }),
			objectsByName = things.GroupBy(t => t.Name).OrderByDescending(g => g.Count()).Take(40).Select(g => new { name = g.Key, count = g.Count() }),
		});
	});

	private string KindOf(WorldScene.Thing t) => TerrainEditor.App.ObjectKinds.Of(_w.NameOfPrefab(t.Prefab), t.Piece, t.Tamed).ToString();

	[McpServerTool(Name = "list_objects", ReadOnly = true, Title = "List objects")]
	[Description("Objects of the open area: id, name, kind, position (x, y, z), turn (yaw in degrees) and whether a player built it. Filter by kind (Buildings, Trees, Rocks…), by a word of the name, and/or near a point; at most limit of them, nearest first when near a point.")]
	public Task<string> ListObjects(
		[Description("A kind, as describe_area counts them (optional).")] string? kind = null,
		[Description("A word the name must hold (optional).")] string? name = null,
		[Description("World x of a point to look near (optional, with z and radius).")] float? x = null,
		[Description("World z of that point.")] float? z = null,
		[Description("How far from it, in metres.")] float radius = 20,
		[Description("At most this many.")] int limit = 200) => OnUi(() =>
	{
		if (_w.Session?.Scene is not { } scene)
		{
			return "No area is open.";
		}
		List<object> found;
		lock (scene.Things)
		{
			var q = scene.Things.Select((t, i) => (t, i)).Where(p => !p.t.Gone);
			if (kind != null)
			{
				q = q.Where(p => string.Equals(KindOf(p.t), kind, StringComparison.OrdinalIgnoreCase));
			}
			if (name != null)
			{
				q = q.Where(p => (_w.NameOfPrefab(p.t.Prefab) ?? "").Contains(name, StringComparison.OrdinalIgnoreCase));
			}
			if (x is float px && z is float pz)
			{
				q = q.Where(p => (p.t.Position.X - px) * (p.t.Position.X - px) + (p.t.Position.Z - pz) * (p.t.Position.Z - pz) <= radius * radius)
					.OrderBy(p => (p.t.Position.X - px) * (p.t.Position.X - px) + (p.t.Position.Z - pz) * (p.t.Position.Z - pz));
			}
			found = q.Take(Math.Clamp(limit, 1, 2000)).Select(p => (object)new
			{
				id = p.i, name = _w.NameOfPrefab(p.t.Prefab), kind = KindOf(p.t),
				x = MathF.Round(p.t.Position.X, 2), y = MathF.Round(p.t.Position.Y, 2), z = MathF.Round(p.t.Position.Z, 2),
				yaw = MathF.Round(p.t.Rotation.Y, 1), built = p.t.Piece,
			}).ToList();
		}
		return ToJson(found);
	});

	[McpServerTool(Name = "screenshot", ReadOnly = true, Title = "See the view")]
	[Description("A picture of what the editor shows: the 3D view of the area or the Workshop (optionally from a camera: looking at x, z from yaw degrees round, pitch degrees down, distance metres away), or the world's map when the map is shown. Use it to see the result of a change.")]
	public async Task<ModelContextProtocol.Protocol.CallToolResult> Screenshot(
		[Description("World x the camera looks at (optional: the view as it is).")] float? x = null,
		[Description("World z the camera looks at.")] float? z = null,
		[Description("Round the point, in degrees (0: looking north).")] float yaw = 45,
		[Description("Down from level, in degrees (90: from straight above).")] float pitch = 55,
		[Description("Metres from the point.")] float distance = 120,
		[Description("Height of the point looked at (optional: the ground's).")] float? height = null)
	{
		string path = Path.Combine(Path.GetTempPath(), $"vwe-claude-{Guid.NewGuid():N}.png");
		try
		{
			string? problem = await OnUi(async () =>
			{
				if (_w.MapShown)
				{
					await _w.MapPage!.Map.Picture(path).WaitAsync(TimeSpan.FromSeconds(60));
					return null;
				}
				if (_w.Session == null)
				{
					return "Nothing to see: open a world's area (open_area) or the Workshop.";
				}
				if (x is float cx && z is float cz)
				{
					_w.View.Orbit(cx, cz, yaw, pitch, distance, height);
				}
				await _w.View.Picture(path).WaitAsync(TimeSpan.FromSeconds(60));
				return null;
			});
			if (problem != null)
			{
				return new() { IsError = true, Content = { new ModelContextProtocol.Protocol.TextContentBlock { Text = problem } } };
			}
			return new() { Content = { ModelContextProtocol.Protocol.ImageContentBlock.FromBytes(Shrink(await File.ReadAllBytesAsync(path)), "image/jpeg") } };
		}
		finally
		{
			File.Delete(path);
		}
	}

	// A picture no wider than 1280 pixels, as a JPEG (smaller to send and to look at).
	internal static byte[] Shrink(byte[] png)
	{
		using var bitmap = SkiaSharp.SKBitmap.Decode(png);
		float k = Math.Min(1f, 1280f / bitmap.Width);
		using var scaled = k < 1 ? bitmap.Resize(new SkiaSharp.SKImageInfo((int)(bitmap.Width * k), (int)(bitmap.Height * k)), new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Linear)) : bitmap.Copy();
		using var image = SkiaSharp.SKImage.FromBitmap(scaled);
		return image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 82).ToArray();
	}

	[McpServerTool(Name = "find_prefabs", ReadOnly = true, Title = "Find what can be placed")]
	[Description("Searches the game's objects by name (prefab names such as Beech1, rock4_forest, woodwall, stone_floor_2x2): what can be placed in a world, and the building pieces with their English name, tool (hammer for building pieces), size and cost. Every word must match.")]
	public static Task<string> FindPrefabs(
		[Description("Words of the name, e.g. \"stone wall\", \"pine\", \"roof 26\".")] string query,
		[Description("Only building pieces (the hammer's): true; anything: false.")] bool piecesOnly = false,
		[Description("At most this many.")] int limit = 60) => OnUi(() =>
	{
		var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		bool Match(string s) => words.All(w => s.Contains(w, StringComparison.OrdinalIgnoreCase));
		var pieces = TerrainEditor.Terrain.PieceCatalog.Names
			.Select(n => (n, p: TerrainEditor.Terrain.PieceCatalog.Get(TerrainEditor.Save.StableHash.Of(n))))
			.Where(x => x.p is { Tool: not null } && (Match(x.n) || Match(TerrainEditor.Terrain.PieceCost.PieceName(x.n))))
			.Select(x => (object)new { prefab = x.n, name = TerrainEditor.Terrain.PieceCost.PieceName(x.n), tool = x.p!.Tool, size = SizeOf(x.p!), cost = TerrainEditor.Terrain.PieceCost.Of(new[] { x.n }).Describe(4) });
		var others = piecesOnly ? Enumerable.Empty<object>() : TerrainEditor.Terrain.PrefabCatalog.Placeable
			.Where(i => TerrainEditor.Terrain.PieceCatalog.Get(TerrainEditor.Save.StableHash.Of(i.Name)) == null && Match(i.Name))
			.Select(i => (object)new { prefab = i.Name, kind = TerrainEditor.App.ObjectKinds.Of(i.Name, false).ToString() });
		return ToJson(pieces.Concat(others).Take(Math.Clamp(limit, 1, 500)));
	});

	private static object SizeOf(TerrainEditor.Terrain.PieceCatalog.Info p) =>
		new { x = MathF.Round(p.MaxX - p.MinX, 2), y = MathF.Round(p.MaxY - p.MinY, 2), z = MathF.Round(p.MaxZ - p.MinZ, 2) };

	[McpServerTool(Name = "piece_info", ReadOnly = true, Title = "About building pieces")]
	[Description("For building pieces (prefab names): English name, tool, size and bounds from the piece's origin (x across, y up, z through, before turning), snap points (where pieces join, from the origin), cost and the station it needs. Use it to place pieces edge to edge.")]
	public static Task<string> PieceInfo([Description("Prefab names, e.g. [\"woodwall\", \"wood_floor\"].")] string[] prefabs) => OnUi(() => ToJson(prefabs.Select(n =>
	{
		var p = TerrainEditor.Terrain.PieceCatalog.Get(TerrainEditor.Save.StableHash.Of(n));
		if (p == null)
		{
			return (object)new { prefab = n, error = "not a building piece the editor knows (find_prefabs)" };
		}
		var cost = TerrainEditor.Terrain.PieceCost.Get(n);
		return new
		{
			prefab = n, name = TerrainEditor.Terrain.PieceCost.PieceName(n), tool = p.Tool, size = SizeOf(p),
			bounds = new { minX = p.MinX, maxX = p.MaxX, minY = p.MinY, maxY = p.MaxY, minZ = p.MinZ, maxZ = p.MaxZ },
			snaps = p.Snaps,
			cost = TerrainEditor.Terrain.PieceCost.Of(new[] { n }).Describe(8),
			station = cost?.Station,
		};
	})));

	[McpServerTool(Name = "script_reference", ReadOnly = true, Title = "The script API")]
	[Description("The C# API run_script scripts use, as its source with comments: Area (bounds, points), Ground (Height, Set, Raise, Lower, Paint, Shape, Mountain, Biome), Objects (All, OfKind, Near, Place, Remove, CanPlace), Noise, Rnd, Print. Read it before writing a script.")]
	public static string ScriptReference() => ScriptHost.ApiSource;

	// ---- Changing (pending changes only, one step of the history each).

	// Why Claude cannot change the open area now, or null: nothing open, or live with Auto on (each change
	// would go to the game at once, and Claude's must wait for the user).
	private string? CannotEdit()
	{
		if (_w.Session == null)
		{
			return "No area is open: open_area (a world) or open_workshop first.";
		}
		if (_w.World is { IsLive: true } && _w.Settings.AutoApply)
		{
			return "The open world is live with Auto on: every change would go to the game at once. Ask the user to turn Auto off; Claude's changes then wait for Apply live.";
		}
		return null;
	}

	[McpServerTool(Name = "run_script", Title = "Run a script on the area")]
	[Description("Runs a C# script on the open area (or the Workshop's plot), as the editor's Script tool does: the script's statements are the body of a program using the API script_reference gives (Area, Ground, Objects, Noise, Rnd, Print). What it changes (ground heights, paint, objects placed or taken away) goes in as one step of the history, pending until the user saves; nothing changes when it has mistakes or fails (the errors say which line). Returns what it printed and what it changed. Ground stays within the game's ±8 m of the original unless Ground.NoLimit = true.")]
	public Task<string> RunScript(
		[Description("The script: C# statements, e.g. \"Ground.Mountain(Area.CenterX, Area.CenterZ, height: 30, radius: 50);\"")] string code,
		[Description("A few words for the history (\"Claude: river valley\").")] string label = "Claude: script") => OnUi(async () =>
	{
		if (CannotEdit() is string why)
		{
			return why;
		}
		var r = await _w.RunScriptCode(code, label.StartsWith("Claude", StringComparison.Ordinal) ? label : "Claude: " + label);
		_w.MessageText.Text = r.Message;
		return r.Output;
	});

	[McpServerTool(Name = "undo", Title = "Undo")]
	[Description("Takes back the last change of the area (anyone's: Claude's or the user's), as Ctrl+Z.")]
	public Task<string> Undo() => OnUi(async () =>
	{
		if (CannotEdit() is string why)
		{
			return why;
		}
		if (_w.Session is not { CanUndo: true })
		{
			return "Nothing to undo.";
		}
		await _w.Undo();
		return "Undone. Now pending: " + Pending();
	});

	[McpServerTool(Name = "redo", Title = "Redo")]
	[Description("Puts back the last change undone, as Ctrl+Y.")]
	public Task<string> Redo() => OnUi(async () =>
	{
		if (CannotEdit() is string why)
		{
			return why;
		}
		if (_w.Session is not { CanRedo: true })
		{
			return "Nothing to redo.";
		}
		await _w.Redo();
		return "Redone. Now pending: " + Pending();
	});

	// The changes not saved yet, in words.
	private string Pending() => _w.World is { } w
		? w.Pending is (0, 0, 0, 0) ? "nothing." : $"{w.Pending.Zones} zone(s) of ground, {w.Pending.Added} object(s) added, {w.Pending.Deleted} taken away."
		: _w.Session is { } s ? $"{Workshop.Pieces(s.Scene)} building piece(s) on the plot, not saved as a blueprint." : "nothing.";

	// Why a world cannot be left now (changes the user has not saved), or null.
	private string? Unsaved()
	{
		if (_w.World is { } w && w.Pending is not (0, 0, 0, 0))
		{
			return $"The open world has changes not {(w.IsLive ? "applied" : "saved")} ({w.Pending.Zones} zone(s), {w.Pending.Added} added, {w.Pending.Deleted} deleted): ask the user to {(w.IsLive ? "apply" : "save")} or discard them first.";
		}
		if (_w.WorkshopDirty)
		{
			return "The Workshop's building is not saved as a blueprint: ask the user to save it (or to leave the Workshop) first.";
		}
		return null;
	}

	internal static object StateOf(MainWindow w)
	{
		var s = w.Session;
		var scene = s?.Scene;
		string page = w.MapShown ? "map" : w.InWorkshop ? "workshop" : s != null ? "area" : "start";
		return new
		{
			page,
			world = w.World is { } world ? new { name = world.World.Name, folder = world.World.Directory, live = world.IsLive, autoApply = world.IsLive && w.Settings.AutoApply } : null,
			area = scene == null ? null : new
			{
				name = scene.Name,
				minX = scene.X0 * 64f - 32f,
				minZ = scene.Z0 * 64f - 32f,
				maxX = scene.X0 * 64f - 32f + scene.Size * 64f,
				maxZ = scene.Z0 * 64f - 32f + scene.Size * 64f,
				water = scene.Water,
				objects = scene.Things.Count(t => !t.Gone),
			},
			pending = w.World is { } pw ? new { zones = pw.Pending.Zones, added = pw.Pending.Added, deleted = pw.Pending.Deleted } : null,
			canUndo = s?.CanUndo ?? false,
			message = w.MessageText.Text ?? "",
		};
	}
}
