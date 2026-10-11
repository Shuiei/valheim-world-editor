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
			found = q.Take(Math.Clamp(limit, 1, 2000)).Select(p => Describe(p.t, p.i)).ToList();
		}
		return ToJson(found);
	});

	[McpServerTool(Name = "get_selection", ReadOnly = true, Title = "What the user selected")]
	[Description("The objects the user has selected in the editor (Select tool): id, name, kind, position, turn. Use it when the user says \"these\", \"the selected trees\", \"this building\".")]
	public Task<string> GetSelection() => OnUi(() =>
	{
		if (_w.Session?.Scene is not { } scene)
		{
			return "No area is open.";
		}
		lock (scene.Things)
		{
			return ToJson(_w.View.Selected.Where(i => i >= 0 && i < scene.Things.Count && !scene.Things[i].Gone).Select(i => Describe(scene.Things[i], i)).ToList());
		}
	});

	[McpServerTool(Name = "select_objects", Title = "Select objects for the user")]
	[Description("Selects objects in the editor (by id), so the user sees which ones are meant, and points the camera at them. Changes nothing in the world.")]
	public Task<string> SelectObjects(
		[Description("Their ids (list_objects).")] int[] ids,
		[Description("Point the camera at them (default: true).")] bool focus = true) => OnUi(() =>
	{
		if (_w.Session?.Scene is not { } scene)
		{
			return "No area is open.";
		}
		var ok = ids.Distinct().Where(i => i >= 0 && i < scene.Things.Count && !scene.Things[i].Gone).ToList();
		_w.View.Select(ok);
		if (focus && ok.Count > 0)
		{
			var mid = ok.Select(i => scene.Things[i].Position).Aggregate(System.Numerics.Vector3.Zero, (a, b) => a + b) / ok.Count;
			float spread = ok.Select(i => System.Numerics.Vector3.Distance(scene.Things[i].Position, mid)).DefaultIfEmpty(0).Max();
			_w.View.Focus(mid, MathF.Max(20, spread * 2.5f));
		}
		return $"Selected {ok.Count}{(ok.Count < ids.Length ? $" ({ids.Length - ok.Count} id(s) were not objects of the area)" : "")}.";
	});

	private object Describe(WorldScene.Thing t, int i) => new
	{
		id = i, name = _w.NameOfPrefab(t.Prefab), kind = KindOf(t),
		x = MathF.Round(t.Position.X, 2), y = MathF.Round(t.Position.Y, 2), z = MathF.Round(t.Position.Z, 2),
		yaw = MathF.Round(t.Rotation.Y, 1), built = t.Piece,
	};

	[McpServerTool(Name = "screenshot", ReadOnly = true, Title = "See the view")]
	[Description("A picture of what the editor shows: the 3D view of the area or the Workshop (optionally from a camera: looking at x, z from yaw degrees round, pitch degrees down, distance metres away), or the world's map when the map is shown. Use it to see the result of a change.")]
	public async Task<ModelContextProtocol.Protocol.CallToolResult> Screenshot(
		[Description("World x the camera looks at (optional: the view as it is).")] float? x = null,
		[Description("World z the camera looks at.")] float? z = null,
		[Description("Round the point, in degrees (0: looking north).")] float yaw = 45,
		[Description("Down from level, in degrees (90: from straight above).")] float pitch = 55,
		[Description("Metres from the point.")] float distance = 120,
		[Description("Height of the point looked at (optional: the ground's).")] float? height = null,
		[Description("4: four pictures in one (from above, and from the south-west, east and north-west, all at the point, or the area's middle), to see a building or a place from every side. 1: one picture (default).")] int views = 1)
	{
		if (views >= 4)
		{
			return await FourViews(x, z, distance, height);
		}
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

	// Four views of a point (the area's middle when none is given), as one picture with their names.
	private async Task<ModelContextProtocol.Protocol.CallToolResult> FourViews(float? x, float? z, float distance, float? height)
	{
		var cams = new (string Name, float Yaw, float Pitch)[] { ("from above (north up)", 0, 89), ("from the south-west", 225, 35), ("from the east", 90, 35), ("from the north-west", 315, 35) };
		var pictures = new List<SkiaSharp.SKBitmap>();
		try
		{
			foreach (var (name, yaw, pitch) in cams)
			{
				string path = Path.Combine(Path.GetTempPath(), $"vwe-claude-{Guid.NewGuid():N}.png");
				try
				{
					string? problem = await OnUi(async () =>
					{
						if (_w.Session?.Scene is not { } scene || _w.MapShown)
						{
							return "Four views need an area or the Workshop open in 3D.";
						}
						float cx = x ?? scene.Cx, cz = z ?? scene.Cz;
						_w.View.Orbit(cx, cz, yaw, pitch, distance, height);
						await _w.View.Picture(path).WaitAsync(TimeSpan.FromSeconds(60));
						return null;
					});
					if (problem != null)
					{
						return new() { IsError = true, Content = { new ModelContextProtocol.Protocol.TextContentBlock { Text = problem } } };
					}
					var bmp = SkiaSharp.SKBitmap.Decode(await File.ReadAllBytesAsync(path));
					pictures.Add(bmp);
				}
				finally
				{
					File.Delete(path);
				}
			}
			int w = 640, h = (int)(640f * pictures[0].Height / pictures[0].Width);
			using var sheet = new SkiaSharp.SKBitmap(w * 2, h * 2);
			using (var canvas = new SkiaSharp.SKCanvas(sheet))
			{
				using var font = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.Default, 20);
				using var back = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(0, 0, 0, 160) };
				using var ink = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.White, IsAntialias = true };
				for (int i = 0; i < 4; i++)
				{
					var r = SkiaSharp.SKRect.Create(i % 2 * w, i / 2 * h, w, h);
					canvas.DrawBitmap(pictures[i], r);
					canvas.DrawRect(r.Left, r.Top, w, 30, back);
					canvas.DrawText(cams[i].Name, r.Left + 8, r.Top + 22, SkiaSharp.SKTextAlign.Left, font, ink);
				}
			}
			using var image = SkiaSharp.SKImage.FromBitmap(sheet);
			return new() { Content = { ModelContextProtocol.Protocol.ImageContentBlock.FromBytes(image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 82).ToArray(), "image/jpeg") } };
		}
		finally
		{
			foreach (var p in pictures)
			{
				p.Dispose();
			}
		}
	}

	[McpServerTool(Name = "area_map", ReadOnly = true, Title = "A map of the area with coordinates")]
	[Description("A top-down map of the open area (or the plot), drawn from its data, north up: ground heights in colour with hill shading, painted ground (paved grey, dirt brown, cultivated dark), water in blue, objects as dots (trees green, rocks grey, buildings orange, others white), and a grid labelled in world metres. Use it to read exact places, plan, and check ground work.")]
	public async Task<ModelContextProtocol.Protocol.CallToolResult> AreaMap(
		[Description("Grid spacing in metres (default 16).")] float grid = 16,
		[Description("Draw the objects (default true).")] bool objects = true)
	{
		var data = await OnUi(() =>
		{
			if (_w.Session?.Scene is not { } scene)
			{
				return null;
			}
			List<(float X, float Z, string Kind)> things;
			lock (scene.Things)
			{
				things = scene.Things.Where(t => !t.Gone).Select(t => (t.Position.X, t.Position.Z, KindOf(t))).ToList();
			}
			// Paint (dirt, cultivated, paved) per ground point, as the ground has it now.
			var g = _w.Session.Ground;
			var paint = g.W == scene.W && g.H == scene.H ? Enumerable.Range(0, scene.W * scene.H * 3).Select(k => g.MaskOf(k / 3, k % 3)).ToArray() : null;
			return new { scene.W, scene.H, scene.Cx, scene.Cz, scene.Water, Heights = (float[])scene.Heights.Clone(), Paint = paint, Things = things };
		});
		if (data == null)
		{
			return new() { IsError = true, Content = { new ModelContextProtocol.Protocol.TextContentBlock { Text = "No area is open." } } };
		}
		byte[] jpeg = await Task.Run(() => DrawMap(data.W, data.H, data.Cx, data.Cz, data.Water, data.Heights, data.Paint, objects ? data.Things : new(), MathF.Max(4, grid)));
		float minX = data.Cx - (data.W - 1) / 2f, minZ = data.Cz - (data.H - 1) / 2f;
		return new()
		{
			Content =
			{
				new ModelContextProtocol.Protocol.TextContentBlock { Text = $"x {minX:0} to {minX + data.W - 1:0} (left to right), z {minZ:0} to {minZ + data.H - 1:0} (bottom to top); heights {data.Heights.Min():0.0} to {data.Heights.Max():0.0} m, water at {data.Water:0.0} m." },
				ModelContextProtocol.Protocol.ImageContentBlock.FromBytes(jpeg, "image/jpeg"),
			},
		};
	}

	internal static byte[] DrawMap(int w, int h, float cx, float cz, float water, float[] heights, float[]? paint, List<(float X, float Z, string Kind)> things, float grid)
	{
		const int Margin = 40;
		float k = MathF.Min(12, 760f / MathF.Max(w, h));
		int pw = (int)(w * k), ph = (int)(h * k);
		float lo = heights.Min(), hi = MathF.Max(heights.Max(), lo + 1);
		using var ground = new SkiaSharp.SKBitmap(w, h);
		for (int j = 0; j < h; j++)
		{
			for (int i = 0; i < w; i++)
			{
				float y = heights[j * w + i];
				// Hill shading: lit from the north-west.
				float dx = heights[j * w + Math.Min(w - 1, i + 1)] - heights[j * w + Math.Max(0, i - 1)];
				float dz = heights[Math.Min(h - 1, j + 1) * w + i] - heights[Math.Max(0, j - 1) * w + i];
				float light = Math.Clamp(0.8f + (dz - dx) * 0.12f, 0.45f, 1.2f);
				SkiaSharp.SKColor c;
				if (y < water)
				{
					float d = Math.Clamp((water - y) / 20, 0, 1);
					c = new SkiaSharp.SKColor((byte)(40 - 20 * d), (byte)(110 - 50 * d), (byte)(170 - 40 * d));
				}
				else
				{
					float t = Math.Clamp((y - lo) / (hi - lo), 0, 1);
					var (r, g, b) = t < 0.5f ? (90 + 120 * t, 140 + 40 * t, 70 + 20 * t) : (150 + 200 * (t - 0.5f), 160 + 160 * (t - 0.5f), 80 + 300 * (t - 0.5f));
					if (paint != null)
					{
						// Painted ground over it: dirt brown, cultivated dark, paved grey.
						int q = (j * w + i) * 3;
						float dirt = Math.Clamp(paint[q], 0, 1), tilled = Math.Clamp(paint[q + 1], 0, 1), paved = Math.Clamp(paint[q + 2], 0, 1);
						(r, g, b) = (r + (125 - r) * dirt, g + (95 - g) * dirt, b + (60 - b) * dirt);
						(r, g, b) = (r + (85 - r) * tilled, g + (60 - g) * tilled, b + (40 - b) * tilled);
						(r, g, b) = (r + (160 - r) * paved, g + (158 - g) * paved, b + (150 - b) * paved);
					}
					c = new SkiaSharp.SKColor((byte)Math.Clamp(r * light, 0, 255), (byte)Math.Clamp(g * light, 0, 255), (byte)Math.Clamp(b * light, 0, 255));
				}
				// North up: the last row of the grid (largest z) at the top.
				ground.SetPixel(i, h - 1 - j, c);
			}
		}
		using var sheet = new SkiaSharp.SKBitmap(pw + 2 * Margin, ph + 2 * Margin);
		using (var canvas = new SkiaSharp.SKCanvas(sheet))
		{
			canvas.Clear(new SkiaSharp.SKColor(24, 28, 34));
			using (var groundImage = SkiaSharp.SKImage.FromBitmap(ground))
			{
				canvas.DrawImage(groundImage, SkiaSharp.SKRect.Create(Margin, Margin, pw, ph), new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Linear));
			}
			float minX = cx - (w - 1) / 2f, minZ = cz - (h - 1) / 2f;
			float Px(float x) => Margin + (x - minX) * k;
			float Py(float z) => Margin + ph - (z - minZ) * k;
			using var dot = new SkiaSharp.SKPaint { IsAntialias = true };
			foreach (var (x, z, kind) in things)
			{
				dot.Color = kind switch { "Trees" => new SkiaSharp.SKColor(30, 90, 30), "Rocks" or "Ore" => new SkiaSharp.SKColor(130, 130, 130), "Buildings" => new SkiaSharp.SKColor(240, 150, 40), _ => SkiaSharp.SKColors.White };
				canvas.DrawCircle(Px(x), Py(z), kind == "Buildings" ? 2.2f : 1.8f, dot);
			}
			using var line = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(255, 255, 255, 70), StrokeWidth = 1 };
			using var ink = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(220, 220, 220), IsAntialias = true };
			using var font = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.Default, 11);
			for (float gx = MathF.Ceiling(minX / grid) * grid; gx <= minX + w - 1; gx += grid)
			{
				canvas.DrawLine(Px(gx), Margin, Px(gx), Margin + ph, line);
				canvas.DrawText($"{gx:0}", Px(gx), Margin + ph + 14, SkiaSharp.SKTextAlign.Center, font, ink);
			}
			for (float gz = MathF.Ceiling(minZ / grid) * grid; gz <= minZ + h - 1; gz += grid)
			{
				canvas.DrawLine(Margin, Py(gz), Margin + pw, Py(gz), line);
				canvas.DrawText($"{gz:0}", Margin - 4, Py(gz) + 4, SkiaSharp.SKTextAlign.Right, font, ink);
			}
			canvas.DrawText("north up; x left to right, z bottom to top; paint: paved grey, dirt brown, cultivated dark", Margin, Margin - 12, SkiaSharp.SKTextAlign.Left, font, ink);
		}
		using var image = SkiaSharp.SKImage.FromBitmap(sheet);
		return image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 85).ToArray();
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
		[Description("At most this many.")] int limit = 60,
		[Description("Items instead (what goes in chests: Coins, Amber, IronScrap, weapons…): true.")] bool items = false) => OnUi(() =>
	{
		var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		bool Match(string s) => words.All(w => s.Contains(w, StringComparison.OrdinalIgnoreCase));
		if (items)
		{
			return ToJson(TerrainEditor.Terrain.PrefabCatalog.Items.Where(Match).Take(Math.Clamp(limit, 1, 500)).Select(i => new { item = i }));
		}
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

	[McpServerTool(Name = "building_guide", ReadOnly = true, Title = "How to build and shape")]
	[Description("How Valheim's building works, from the game's data: the 2 m grid, pieces' origins and turns, roofs, and support (what holds and what falls); and how to shape ground. Read it before building.")]
	public static string BuildingGuide() => ClaudeGuide.Building + "\n\n" + ClaudeGuide.Shaping;

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
		[Description("A few words for the history (\"Claude: river valley\").")] string label = "Claude: script",
		[Description("true: only say what it would change (ground points, paint, objects placed and taken away), changing nothing. Use it before a large change.")] bool dryRun = false) => OnUi(async () =>
	{
		if (CannotEdit() is string why)
		{
			return why;
		}
		var r = await _w.RunScriptCode(code, label.StartsWith("Claude", StringComparison.Ordinal) ? label : "Claude: " + label, apply: !dryRun);
		_w.MessageText.Text = r.Message;
		return r.Output;
	});

	// ---- Common tasks, as scripts the Script tool runs (one pending step each).

	private static string N(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

	private static readonly HashSet<string> PaintKinds = new(StringComparer.OrdinalIgnoreCase) { "dirt", "cultivated", "paved", "clear" };

	private Task<string> RunGenerated(string code, string label) => OnUi(async () =>
	{
		if (CannotEdit() is string why)
		{
			return why;
		}
		var r = await _w.RunScriptCode(code, "Claude: " + label);
		_w.MessageText.Text = r.Message;
		return r.Output;
	});

	[McpServerTool(Name = "flatten", Title = "Flatten a rectangle")]
	[Description("Levels the ground of a rectangle of the open area to one height (default: its average), blending into the ground around over a soft edge. One pending step. The game's ±8 m limit from the original ground holds.")]
	public Task<string> Flatten(
		[Description("West edge (smallest x).")] float x0, [Description("South edge (smallest z).")] float z0,
		[Description("East edge (largest x).")] float x1, [Description("North edge (largest z).")] float z1,
		[Description("The height to level to (optional: the rectangle's average).")] float? height = null,
		[Description("Width of the soft edge outside the rectangle, metres (0: a hard edge).")] float edge = 4) =>
		RunGenerated($$"""
			float x0 = {{N(MathF.Min(x0, x1))}}f, x1 = {{N(MathF.Max(x0, x1))}}f, z0 = {{N(MathF.Min(z0, z1))}}f, z1 = {{N(MathF.Max(z0, z1))}}f, edge = {{N(MathF.Max(0, edge))}}f;
			float target = {{(height is float h ? N(h) + "f" : "float.NaN")}};
			if (float.IsNaN(target))
			{
				float sum = 0; int n = 0;
				foreach (var (x, z) in Area.Points()) if (x >= x0 && x <= x1 && z >= z0 && z <= z1) { sum += Ground.Height(x, z); n++; }
				target = n > 0 ? sum / n : 0;
			}
			foreach (var (x, z) in Area.Points())
			{
				float dx = MathF.Max(0, MathF.Max(x0 - x, x - x1)), dz = MathF.Max(0, MathF.Max(z0 - z, z - z1));
				float d = MathF.Sqrt(dx * dx + dz * dz);
				if (d > edge) continue;
				float w = edge <= 0 || d <= 0 ? 1 : 0.5f + 0.5f * MathF.Cos(MathF.PI * d / edge);
				float h = Ground.Height(x, z);
				Ground.Set(x, z, h + (target - h) * w);
			}
			Print($"levelled to {target:0.0} m");
			""", "flatten");

	[McpServerTool(Name = "paint_area", Title = "Paint the ground")]
	[Description("Paints the ground of a rectangle (x0, z0, x1, z1) or a circle (x, z, radius) of the open area: dirt, cultivated, paved, or clear (back to the biome's own). One pending step.")]
	public Task<string> PaintArea(
		[Description("dirt, cultivated, paved or clear.")] string kind,
		[Description("Rectangle: west edge.")] float? x0 = null, [Description("Rectangle: south edge.")] float? z0 = null,
		[Description("Rectangle: east edge.")] float? x1 = null, [Description("Rectangle: north edge.")] float? z1 = null,
		[Description("Circle: middle x.")] float? x = null, [Description("Circle: middle z.")] float? z = null,
		[Description("Circle: radius, metres.")] float? radius = null)
	{
		if (!PaintKinds.Contains(kind))
		{
			return Task.FromResult("The paint is dirt, cultivated, paved or clear.");
		}
		string inside = x0 is float a && z0 is float b && x1 is float c && z1 is float d
			? $"x >= {N(MathF.Min(a, c))}f && x <= {N(MathF.Max(a, c))}f && z >= {N(MathF.Min(b, d))}f && z <= {N(MathF.Max(b, d))}f"
			: x is float cx && z is float cz && radius is float r
				? $"(x - {N(cx)}f) * (x - {N(cx)}f) + (z - {N(cz)}f) * (z - {N(cz)}f) <= {N(r * r)}f"
				: null!;
		if (inside == null)
		{
			return Task.FromResult("Give a rectangle (x0, z0, x1, z1) or a circle (x, z, radius).");
		}
		return RunGenerated($$"""
			int n = 0;
			foreach (var (x, z) in Area.Points()) if ({{inside}}) { Ground.Paint(x, z, "{{kind.ToLowerInvariant()}}"); n++; }
			Print($"painted {n} points");
			""", $"paint {kind.ToLowerInvariant()}");
	}

	[McpServerTool(Name = "road", Title = "Lay a road")]
	[Description("Lays a road along points of the open area: the ground along it levelled to a smooth slope from point to point (its heights there, or given), the edges blended, and painted (paved by default). One pending step.")]
	public Task<string> Road(
		[Description("The road's points, in order: [[x, z], [x, z], …] (at least 2).")] float[][] points,
		[Description("Width, metres.")] float width = 4,
		[Description("Paint: paved, dirt, or none.")] string paint = "paved",
		[Description("Heights of the points, in order (optional: the ground's there).")] float[]? heights = null)
	{
		if (points.Length < 2 || points.Any(p => p.Length < 2))
		{
			return Task.FromResult("A road needs at least 2 points, each [x, z].");
		}
		if (paint != "none" && !PaintKinds.Contains(paint))
		{
			return Task.FromResult("The paint is paved, dirt or none.");
		}
		string px = string.Join(", ", points.Select(p => N(p[0]) + "f")), pz = string.Join(", ", points.Select(p => N(p[1]) + "f"));
		string ph = heights is { } hs && hs.Length == points.Length ? string.Join(", ", hs.Select(h => N(h) + "f")) : "";
		return RunGenerated($$"""
			float[] px = { {{px}} }, pz = { {{pz}} };
			float[] ph = { {{ph}} };
			if (ph.Length != px.Length) { ph = new float[px.Length]; for (int i = 0; i < px.Length; i++) ph[i] = Ground.Height(px[i], pz[i]); }
			float half = {{N(MathF.Max(1, width) / 2)}}f, edge = 3;
			foreach (var (x, z) in Area.Points())
			{
				float best = float.MaxValue, at = 0;
				for (int i = 0; i + 1 < px.Length; i++)
				{
					float ax = px[i], az = pz[i], bx = px[i + 1] - ax, bz = pz[i + 1] - az;
					float len2 = bx * bx + bz * bz, t = len2 > 0 ? Math.Clamp(((x - ax) * bx + (z - az) * bz) / len2, 0, 1) : 0;
					float dx = x - (ax + bx * t), dz = z - (az + bz * t), d = MathF.Sqrt(dx * dx + dz * dz);
					if (d < best) { best = d; at = ph[i] + (ph[i + 1] - ph[i]) * t; }
				}
				if (best > half + edge) continue;
				float w = best <= half ? 1 : 0.5f + 0.5f * MathF.Cos(MathF.PI * (best - half) / edge);
				float h = Ground.Height(x, z);
				Ground.Set(x, z, h + (at - h) * w);
				if ({{(paint == "none" ? "false" : "true")}} && best <= half) Ground.Paint(x, z, "{{paint.ToLowerInvariant()}}");
			}
			Print($"road of {px.Length - 1} segment(s)");
			""", "road");
	}

	[McpServerTool(Name = "forest", Title = "Plant trees or rocks")]
	[Description("Plants objects (trees, bushes, rocks: prefab names from find_prefabs) over a rectangle of the open area, about spacing metres apart with some randomness, turned at random, kept off water, off buildings and away from what is already there. One pending step.")]
	public Task<string> Forest(
		[Description("West edge.")] float x0, [Description("South edge.")] float z0,
		[Description("East edge.")] float x1, [Description("North edge.")] float z1,
		[Description("Prefab names to mix, e.g. [\"Beech1\", \"Birch1\", \"Oak1\"].")] string[] kinds,
		[Description("About how far apart, metres.")] float spacing = 6,
		[Description("Any number: another layout with the same choices.")] int seed = 1)
	{
		if (kinds.Length == 0)
		{
			return Task.FromResult("Give at least one prefab name (find_prefabs).");
		}
		string list = string.Join(", ", kinds.Select(k => "\"" + k.Replace("\"", "", StringComparison.Ordinal) + "\""));
		return RunGenerated($$"""
			string[] kinds = { {{list}} };
			foreach (var k in kinds) if (!Objects.CanPlace(k)) { Print($"{k}: not an object this world can have"); return; }
			Rnd.Seed = {{seed}};
			float x0 = {{N(MathF.Min(x0, x1))}}f, x1 = {{N(MathF.Max(x0, x1))}}f, z0 = {{N(MathF.Min(z0, z1))}}f, z1 = {{N(MathF.Max(z0, z1))}}f, step = {{N(MathF.Max(1, spacing))}}f;
			int n = 0;
			for (float z = z0 + step / 2; z < z1; z += step)
				for (float x = x0 + step / 2; x < x1; x += step)
				{
					float px = x + Rnd.Range(-0.4f, 0.4f) * step, pz = z + Rnd.Range(-0.4f, 0.4f) * step;
					if (!Area.Inside(px, pz) || Ground.Height(px, pz) < Area.Water + 0.5f) continue;
					if (Objects.Near(px, pz, step * 0.5f).Any(o => o.Building || o.Kind is "Trees" or "Rocks")) continue;
					Objects.Place(Rnd.Pick(kinds), px, pz, yaw: Rnd.Range(0, 360));
					n++;
				}
			Print($"placed {n}");
			""", "forest");
	}

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

	// ---- Building.

	// A piece (or any object) to put down.
	public sealed class PieceSpec
	{
		[Description("Prefab name, e.g. woodwall, wood_floor, stone_wall_2x1, wood_roof_45, Beech1.")]
		public string Prefab { get; set; } = "";

		[Description("World x (east), metres.")]
		public float X { get; set; }

		[Description("World z (north), metres.")]
		public float Z { get; set; }

		[Description("Height of the piece's origin (optional: it stands on the ground there, its lowest point touching it). The Workshop's plot is at y = 34.")]
		public float? Y { get; set; }

		[Description("Turn round the vertical, degrees (0: the piece's front faces north; 90: east).")]
		public float Yaw { get; set; }

		[Description("Tilt forward, degrees (usually 0).")]
		public float Pitch { get; set; }

		[Description("Tilt sideways, degrees (usually 0).")]
		public float Roll { get; set; }

		[Description("Snap to the snap points of pieces already there (and of those before it in the list) within 0.5 m, as the game's hammer does (default: true).")]
		public bool Snap { get; set; } = true;
	}

	[McpServerTool(Name = "place_pieces", Title = "Put down pieces and objects")]
	[Description("Puts down building pieces (and any other objects: trees, rocks…) in the open area or on the Workshop's plot, all as one step of the history (pending). Pieces snap to the snap points of pieces already there within 0.5 m, as the game's hammer does, so place them where they should join and let the snap make them meet; piece_info gives sizes and snap points. Returns where each went (after snapping) and its id. Check the result with support_check (Workshop) and screenshot.")]
	public Task<string> PlacePieces(
		[Description("The pieces, in the order they are put down.")] PieceSpec[] pieces,
		[Description("A few words for the history.")] string label = "Claude: building") => OnUi(() => Place(pieces, label));

	// The pieces put down as one step (on the window's thread): where each went and its id, or why not.
	private string Place(IReadOnlyList<PieceSpec> pieces, string label)
	{
		if (CannotEdit() is string why)
		{
			return why;
		}
		var session = _w.Session!;
		var scene = session.Scene;
		float minX = scene.X0 * 64f - 32f, minZ = scene.Z0 * 64f - 32f, maxX = minX + scene.Size * 64f, maxZ = minZ + scene.Size * 64f;
		var placed = new List<TerrainEditor.Editing.Hammer.Placed>();
		lock (scene.Things)
		{
			for (int i = 0; i < scene.Things.Count; i++)
			{
				var t = scene.Things[i];
				if (!t.Gone && _w.NameOfPrefab(t.Prefab) is string n && TerrainEditor.Editing.Hammer.Get(n) != null)
				{
					placed.Add(new(i, n, t.Position, TerrainEditor.App.BlueprintFormats.FromEuler(t.Rotation)));
				}
			}
		}
		var adds = new List<(TerrainEditor.Editing.NewObject, bool)>();
		var report = new List<object>();
		var problems = new List<string>();
		foreach (var (p, k) in pieces.Select((p, k) => (p, k)))
		{
			int hash = TerrainEditor.Save.StableHash.Of(p.Prefab);
			var piece = TerrainEditor.Terrain.PieceCatalog.Get(hash);
			if (piece == null && TerrainEditor.Terrain.PrefabCatalog.Get(hash) == null)
			{
				problems.Add($"#{k} {p.Prefab}: not an object the game has (find_prefabs)");
				continue;
			}
			if (p.X < minX || p.X > maxX || p.Z < minZ || p.Z > maxZ)
			{
				problems.Add($"#{k} {p.Prefab}: ({p.X}, {p.Z}) is outside the area (x {minX} to {maxX}, z {minZ} to {maxZ})");
				continue;
			}
			var euler = new System.Numerics.Vector3(p.Pitch, p.Yaw, p.Roll);
			var q = TerrainEditor.App.BlueprintFormats.FromEuler(euler);
			float y = p.Y ?? GroundAt(scene, p.X, p.Z) - TerrainEditor.Editing.Hammer.Bottom(p.Prefab, q);
			var pos = new System.Numerics.Vector3(p.X, y, p.Z);
			int? to = null;
			if (p.Snap)
			{
				(pos, to) = TerrainEditor.Editing.Hammer.Snap(p.Prefab, q, pos, placed);
			}
			placed.Add(new(-1 - k, p.Prefab, pos, q));
			adds.Add((new TerrainEditor.Editing.NewObject(0, hash, pos, euler, 0), piece?.Tool != null));
			report.Add(new { prefab = p.Prefab, x = MathF.Round(pos.X, 3), y = MathF.Round(pos.Y, 3), z = MathF.Round(pos.Z, 3), snapped = to != null });
		}
		if (adds.Count == 0)
		{
			return "Nothing put down: " + string.Join("; ", problems);
		}
		var ids = session.Commit(label.StartsWith("Claude", StringComparison.Ordinal) ? label : "Claude: " + label, null, Array.Empty<int>(), adds);
		_w.AfterClaudeEdit($"Claude put down {adds.Count} piece(s); Ctrl+Z takes them back.");
		return ToJson(new { placed = report.Select((r, i) => new { id = i < ids.Count ? ids[i] : -1, piece = r }), problems, pending = Pending() });
	}

	// ---- Building in larger pieces (ClaudeBuilder: floors, walls, roofs over a rectangle).

	public sealed class DoorSpec
	{
		[Description("Which wall: north, south, east or west.")]
		public string Side { get; set; } = "south";

		[Description("Where along it: x for a north or south wall, z for an east or west one (a 2 m door at the 2 m of wall holding that point).")]
		public float At { get; set; }
	}

	// The highest ground under a rectangle (every metre).
	private static float HighestGround(WorldScene scene, float x0, float z0, float x1, float z1, bool edgesOnly = false)
	{
		float best = float.MinValue;
		for (float x = MathF.Min(x0, x1); x <= MathF.Max(x0, x1); x += 1)
		{
			for (float z = MathF.Min(z0, z1); z <= MathF.Max(z0, z1); z += 1)
			{
				bool edge = x - MathF.Min(x0, x1) < 1 || MathF.Max(x0, x1) - x < 1 || z - MathF.Min(z0, z1) < 1 || MathF.Max(z0, z1) - z < 1;
				if (!edgesOnly || edge)
				{
					best = MathF.Max(best, GroundAt(scene, x, z));
				}
			}
		}
		return best;
	}

	// The highest top of the building pieces within a rectangle (whose names hold word, when given), or null.
	private float? HighestPieceTop(WorldScene scene, float x0, float z0, float x1, float z1, string? word = null)
	{
		float? best = null;
		lock (scene.Things)
		{
			foreach (var t in scene.Things)
			{
				if (t.Gone || !t.Piece || _w.NameOfPrefab(t.Prefab) is not string n || (word != null && !n.Contains(word, StringComparison.OrdinalIgnoreCase))
					|| TerrainEditor.Terrain.PieceCatalog.Get(t.Prefab) is not { } info)
				{
					continue;
				}
				var p = t.Position;
				if (p.X < MathF.Min(x0, x1) - 0.6f || p.X > MathF.Max(x0, x1) + 0.6f || p.Z < MathF.Min(z0, z1) - 0.6f || p.Z > MathF.Max(z0, z1) + 0.6f)
				{
					continue;
				}
				float top = p.Y + info.MaxY;
				best = best is float b ? MathF.Max(b, top) : top;
			}
		}
		return best;
	}

	private Task<string> Build(Func<WorldScene, List<PieceSpec>> layout, string label) => OnUi(() =>
	{
		if (CannotEdit() is string why)
		{
			return why;
		}
		try
		{
			return Place(layout(_w.Session!.Scene), label);
		}
		catch (ArgumentException ex)
		{
			return ex.Message;
		}
	});

	[McpServerTool(Name = "build_floor", Title = "Build a floor")]
	[Description("Lays a floor of 2 x 2 m tiles (wood or stone) over a rectangle, its top at y (default: the highest ground under it, so that it rests on the ground). One pending step. Sizes that are not multiples of 2 m overhang a little.")]
	public Task<string> BuildFloor(
		[Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
		[Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
		[Description("wood or stone.")] string material = "wood",
		[Description("Height of its top (optional).")] float? y = null) =>
		Build(scene => ClaudeBuilder.Floor(x0, z0, x1, z1, y ?? HighestGround(scene, x0, z0, x1, z1), material), $"floor of {material}");

	[McpServerTool(Name = "build_walls", Title = "Build walls")]
	[Description("Builds walls on the four edges of a rectangle (the walls' middle lines on its edges), from the bottom up to a height: wood (2 m rows) or stone (1 m rows); doors are 2 m wood doors in the walls at the given points. Bottom: the top of a floor already in the rectangle, else the highest ground on its edges. One pending step. Use the floor's own rectangle so that they meet.")]
	public Task<string> BuildWalls(
		[Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
		[Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
		[Description("Height, metres (2 or 4 for one storey).")] float height = 4,
		[Description("wood or stone.")] string material = "wood",
		[Description("Doors (optional).")] DoorSpec[]? doors = null,
		[Description("Height of the walls' bottom (optional).")] float? y = null) =>
		Build(scene => ClaudeBuilder.Walls(x0, z0, x1, z1, y ?? HighestPieceTop(scene, x0, z0, x1, z1, "floor") ?? HighestGround(scene, x0, z0, x1, z1, edgesOnly: true),
			height, material, (doors ?? Array.Empty<DoorSpec>()).Select(d => new ClaudeBuilder.Door(d.Side, d.At)).ToList()), $"walls of {material}");

	[McpServerTool(Name = "build_roof", Title = "Build a roof")]
	[Description("Builds a gable roof over a rectangle (the walls' lines), its ridge along the longer side: thatch or shingle, 26 or 45 degrees, its underside meeting the wall lines at the eave height (default: the top of the building pieces in the rectangle, i.e. the walls), overhanging up to 2 m; the two gable ends closed with sloped walls (gables: true). One pending step. Check it with support_check and screenshot (views: 4).")]
	public Task<string> BuildRoof(
		[Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
		[Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
		[Description("26 or 45 degrees.")] int angle = 45,
		[Description("thatch or shingle.")] string material = "thatch",
		[Description("Close the gable ends with sloped walls (default true).")] bool gables = true,
		[Description("Height of the walls' tops (optional).")] float? eave = null) =>
		Build(scene => ClaudeBuilder.Roof(x0, z0, x1, z1,
			eave ?? HighestPieceTop(scene, x0, z0, x1, z1) ?? throw new ArgumentException("No walls under the roof: build the walls first, or give eave."),
			angle, material, gables), $"{material} roof");

	// The ground's height at a world point of the area (its nearest grid point).
	private static float GroundAt(WorldScene scene, float x, float z)
	{
		int i = Math.Clamp((int)MathF.Round(x - scene.Cx + (scene.W - 1) / 2f), 0, scene.W - 1);
		int j = Math.Clamp((int)MathF.Round(z - scene.Cz + (scene.H - 1) / 2f), 0, scene.H - 1);
		return scene.Heights[j * scene.W + i];
	}

	[McpServerTool(Name = "remove_objects", Title = "Take objects away")]
	[Description("Takes objects of the open area (or the plot) away, by their ids (list_objects, place_pieces), as one step of the history (pending).")]
	public Task<string> RemoveObjects([Description("Their ids.")] int[] ids) => OnUi(() =>
	{
		if (CannotEdit() is string why)
		{
			return why;
		}
		var session = _w.Session!;
		var things = session.Scene.Things;
		var ok = ids.Distinct().Where(i => i >= 0 && i < things.Count && !things[i].Gone).ToList();
		if (ok.Count == 0)
		{
			return "None of those ids is an object of the area.";
		}
		session.Commit($"Claude: took {ok.Count} away", null, ok, Array.Empty<(TerrainEditor.Editing.NewObject, bool)>());
		_w.AfterClaudeEdit($"Claude took {ok.Count} object(s) away; Ctrl+Z puts them back.");
		return $"Took {ok.Count} away{(ok.Count < ids.Length ? $" ({ids.Length - ok.Count} id(s) were not objects of the area)" : "")}. Now pending: {Pending()}";
	});

	[McpServerTool(Name = "support_check", ReadOnly = true, Title = "Would it stand?")]
	[Description("The Workshop's support check, as the game computes it: for the building pieces on the plot, which would fall (nothing holds them up enough) and the weakest ones still standing (support 0 about to fall, 1 strong). Pieces on the ground hold best; support drops with each piece away from it (wood less far than stone, iron further).")]
	public Task<string> SupportCheck() => OnUi(() =>
	{
		if (!_w.InWorkshop)
		{
			return "The support check is the Workshop's: open_workshop first.";
		}
		_w.SupportBox.IsChecked = true;
		_w.RefreshSupport();
		if (_w.LastSupport is not { } r || _w.Session?.Scene is not { } scene)
		{
			return "No building piece on the plot.";
		}
		var things = _w.LastSupportThings;
		object Piece(int k) => new
		{
			id = things[k], name = _w.NameOfPrefab(scene.Things[things[k]].Prefab),
			x = MathF.Round(scene.Things[things[k]].Position.X, 2), y = MathF.Round(scene.Things[things[k]].Position.Y, 2), z = MathF.Round(scene.Things[things[k]].Position.Z, 2),
			support = MathF.Round(r.Colour[k], 2),
		};
		return ToJson(new
		{
			pieces = things.Count,
			wouldFall = r.Breaking,
			falling = r.Falls.Take(60).Select(Piece),
			// Support: 0 (red, about to fall) to 1 (green); pieces on the ground (light blue in the editor) hold best and are left out.
			weakest = Enumerable.Range(0, things.Count).Where(k => !r.Free[k] && !r.Breaks[k] && r.Colour[k] >= 0).OrderBy(k => r.Colour[k]).Take(10).Select(Piece),
			onTheGround = Enumerable.Range(0, things.Count).Count(k => r.Colour[k] < 0),
		});
	});

	// What a container gets.
	public sealed class ItemSpec
	{
		[Description("The item's prefab name, e.g. Coins, Amber, Ruby, IronScrap, SwordIron (find_prefabs with items: true).")]
		public string Item { get; set; } = "";

		[Description("How many in the stack (default 1).")]
		public int Stack { get; set; } = 1;

		[Description("Quality (upgrade level) for weapons and armour (default 1).")]
		public int Quality { get; set; } = 1;

		[Description("Variant: a shield's or banner's style, from 0 (default 0).")]
		public int Variant { get; set; }
	}

	[McpServerTool(Name = "set_contents", Title = "Fill a chest or a stand, write a sign, set stars")]
	[Description("Changes what an object holds, as the editor's inspector does: a container's items (they replace what it held, laid out in its slots in order); an item stand's item (itemstand on a wall, itemstandh lying flat: a table's food, a trophy, a weapon; one item, none to empty it) and the way it hangs; an armour stand's armour, cape, belt, shield and weapon (each in the slot that takes it; the others emptied) and its pose; a sign's text; a creature's stars (0 to 2). Stands take what the game lets them (find_prefabs with items: true for names). One pending step; the object keeps everything else it holds.")]
	public Task<string> SetContents(
		[Description("The object's id (list_objects, place_pieces).")] int id,
		[Description("For a container (chest, barrel…): its items; for an item stand: one item (or none: empty); for an armour stand: what it wears (optional).")] ItemSpec[]? items = null,
		[Description("For a sign: its text (optional).")] string? text = null,
		[Description("For a creature: its stars, 0 to 2 (optional).")] int? stars = null,
		[Description("For an item stand: the way the item hangs, from 0 (the game's orientations, as alt + use cycles them; optional).")] int? orientation = null,
		[Description("For an armour stand: its pose, from 0 (optional).")] int? pose = null) => OnUi(() =>
	{
		if (CannotEdit() is string why)
		{
			return why;
		}
		var s = _w.Session!;
		var things = s.Scene.Things;
		if (id < 0 || id >= things.Count || things[id].Gone)
		{
			return "No object with that id in the area.";
		}
		var t = things[id];
		var info = TerrainEditor.Terrain.PrefabCatalog.Details(t.Prefab);
		var set = new List<TerrainEditor.App.FieldChange>();
		List<TerrainEditor.App.ItemUpload>? inv = null;
		bool itemStand = TerrainEditor.App.StandData.IsItemStand(t.Prefab), armourStand = TerrainEditor.App.StandData.IsArmourStand(t.Prefab);
		if ((orientation != null && !itemStand) || (pose != null && !armourStand))
		{
			return $"{_w.NameOfPrefab(t.Prefab)} is not {(orientation != null ? "an item stand: only item stands take an orientation" : "an armour stand: only armour stands take a pose")}.";
		}
		try
		{
			if (itemStand && (items != null || orientation != null))
			{
				if (items is { Length: > 1 })
				{
					return $"{_w.NameOfPrefab(t.Prefab)} holds one item, not {items.Length}.";
				}
				if (items != null)
				{
					var held = items.Length == 0 ? null : new TerrainEditor.App.StandData.Held(items[0].Item, items[0].Quality, items[0].Variant);
					set.AddRange(TerrainEditor.App.StandData.ForItemStand(t.Prefab, held, orientation ?? 0));
				}
				else
				{
					set.Add(new("ints", "type", Math.Max(0, orientation!.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)));
				}
				items = null;
			}
			if (armourStand && (items != null || pose != null))
			{
				if (items != null)
				{
					set.AddRange(TerrainEditor.App.StandData.ForArmourStand(t.Prefab, items.Select(i => new TerrainEditor.App.StandData.Held(i.Item, i.Quality, i.Variant)).ToList(), pose));
				}
				else
				{
					set.Add(new("ints", "pose", Math.Max(0, pose!.Value).ToString(System.Globalization.CultureInfo.InvariantCulture)));
				}
				items = null;
			}
		}
		catch (ArgumentException ex)
		{
			return ex.Message;
		}
		if (items != null)
		{
			if (info is not { ContainerW: > 0 } box)
			{
				return $"{_w.NameOfPrefab(t.Prefab)} is not a container or a stand.";
			}
			if (items.Length > box.ContainerW * box.ContainerH)
			{
				return $"{_w.NameOfPrefab(t.Prefab)} has {box.ContainerW * box.ContainerH} slots, not {items.Length}.";
			}
			inv = items.Select((it, i) => new TerrainEditor.App.ItemUpload(it.Item, null, Math.Max(1, it.Stack), Math.Max(1, it.Quality), X: i % box.ContainerW, Y: i / box.ContainerW)).ToList();
		}
		string kindName = _w.NameOfPrefab(t.Prefab) ?? "";
		if (text != null)
		{
			if (!kindName.Contains("sign", StringComparison.OrdinalIgnoreCase))
			{
				return $"{kindName} is not a sign: only signs take a text.";
			}
			set.Add(new("strings", "text", text));
		}
		if (stars is int st)
		{
			if (!TerrainEditor.Terrain.PrefabCatalog.IsCreature(t.Prefab))
			{
				return $"{kindName} is not a creature: only creatures have stars.";
			}
			set.Add(new("ints", "level", (Math.Clamp(st, 0, 2) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
		}
		if (set.Count == 0 && inv == null)
		{
			return "Nothing to change: give items, text, stars, an orientation or a pose.";
		}
		string name = _w.NameOfPrefab(t.Prefab) ?? "object";
		try
		{
			TerrainEditor.Editing.NewObject replacement;
			// (The Workshop's plot has no world files behind it: its objects carry their values.)
			if (!_w.InWorkshop && s.Scene.World is { } world && TerrainEditor.App.ObjectData.Bytes(world, s.Edits, t.Id) is { } bytes)
			{
				// A world's object: its own data, changed (as the inspector does).
				var z = TerrainEditor.App.ObjectData.Edited(bytes, set, inv);
				replacement = new(0, z.Prefab, z.Position, z.Rotation, 0, null, false, z.Serialize());
			}
			else
			{
				// On the Workshop's plot: its values, with these.
				var fields = (s.Edits.FindAdded(t.Id)?.Data ?? Array.Empty<TerrainEditor.Editing.ObjectField>()).ToList();
				void Put(string section, string key, string? value)
				{
					int k = TerrainEditor.Save.StableHash.Of(key);
					fields.RemoveAll(f => f.Key == k);
					if (value != null)
					{
						fields.Add(new(section, k, value));
					}
				}
				foreach (var f in set)
				{
					Put(f.Section, f.Key, f.Value);
				}
				if (inv != null)
				{
					Put("bytes", "items", Convert.ToBase64String(TerrainEditor.App.ObjectData.BuildInventory(inv).Write()));
					Put("ints", "addedDefaultItems", "1");
				}
				replacement = new(0, t.Prefab, t.Position, t.Rotation, t.Scale, Data: fields);
			}
			var copies = s.Commit($"Claude: filled {name}", null, new[] { id }, new[] { (replacement, t.Piece) });
			_w.AfterClaudeEdit($"Claude changed what {name} holds; Ctrl+Z puts it back.");
			return $"Changed {name}; its id is now {copies[0]}.";
		}
		catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
		{
			return $"Could not change it: {ex.Message}";
		}
	});

	[McpServerTool(Name = "open_workshop", Title = "Open the Workshop")]
	[Description("Opens the Workshop: a blank 192 m plot (ground at y = 34, its middle at x = 0, z = 0) to build a blueprint on, optionally with a blueprint of the library on it (list_blueprints). Refused while a world or the Workshop has changes not saved. The user saves the building as a blueprint.")]
	public Task<string> OpenWorkshop([Description("A blueprint's name from list_blueprints (optional: an empty plot).")] string? blueprint = null) => OnUi(async () =>
	{
		if (Unsaved() is string why)
		{
			return why;
		}
		string? path = null;
		if (!string.IsNullOrWhiteSpace(blueprint))
		{
			path = Path.Combine(_w.Blueprints.Status.Folder, TerrainEditor.App.Homestead.FileName(blueprint));
			if (!File.Exists(path))
			{
				return $"No blueprint \"{blueprint}\" in the library (list_blueprints).";
			}
		}
		await _w.OpenWorkshop(path);
		return ToJson(StateOf(_w));
	});

	[McpServerTool(Name = "list_blueprints", ReadOnly = true, Title = "The blueprint library")]
	[Description("The user's blueprints (Homestead's library): name, pieces, cost, description, tags. Open one in the Workshop (open_workshop) or add it to the plot (add_blueprint).")]
	public Task<string> ListBlueprints([Description("Words to find in the name, description or tags (optional).")] string? query = null) => OnUi(() =>
	{
		var words = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
		return ToJson(TerrainEditor.App.Homestead.List(_w.Blueprints.Status.Folder)
			.Where(e => words.All(w => e.Name.Contains(w, StringComparison.OrdinalIgnoreCase) || e.Description.Contains(w, StringComparison.OrdinalIgnoreCase) || e.Tags.Any(t => t.Contains(w, StringComparison.OrdinalIgnoreCase))))
			.Select(e => new { name = e.Name, pieces = e.Pieces, cost = TerrainEditor.Terrain.PieceCost.Of(e.Kinds).Describe(4), description = e.Description, tags = e.Tags }));
	});

	[McpServerTool(Name = "add_blueprint", Title = "Add a blueprint to the plot")]
	[Description("Adds a blueprint of the library to the Workshop's plot, with what is there, centred on x, z (default: the plot's middle), standing on the ground; one step of the history.")]
	public Task<string> AddBlueprint(
		[Description("The blueprint's name (list_blueprints).")] string blueprint,
		[Description("World x of its middle (optional).")] float? x = null,
		[Description("World z of its middle (optional).")] float? z = null) => OnUi(() =>
	{
		if (!_w.InWorkshop)
		{
			return "Blueprints are added in the Workshop: open_workshop first.";
		}
		string path = Path.Combine(_w.Blueprints.Status.Folder, TerrainEditor.App.Homestead.FileName(blueprint));
		if (!File.Exists(path))
		{
			return $"No blueprint \"{blueprint}\" in the library (list_blueprints).";
		}
		_w.AddToWorkshop(path, x is float ax && z is float az ? new System.Numerics.Vector2(ax, az) : null);
		return _w.MessageText.Text + " Now pending: " + Pending();
	});

	[McpServerTool(Name = "generate_dungeon", Title = "Generate a dungeon")]
	[Description("Generates a dungeon, as the Dungeon panel does, and puts it on the Workshop's plot (with what is there) or, in a world, 5000 m above the point x, z (where the game keeps its dungeons) with a linked portal pair on the ground there. One step of the history (pending).")]
	public async Task<string> GenerateDungeon(
		[Description("Its biome: Meadows, Black Forest, Swamp, Mountain, Plains, Mistlands, Ashlands, Deep North.")] string biome = "Black Forest",
		[Description("Its style (optional: the biome's): Crypt, Catacombs, Temple, Fortress, Prison, Dvergr hold, Goblin warren, Ruins.")] string? style = null,
		[Description("Its wall material (optional: the biome's).")] string? walls = null,
		[Description("Rooms per level: 1 small to 4 huge.")] int size = 2,
		[Description("Levels, 1 to 4.")] int levels = 2,
		[Description("Monsters: 0 none to 3 (1 usual).")] float monsters = 1,
		[Description("Chests and treasure: 0 none to 2 (1 usual).")] float loot = 1,
		[Description("Light: 0 dark, 1 dim, 2 bright.")] int light = 1,
		[Description("Furniture: 0 bare, 1 furnished, 2 rich.")] int decor = 1,
		[Description("Any number: each makes another dungeon with the same choices.")] int seed = 1,
		[Description("Its name, on a sign at the entrance (optional: made up).")] string? name = null,
		[Description("In a world: x of the point of the open area it goes above (optional: the view's middle).")] float? x = null,
		[Description("In a world: z of that point.")] float? z = null)
	{
		var settings = new TerrainEditor.Editing.DungeonGen.Settings(Biome: biome, Style: style, Walls: walls, Size: Math.Clamp(size, 1, 4), Levels: Math.Clamp(levels, 1, 4),
			Monsters: Math.Clamp(monsters, 0, 3), Loot: Math.Clamp(loot, 0, 2), Light: Math.Clamp(light, 0, 2), Decor: Math.Clamp(decor, 0, 2), Seed: seed, Name: name);
		if (await OnUi(() => CannotEdit()) is string why)
		{
			return why;
		}
		var result = await Task.Run(() => TerrainEditor.Editing.DungeonGen.Make(settings));
		return await OnUi(async () =>
		{
			if (_w.InWorkshop)
			{
				await _w.GeneratedOntoPlot(settings, result, add: true);
			}
			else
			{
				if (x is float px && z is float pz)
				{
					// Above a point of the open area (its ground is the area's).
					var sc = _w.Session!.Scene;
					float minX = sc.X0 * 64f - 32f, minZ = sc.Z0 * 64f - 32f;
					if (px < minX || pz < minZ || px > minX + sc.Size * 64f || pz > minZ + sc.Size * 64f)
					{
						return $"({px}, {pz}) is outside the open area (x {minX} to {minX + sc.Size * 64f}, z {minZ} to {minZ + sc.Size * 64f}): open_area there first.";
					}
					_w.View.Orbit(px, pz, 45, 55, 120);
				}
				_w.PlaceGenerated(settings, result);
			}
			return $"{string.Join(" ", result.Notes)} {_w.MessageText.Text} Now pending: {Pending()}";
		});
	}

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
