using System.ComponentModel;
using System.Text.Json;
using Avalonia.Threading;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

/// <summary>
/// What Claude can do in the editor (ClaudeServer): look at what is open, and change it as the user would,
/// on the window's thread. Coordinates are the game's: x east, z north, y up, in metres. Every change
/// stays pending, one step of the history each: the user saves, applies live or saves the blueprint.
/// </summary>
/// <remarks>
/// Each tool's description for Claude is its <c>[Description]</c> attribute; the summaries here are for the code.
/// </remarks>
[McpServerToolType]
public sealed partial class ClaudeTools
{
    /// <summary>The editor's window, whose world, session and view the tools work on.</summary>
    private readonly MainWindow _w;

    /// <summary>The tools for this window.</summary>
    internal ClaudeTools(MainWindow window)
    {
        _w = window;
        ViewPicture = _w.View.Picture;
        MapPicture = path => _w.MapPage!.Map.Picture(path);
    }

    /// <summary>Saves a picture of the 3D view as a PNG file (the view's, drawn by OpenGL; tests give their own).</summary>
    internal Func<string, Task> ViewPicture { get; set; }

    /// <summary>Saves a picture of the world's map as a PNG file (the map's, drawn by OpenGL; tests give their own).</summary>
    internal Func<string, Task> MapPicture { get; set; }

    /// <summary>The worlds folders create_world may make a world in (the game's that exist; tests give their own).</summary>
    internal Func<List<string>> WorldFolders { get; set; } = NewWorldPage.Roots;

    /// <summary>Compact JSON for the answers.</summary>
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    /// <summary>An answer as compact JSON.</summary>
    internal static string ToJson(object o) => JsonSerializer.Serialize(o, Json);

    /// <summary>On the window's thread, as a click would be.</summary>
    private static Task<T> OnUi<T>(Func<Task<T>> f) => Dispatcher.UIThread.InvokeAsync(f);

    /// <summary>On the window's thread, as a click would be.</summary>
    private static Task<T> OnUi<T>(Func<T> f) => Dispatcher.UIThread.InvokeAsync(f).GetTask();

    /// <summary>
    /// The camera tool running now (done when none is): one at a time moves the camera or takes a picture, since
    /// Claude's calls run side by side, and a picture awaited after the camera was set would otherwise come from
    /// another call's camera. Used on the window's thread only.
    /// </summary>
    private Task _camera = Task.CompletedTask;

    /// <summary>On the window's thread, the camera this call's alone until it ends (<see cref="_camera"/>).</summary>
    private Task<T> WithCamera<T>(Func<Task<T>> f) => OnUi(async () =>
    {
        while (!_camera.IsCompleted)
        {
            await _camera;
        }
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _camera = done.Task;
        try
        {
            return await f();
        }
        finally
        {
            done.SetResult();
        }
    });

    /// <summary>On the window's thread, the camera this call's alone until it ends (<see cref="_camera"/>).</summary>
    private Task<T> WithCamera<T>(Func<T> f) => WithCamera(() => Task.FromResult(f()));

    /// <summary>editor_state: the page shown, the world, the area's bounds, what is pending (see <see cref="StateOf"/>).</summary>
    [McpServerTool(Name = "editor_state", ReadOnly = true, Title = "What the editor shows")]
    [Description("What the editor shows now: the start page, a world's map, an area of a world in 3D, or the Workshop (a blank plot to build a blueprint on); the world, the area's bounds in world coordinates, the changes not saved yet, and the editor's last message. Call it first.")]
    public Task<string> EditorState() => OnUi(() => ToJson(StateOf(_w)));

    // ---- Looking.

    /// <summary>list_worlds: the start page's worlds.</summary>
    [McpServerTool(Name = "list_worlds", ReadOnly = true, Title = "Worlds on this computer")]
    [Description("The Valheim worlds on this computer (the start page's list): name, folder, last save. Open one with open_world.")]
    public Task<string> ListWorlds() => OnUi(() => ToJson(TerrainEditor.App.Worlds.Find(_w.Settings)
        .Select(i => new { name = i.Name, folder = i.Path, saved = i.Saved, where = i.Where, usable = i.Usable, problem = i.Problem })));

    /// <summary>open_world: opens a world offline, refused while there are unsaved changes.</summary>
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

    /// <summary>open_area: opens the zones around a world point in 3D (size clamped to 1 to 17 zones).</summary>
    [McpServerTool(Name = "open_area", Title = "Open an area in 3D")]
    [Description("Opens the square area around a point of the open world in the 3D editor, where it can be looked at and changed. The world is made of 64 m zones; size is how many zones across (1 to 17; 3 = 192 m is usual; large areas are slow to open and draw). Changes made in another area stay pending.")]
    public Task<string> OpenArea(
        [Description("World x (east) of the area's middle, in metres.")] float x,
        [Description("World z (north) of the area's middle, in metres.")] float z,
        [Description("Zones across: 1 to 17.")] int size = 3) => OnUi(async () =>
    {
        if (_w.World == null)
        {
            return "Open a world first (open_world).";
        }
        int zx = (int)MathF.Floor((x + 32) / 64), zz = (int)MathF.Floor((z + 32) / 64);
        await _w.EditArea(zx, zz, Math.Clamp(size, 1, MainWindow.MaxFollowZones));
        return ToJson(StateOf(_w));
    });

    /// <summary>describe_area: the open area's bounds, heights, biomes and objects counted by kind and by name (the top 40).</summary>
    [McpServerTool(Name = "describe_area", ReadOnly = true, Title = "Describe the open area")]
    [Description("The open area (or the Workshop's plot): its bounds, ground heights (lowest, highest, average; the water level), its biomes, and its objects counted by kind and by name.")]
    public Task<string> DescribeArea() => OnUi(() =>
    {
        if (_w.Session?.Scene is not { } scene)
        {
            return "No area is open (open_area, or the Workshop).";
        }
        float[] h = scene.Heights;
        var biomes = scene.Biomes.GroupBy(b => ((ValheimGen.Heightmap.Biome)b).ToString()).OrderByDescending(g => g.Count())
            .Select(g => new { biome = g.Key, share = MathF.Round(100f * g.Count() / scene.Biomes.Length, 1) });
        List<(string Name, string Kind)> things;
        lock (scene.Things)
        {
            things = [.. scene.Things.Where(t => !t.Gone).Select(t => (Name: _w.NameOfPrefab(t.Prefab) ?? t.Prefab.ToString(), Kind: KindOf(t)))];
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

    /// <summary>A thing's object kind, as describe_area and list_objects name it.</summary>
    private string KindOf(WorldScene.Thing t) => TerrainEditor.App.ObjectKinds.Of(_w.NameOfPrefab(t.Prefab), t.Piece, t.Tamed).ToString();

    /// <summary>list_objects: objects of the open area, filtered by kind, name and distance (limit clamped to 1 to 2000).</summary>
    [McpServerTool(Name = "list_objects", ReadOnly = true, Title = "List objects")]
    [Description("Objects of the open area: id, name, kind, position (x, y, z), turn (yaw in degrees), tilts (pitch, roll) and size (scale) when it has them, and whether a player built it. Filter by kind (Buildings, Trees, Rocks…), by a word of the name, and/or near a point; at most limit of them, nearest first when near a point.")]
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
        int reads;
        lock (scene.Things)
        {
            reads = scene.Reads;
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
            found = [.. q.Take(Math.Clamp(limit, 1, 2000)).Select(p => Describe(p.t, p.i))];
        }
        // (A listing with nothing in it gives no id: ids given before stay refused.)
        if (found.Count > 0)
        {
            GaveIds(scene, reads);
        }
        return ToJson(found);
    });

    /// <summary>get_selection: the objects selected in the view.</summary>
    [McpServerTool(Name = "get_selection", ReadOnly = true, Title = "What the user selected")]
    [Description("The objects the user has selected in the editor (Select tool): id, name, kind, position, turn. Use it when the user says \"these\", \"the selected trees\", \"this building\".")]
    public Task<string> GetSelection() => OnUi(() =>
    {
        if (_w.Session?.Scene is not { } scene)
        {
            return "No area is open.";
        }
        List<Dictionary<string, object?>> selected;
        int reads;
        lock (scene.Things)
        {
            reads = scene.Reads;
            selected = [.. _w.View.Selected.Where(i => i >= 0 && i < scene.Things.Count && !scene.Things[i].Gone).Select(i => Describe(scene.Things[i], i))];
        }
        if (selected.Count > 0)
        {
            GaveIds(scene, reads);
        }
        return ToJson(selected);
    });

    /// <summary>select_objects: selects objects by id and points the camera at them; changes nothing in the world.</summary>
    [McpServerTool(Name = "select_objects", Title = "Select objects for the user")]
    [Description("Selects objects in the editor (by id), so the user sees which ones are meant, and points the camera at them. Changes nothing in the world.")]
    public Task<string> SelectObjects(
        [Description("Their ids (list_objects).")] int[] ids,
        [Description("Point the camera at them (default: true).")] bool focus = true) => WithCamera(() =>
    {
        if (_w.Session?.Scene is not { } scene)
        {
            return "No area is open.";
        }
        if (StaleIds(scene) is string stale)
        {
            return stale;
        }
        int[] asked = [.. ids.Distinct()];
        var ok = asked.Where(i => i >= 0 && i < scene.Things.Count && !scene.Things[i].Gone).ToList();
        _w.View.Select(ok);
        if (focus && ok.Count > 0)
        {
            var mid = ok.Select(i => scene.Things[i].Position).Aggregate(System.Numerics.Vector3.Zero, (a, b) => a + b) / ok.Count;
            float spread = ok.Select(i => System.Numerics.Vector3.Distance(scene.Things[i].Position, mid)).DefaultIfEmpty(0).Max();
            _w.View.Focus(mid, MathF.Max(20, spread * 2.5f));
        }
        return $"Selected {ok.Count}{(ok.Count < asked.Length ? $" ({asked.Length - ok.Count} id(s) were not objects of the area)" : "")}.";
    });

    /// <summary>
    /// An object as the tools give it: id (its index in the scene), name, kind, position, yaw, built; its tilts and
    /// size only when it has them (most objects stand upright at the prefab's own size).
    /// </summary>
    private Dictionary<string, object?> Describe(WorldScene.Thing t, int i)
    {
        var d = new Dictionary<string, object?>
        {
            ["id"] = i,
            ["name"] = _w.NameOfPrefab(t.Prefab),
            ["kind"] = KindOf(t),
            ["x"] = MathF.Round(t.Position.X, 2),
            ["y"] = MathF.Round(t.Position.Y, 2),
            ["z"] = MathF.Round(t.Position.Z, 2),
            ["yaw"] = MathF.Round(t.Rotation.Y, 1),
            ["built"] = t.Piece,
        };
        if (MathF.Abs(t.Rotation.X) >= 0.05f || MathF.Abs(t.Rotation.Z) >= 0.05f)
        {
            d["pitch"] = MathF.Round(t.Rotation.X, 1);
            d["roll"] = MathF.Round(t.Rotation.Z, 1);
        }
        if (t.Scale > 0 && MathF.Abs(t.Scale - 1) >= 1e-3f)
        {
            d["scale"] = MathF.Round(t.Scale, 3);
        }
        return d;
    }

    /// <summary>
    /// screenshot: the 3D view (from the camera given, else as it is) or the map, as a JPEG no wider than 1280
    /// pixels; four views in one with <c>views</c> 4 or more.
    /// </summary>
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
            string? problem = await WithCamera(async () =>
            {
                if (_w.MapShown)
                {
                    await MapPicture(path).WaitAsync(TimeSpan.FromSeconds(60));
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
                await ViewPicture(path).WaitAsync(TimeSpan.FromSeconds(60));
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

    /// <summary>Four views of a point (the area's middle when none is given), as one picture with their names.</summary>
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
                    string? problem = await WithCamera(async () =>
                    {
                        if (_w.Session?.Scene is not { } scene || _w.MapShown)
                        {
                            return "Four views need an area or the Workshop open in 3D.";
                        }
                        float cx = x ?? scene.Cx, cz = z ?? scene.Cz;
                        _w.View.Orbit(cx, cz, yaw, pitch, distance, height);
                        await ViewPicture(path).WaitAsync(TimeSpan.FromSeconds(60));
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

    /// <summary>area_map: a top-down map of the open area drawn from its data (see <see cref="DrawMap"/>), with its bounds in words.</summary>
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
                things = [.. scene.Things.Where(t => !t.Gone).Select(t => (t.Position.X, t.Position.Z, KindOf(t)))];
            }
            // Paint (dirt, cultivated, paved) per ground point, as the ground has it now.
            var g = _w.Session.Ground;
            float[]? paint = g.W == scene.W && g.H == scene.H ? [.. Enumerable.Range(0, scene.W * scene.H * 3).Select(k => g.MaskOf(k / 3, k % 3))] : null;
            return new { scene.W, scene.H, scene.Cx, scene.Cz, scene.Water, Heights = (float[])scene.Heights.Clone(), Paint = paint, Things = things };
        });
        if (data == null)
        {
            return new() { IsError = true, Content = { new ModelContextProtocol.Protocol.TextContentBlock { Text = "No area is open." } } };
        }
        byte[] jpeg = await Task.Run(() => DrawMap(data.W, data.H, data.Cx, data.Cz, data.Water, data.Heights, data.Paint, objects ? data.Things : [], MathF.Max(4, grid)));
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

    /// <summary>
    /// The area's map, north up: heights in colour with hill shading, paint, water, objects as dots by kind, and a grid
    /// labelled in world metres, as a JPEG.
    /// </summary>
    /// <param name="w">The grid's width (points, one per metre).</param>
    /// <param name="h">The grid's height.</param>
    /// <param name="cx">World x of the grid's middle.</param>
    /// <param name="cz">World z of the grid's middle.</param>
    /// <param name="water">The water level, in metres.</param>
    /// <param name="heights">The ground heights, row by row from the south.</param>
    /// <param name="paint">Dirt, cultivated and paved weights per point (three each), null: none.</param>
    /// <param name="things">The objects: world x, z and kind.</param>
    /// <param name="grid">The grid lines' spacing, in metres.</param>
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

    /// <summary>A picture no wider than 1280 pixels, as a JPEG (smaller to send and to look at).</summary>
    internal static byte[] Shrink(byte[] png)
    {
        using var bitmap = SkiaSharp.SKBitmap.Decode(png);
        float k = Math.Min(1f, 1280f / bitmap.Width);
        using var scaled = k < 1 ? bitmap.Resize(new SkiaSharp.SKImageInfo((int)(bitmap.Width * k), (int)(bitmap.Height * k)), new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Linear)) : bitmap.Copy();
        using var image = SkiaSharp.SKImage.FromBitmap(scaled);
        return image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 82).ToArray();
    }

    /// <summary>find_prefabs: building pieces (with name, tool, size and cost) and other placeable objects, or items, by words of the name.</summary>
    [McpServerTool(Name = "find_prefabs", ReadOnly = true, Title = "Find what can be placed")]
    [Description("Searches the game's objects by name (prefab names such as Beech1, rock4_forest, woodwall, stone_floor_2x2): what can be placed in a world, and the building pieces with their English name, tool (hammer for building pieces), size and cost. Every word must match. Spawn points (kind SpawnPoints: invisible, each spawns its creature where it stands, once or again after it dies) and nests (kind Nests) are also found by the creature they spawn (\"boar\" finds Spawner_Boar), with what they spawn; place them with place_pieces.")]
    public static Task<string> FindPrefabs(
        [Description("Words of the name, e.g. \"stone wall\", \"pine\", \"roof 26\".")] string query,
        [Description("Only building pieces (the hammer's): true; anything: false.")] bool piecesOnly = false,
        [Description("At most this many.")] int limit = 60,
        [Description("Items instead (what goes in chests: Coins, Amber, IronScrap, weapons…): true.")] bool items = false) => OnUi(() =>
    {
        string[] words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool Match(string s) => words.All(w => s.Contains(w, StringComparison.OrdinalIgnoreCase));
        if (items)
        {
            return ToJson(Terrain.PrefabCatalog.Items.Where(Match).Take(Math.Clamp(limit, 1, 500)).Select(i => new { item = i }));
        }
        var pieces = Terrain.PieceCatalog.Names
            .Select(n => (n, p: Terrain.PieceCatalog.Get(Save.StableHash.Of(n))))
            .Where(x => x.p is { Tool: not null } && (Match(x.n) || Match(Terrain.PieceCost.PieceName(x.n))))
            .Select(x => (object)new { prefab = x.n, name = Terrain.PieceCost.PieceName(x.n), tool = x.p!.Tool, size = SizeOf(x.p!), cost = Terrain.PieceCost.Of([x.n]).Describe(4, english: true) });
        var others = piecesOnly ? [] : Terrain.PrefabCatalog.Placeable
            .Where(i => Terrain.PieceCatalog.Get(Save.StableHash.Of(i.Name)) == null && (Match(i.Name) || Terrain.PrefabCatalog.SpawnOf(i.Name) is { } sp && Match(string.Join(" ", sp.Creatures))))
            .Select(i => Terrain.PrefabCatalog.SpawnOf(i.Name) is { } sp
                ? (object)new { prefab = i.Name, kind = TerrainEditor.App.ObjectKinds.Of(i.Name, false).ToString(), spawns = sp.Summary() }
                : new { prefab = i.Name, kind = TerrainEditor.App.ObjectKinds.Of(i.Name, false).ToString() });
        return ToJson(pieces.Concat(others).Take(Math.Clamp(limit, 1, 500)));
    });

    /// <summary>A piece's size (x, y, z) in metres, before turning.</summary>
    private static object SizeOf(Terrain.PieceCatalog.Info p) =>
        new { x = MathF.Round(p.MaxX - p.MinX, 2), y = MathF.Round(p.MaxY - p.MinY, 2), z = MathF.Round(p.MaxZ - p.MinZ, 2) };

    /// <summary>piece_info: name, tool, size, bounds, snap points, cost and station of building pieces.</summary>
    [McpServerTool(Name = "piece_info", ReadOnly = true, Title = "About building pieces")]
    [Description("For building pieces (prefab names): English name, tool, size and bounds from the piece's origin (x across, y up, z through, before turning), snap points (where pieces join, from the origin), cost and the station it needs. Use it to place pieces edge to edge.")]
    public static Task<string> PieceInfo([Description("Prefab names, e.g. [\"woodwall\", \"wood_floor\"].")] string[] prefabs) => OnUi(() => ToJson(prefabs.Select(n =>
    {
        var p = Terrain.PieceCatalog.Get(Save.StableHash.Of(n));
        if (p == null)
        {
            return (object)new { prefab = n, error = "not a building piece the editor knows (find_prefabs)" };
        }
        var cost = Terrain.PieceCost.Get(n);
        return new
        {
            prefab = n,
            name = Terrain.PieceCost.PieceName(n),
            tool = p.Tool,
            size = SizeOf(p),
            bounds = new { minX = p.MinX, maxX = p.MaxX, minY = p.MinY, maxY = p.MaxY, minZ = p.MinZ, maxZ = p.MaxZ },
            snaps = p.Snaps,
            cost = Terrain.PieceCost.Of([n]).Describe(8, english: true),
            station = cost?.Station,
        };
    })));

    /// <summary>building_guide: <see cref="ClaudeGuide.Building"/> and <see cref="ClaudeGuide.Shaping"/>.</summary>
    [McpServerTool(Name = "building_guide", ReadOnly = true, Title = "How to build and shape")]
    [Description("How Valheim's building works, from the game's data: the 2 m grid, pieces' origins and turns, roofs, and support (what holds and what falls); and how to shape ground. Read it before building.")]
    public static string BuildingGuide() => ClaudeGuide.Building + "\n\n" + ClaudeGuide.Shaping;

    /// <summary>script_reference: the script API's source.</summary>
    [McpServerTool(Name = "script_reference", ReadOnly = true, Title = "The script API")]
    [Description("The C# API run_script scripts use, as its source with comments: Area (bounds, points), Ground (Height, Set, Raise, Lower, Paint, Shape, Mountain, Biome), Objects (All, OfKind, Near, Place with tilts and snapping, Move, Remove, CanPlace), Noise, Rnd, Print. Read it before writing a script.")]
    public static string ScriptReference() => ScriptHost.ApiSource;

    // ---- Changing (pending changes only, one step of the history each).

    /// <summary>
    /// Why Claude cannot change the open area now, or null: nothing open, or live with Auto on (each change
    /// would go to the game at once, and Claude's must wait for the user).
    /// </summary>
    private string? CannotEdit()
    {
        if (_w.Session == null)
        {
            return "No area is open: open_area (a world) or open_workshop first.";
        }
        if (LiveWithAuto)
        {
            return "The open world is live with Auto on: every change would go to the game at once. Ask the user to turn Auto off; Claude's changes then wait for Apply live.";
        }
        return null;
    }

    /// <summary>Whether the open world is live with Auto on: every change would go to the game at once.</summary>
    private bool LiveWithAuto => _w.World is { IsLive: true } && _w.Settings.AutoApply;

    /// <summary>Why Claude may not run a script now: the user has not allowed it (Settings); null when it may.</summary>
    internal static string ScriptsRefused => "The user has not allowed Claude to run scripts (Settings → Claude → Claude may run scripts).";

    /// <summary>run_script: runs a C# script as the Script tool does, as one pending step (or only says what it would change).</summary>
    [McpServerTool(Name = "run_script", Title = "Run a script on the area")]
    [Description("Runs a C# script on the open area (or the Workshop's plot), as the editor's Script tool does: the script's statements are the body of a program using the API script_reference gives (Area, Ground, Objects, Noise, Rnd, Print). What it changes (ground heights, paint, objects placed or taken away) goes in as one step of the history, pending until the user saves; nothing changes when it has mistakes or fails (the errors say which line). Returns what it printed and what it changed. Ground stays within the game's ±8 m of the original unless Ground.NoLimit = true. Needs the user's leave, given once in Settings → Claude → Claude may run scripts (off by default: a script has the user's rights on their computer); refused otherwise, dry runs too.")]
    public Task<string> RunScript(
        [Description("The script: C# statements, e.g. \"Ground.Mountain(Area.CenterX, Area.CenterZ, height: 30, radius: 50);\"")] string code,
        [Description("A few words for the history (\"Claude: river valley\").")] string label = "Claude: script",
        [Description("true: only say what it would change (ground points, paint, objects placed and taken away), changing nothing. Use it before a large change.")] bool dryRun = false) => OnUi(async () =>
    {
        // A dry run compiles and runs the code too: the same leave. Said first: the user's own setting, which no
        // other change (opening an area, turning Auto off) would get round.
        if (!_w.Settings.ClaudeScripts)
        {
            return ScriptsRefused;
        }
        if (CannotEdit() is string why)
        {
            return why;
        }
        var r = await _w.RunScriptCode(code, label.StartsWith("Claude", StringComparison.Ordinal) ? label : "Claude: " + label, apply: !dryRun);
        _w.MessageText.Text = r.Message;
        return r.Output;
    });

    // ---- Common tasks, as scripts the Script tool runs (one pending step each).

    /// <summary>A number as C# source (invariant culture, up to three decimals).</summary>
    private static string N(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The paints paint_area and road take.</summary>
    private static readonly HashSet<string> PaintKinds = new(StringComparer.OrdinalIgnoreCase) { "dirt", "cultivated", "paved", "clear" };

    /// <summary>Runs a script written by a tool as one pending step labelled "Claude: label".</summary>
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

    /// <summary>flatten: levels a rectangle to a height (default: its average), with a cosine soft edge outside it.</summary>
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

    /// <summary>paint_area: paints a rectangle or a circle.</summary>
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

    /// <summary>road: levels the ground along a line of points to a smooth slope, blends its edges (3 m) and paints it.</summary>
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

    /// <summary>forest: plants a mix of prefabs on a jittered grid, off water and away from buildings, trees and rocks.</summary>
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

    /// <summary>undo: the last change of the area, anyone's, as Ctrl+Z.</summary>
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

    /// <summary>redo: the last change undone, as Ctrl+Y.</summary>
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

    /// <summary>A piece (or any object) to put down (place_pieces, and ClaudeBuilder's layouts).</summary>
    public sealed class PieceSpec
    {
        /// <summary>The prefab name.</summary>
        [Description("Prefab name, e.g. woodwall, wood_floor, stone_wall_2x1, wood_roof_45, Beech1.")]
        public string Prefab { get; set; } = "";

        /// <summary>World x (east), in metres.</summary>
        [Description("World x (east), metres.")]
        public float X { get; set; }

        /// <summary>World z (north), in metres.</summary>
        [Description("World z (north), metres.")]
        public float Z { get; set; }

        /// <summary>The height of the piece's origin; null: standing on the ground there, its lowest point touching it.</summary>
        [Description("Height of the piece's origin (optional: it stands on the ground there, its lowest point touching it). The Workshop's plot is at y = 34.")]
        public float? Y { get; set; }

        /// <summary>Turn round the vertical, in degrees (0: front faces north).</summary>
        [Description("Turn round the vertical, degrees (0: the piece's front faces north; 90: east).")]
        public float Yaw { get; set; }

        /// <summary>Tilt forward, in degrees.</summary>
        [Description("Tilt forward, degrees (usually 0).")]
        public float Pitch { get; set; }

        /// <summary>Tilt sideways, in degrees.</summary>
        [Description("Tilt sideways, degrees (usually 0).")]
        public float Roll { get; set; }

        /// <summary>Snap to the snap points of pieces already there (and earlier in the list) within 0.5 m.</summary>
        [Description("Snap to the snap points of pieces already there (and of those before it in the list) within 0.5 m, as the game's hammer does (default: true).")]
        public bool Snap { get; set; } = true;
    }

    /// <summary>place_pieces: puts down pieces and objects as one pending step (see <see cref="Place"/>).</summary>
    [McpServerTool(Name = "place_pieces", Title = "Put down pieces and objects")]
    [Description("Puts down building pieces (and any other objects: trees, rocks…) in the open area or on the Workshop's plot, all as one step of the history (pending). Pieces snap to the snap points of pieces already there within 0.5 m, as the game's hammer does, so place them where they should join and let the snap make them meet; piece_info gives sizes and snap points. Returns where each went (after snapping) and its id. Check the result with support_check (Workshop) and screenshot.")]
    public Task<string> PlacePieces(
        [Description("The pieces, in the order they are put down.")] PieceSpec[] pieces,
        [Description("A few words for the history.")] string label = "Claude: building") => OnUi(() => Place(pieces, label));

    /// <summary>
    /// The pieces put down as one step (on the window's thread): where each went and its id, or why not. Pieces
    /// outside the area or unknown to the game are left out and named in the problems.
    /// </summary>
    private string Place(IReadOnlyList<PieceSpec> pieces, string label)
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        var session = _w.Session!;
        var scene = session.Scene;
        int reads = scene.Reads;
        float minX = scene.X0 * 64f - 32f, minZ = scene.Z0 * 64f - 32f, maxX = minX + scene.ZonesX * 64f, maxZ = minZ + scene.ZonesZ * 64f;
        var placed = new List<Editing.Hammer.Placed>();
        lock (scene.Things)
        {
            for (int i = 0; i < scene.Things.Count; i++)
            {
                var t = scene.Things[i];
                if (!t.Gone && _w.NameOfPrefab(t.Prefab) is string n && Editing.Hammer.Get(n) != null)
                {
                    placed.Add(new(i, n, t.Position, TerrainEditor.App.BlueprintFormats.FromEuler(t.Rotation)));
                }
            }
        }
        var adds = new List<(Editing.NewObject, bool)>();
        var report = new List<object>();
        var problems = new List<string>();
        foreach (var (p, k) in pieces.Select((p, k) => (p, k)))
        {
            int hash = Save.StableHash.Of(p.Prefab);
            var piece = Terrain.PieceCatalog.Get(hash);
            if (piece == null && Terrain.PrefabCatalog.Get(hash) == null)
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
            float y = p.Y ?? GroundAt(scene, p.X, p.Z) - Editing.Hammer.Bottom(p.Prefab, q);
            var pos = new System.Numerics.Vector3(p.X, y, p.Z);
            int? to = null;
            if (p.Snap)
            {
                (pos, to) = Editing.Hammer.Snap(p.Prefab, q, pos, placed);
            }
            placed.Add(new(-1 - k, p.Prefab, pos, q));
            adds.Add((new Editing.NewObject(0, hash, pos, euler, 0), piece?.Tool != null));
            report.Add(new { prefab = p.Prefab, x = MathF.Round(pos.X, 3), y = MathF.Round(pos.Y, 3), z = MathF.Round(pos.Z, 3), snapped = to != null });
        }
        if (adds.Count == 0)
        {
            return "Nothing put down: " + string.Join("; ", problems);
        }
        var ids = session.Commit(label.StartsWith("Claude", StringComparison.Ordinal) ? label : "Claude: " + label, null, [], adds);
        _w.AfterClaudeEdit($"Claude put down {adds.Count} piece(s); Ctrl+Z takes them back.");
        GaveIds(scene, reads);
        return ToJson(new { placed = report.Select((r, i) => new { id = i < ids.Count ? ids[i] : -1, piece = r }), problems, pending = Pending() });
    }

    // ---- Building in larger pieces (ClaudeBuilder: floors, walls, roofs over a rectangle).

    /// <summary>A door for build_walls.</summary>
    public sealed class DoorSpec
    {
        /// <summary>Which wall: north, south, east or west.</summary>
        [Description("Which wall: north, south, east or west.")]
        public string Side { get; set; } = "south";

        /// <summary>Where along it, in world metres: x on a north or south wall, z on an east or west one.</summary>
        [Description("Where along it: x for a north or south wall, z for an east or west one (a 2 m door at the 2 m of wall holding that point).")]
        public float At { get; set; }
    }

    /// <summary>The highest ground under a rectangle (every metre); edgesOnly: only within a metre of its edges.</summary>
    private static float HighestGround(WorldScene scene, float x0, float z0, float x1, float z1, bool edgesOnly = false)
    {
        (float lx, float hx, float lz, float hz) = (MathF.Min(x0, x1), MathF.Max(x0, x1), MathF.Min(z0, z1), MathF.Max(z0, z1));
        // Whole-metre steps counted in integers: adding 1 to a float that large would not move it on.
        int nx = (int)MathF.Floor(hx - lx), nz = (int)MathF.Floor(hz - lz);
        float best = float.MinValue;
        for (int i = 0; i <= nx; i++)
        {
            float x = lx + i;
            for (int j = 0; j <= nz; j++)
            {
                float z = lz + j;
                bool edge = i < 1 || hx - x < 1 || j < 1 || hz - z < 1;
                if (!edgesOnly || edge)
                {
                    best = MathF.Max(best, GroundAt(scene, x, z));
                }
            }
        }
        return best;
    }

    /// <summary>
    /// The highest top of the building pieces within a rectangle (0.6 m around it counts; whose names hold word, when
    /// given), or null.
    /// </summary>
    private float? HighestPieceTop(WorldScene scene, float x0, float z0, float x1, float z1, string? word = null)
    {
        float? best = null;
        lock (scene.Things)
        {
            foreach (var t in scene.Things)
            {
                if (t.Gone || !t.Piece || _w.NameOfPrefab(t.Prefab) is not string n || (word != null && !n.Contains(word, StringComparison.OrdinalIgnoreCase))
                    || Terrain.PieceCatalog.Get(t.Prefab) is not { } info)
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

    /// <summary>
    /// Puts down a layout worked out from the open scene over a rectangle; refused when the rectangle goes outside the
    /// area, and a layout's <see cref="ArgumentException"/> (too many pieces among them) is the answer.
    /// </summary>
    /// <param name="x0">One corner, x.</param>
    /// <param name="z0">One corner, z.</param>
    /// <param name="x1">The opposite corner, x.</param>
    /// <param name="z1">The opposite corner, z.</param>
    /// <param name="layout">The pieces, from the open scene.</param>
    /// <param name="label">A few words for the history.</param>
    private Task<string> Build(float x0, float z0, float x1, float z1, Func<WorldScene, List<PieceSpec>> layout, string label) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        var (minX, minZ, maxX, maxZ) = Bounds(_w.Session!.Scene);
        // Written so that a coordinate that is not a number is outside too.
        if (!(MathF.Min(x0, x1) >= minX && MathF.Max(x0, x1) <= maxX && MathF.Min(z0, z1) >= minZ && MathF.Max(z0, z1) <= maxZ))
        {
            return $"Nothing built: the rectangle (x {x0} to {x1}, z {z0} to {z1}) goes outside the area (x {minX} to {maxX}, z {minZ} to {maxZ}). Open a larger area (open_area) or build within it.";
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

    /// <summary>build_floor: <see cref="ClaudeBuilder.Floor"/>, its top on the highest ground under it by default.</summary>
    [McpServerTool(Name = "build_floor", Title = "Build a floor")]
    [Description("Lays a floor of 2 x 2 m tiles (wood or stone) over a rectangle, its top at y (default: the highest ground under it, so that it rests on the ground). One pending step; the rectangle must be within the open area, and at most 5000 pieces are laid at once. Sizes that are not multiples of 2 m overhang a little.")]
    public Task<string> BuildFloor(
        [Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
        [Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
        [Description("wood or stone.")] string material = "wood",
        [Description("Height of its top (optional).")] float? y = null) =>
        Build(x0, z0, x1, z1, scene =>
        {
            // Too many tiles is refused before the ground under them is read.
            ClaudeBuilder.CheckFloor(x0, z0, x1, z1);
            return ClaudeBuilder.Floor(x0, z0, x1, z1, y ?? HighestGround(scene, x0, z0, x1, z1), material);
        }, $"floor of {material}");

    /// <summary>
    /// build_walls: <see cref="ClaudeBuilder.Walls"/>, from the top of a floor in the rectangle by default, else the
    /// highest ground on its edges.
    /// </summary>
    [McpServerTool(Name = "build_walls", Title = "Build walls")]
    [Description("Builds walls on the four edges of a rectangle (the walls' middle lines on its edges), from the bottom up to a height: wood (2 m rows) or stone (1 m rows); doors are 2 m wood doors in the walls at the given points. Bottom: the top of a floor already in the rectangle, else the highest ground on its edges. One pending step; the rectangle must be within the open area, and at most 5000 pieces are laid at once. Use the floor's own rectangle so that they meet.")]
    public Task<string> BuildWalls(
        [Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
        [Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
        [Description("Height, metres (2 or 4 for one storey).")] float height = 4,
        [Description("wood or stone.")] string material = "wood",
        [Description("Doors (optional).")] DoorSpec[]? doors = null,
        [Description("Height of the walls' bottom (optional).")] float? y = null) =>
        Build(x0, z0, x1, z1, scene =>
        {
            // Too many pieces is refused before the ground under them is read.
            ClaudeBuilder.CheckWalls(x0, z0, x1, z1, height, material);
            return ClaudeBuilder.Walls(x0, z0, x1, z1, y ?? HighestPieceTop(scene, x0, z0, x1, z1, "floor") ?? HighestGround(scene, x0, z0, x1, z1, edgesOnly: true),
                height, material, [.. (doors ?? []).Select(d => new ClaudeBuilder.Door(d.Side, d.At))]);
        }, $"walls of {material}");

    /// <summary>build_roof: <see cref="ClaudeBuilder.Roof"/>, its eave at the top of the pieces in the rectangle by default.</summary>
    [McpServerTool(Name = "build_roof", Title = "Build a roof")]
    [Description("Builds a gable roof over a rectangle (the walls' lines), its ridge along the longer side: thatch or shingle, 26 or 45 degrees, its underside meeting the wall lines at the eave height (default: the top of the building pieces in the rectangle, i.e. the walls), overhanging up to 2 m; the two gable ends closed with sloped walls (gables: true). One pending step; the rectangle must be within the open area, and at most 5000 pieces are laid at once. Check it with support_check and screenshot (views: 4).")]
    public Task<string> BuildRoof(
        [Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
        [Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
        [Description("26 or 45 degrees.")] int angle = 45,
        [Description("thatch or shingle.")] string material = "thatch",
        [Description("Close the gable ends with sloped walls (default true).")] bool gables = true,
        [Description("Height of the walls' tops (optional).")] float? eave = null) =>
        Build(x0, z0, x1, z1, scene => ClaudeBuilder.Roof(x0, z0, x1, z1,
            eave ?? HighestPieceTop(scene, x0, z0, x1, z1) ?? throw new ArgumentException("No walls under the roof: build the walls first, or give eave."),
            angle, material, gables), $"{material} roof");

    /// <summary>The ground's height at a world point of the area (its nearest grid point, clamped to the area).</summary>
    private static float GroundAt(WorldScene scene, float x, float z)
    {
        int i = Math.Clamp((int)MathF.Round(x - scene.Cx + (scene.W - 1) / 2f), 0, scene.W - 1);
        int j = Math.Clamp((int)MathF.Round(z - scene.Cz + (scene.H - 1) / 2f), 0, scene.H - 1);
        return scene.Heights[j * scene.W + i];
    }

    /// <summary>remove_objects: takes objects away by id, as one pending step.</summary>
    [McpServerTool(Name = "remove_objects", Title = "Take objects away")]
    [Description("Takes objects of the open area (or the plot) away, by their ids (list_objects, place_pieces), as one step of the history (pending). A part of a group a mod links into one (a ship and the pieces built on it) takes its whole group away.")]
    public Task<string> RemoveObjects([Description("Their ids.")] int[] ids) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        var session = _w.Session!;
        if (StaleIds(session.Scene) is string stale)
        {
            return stale;
        }
        var things = session.Scene.Things;
        int[] asked = [.. ids.Distinct()];
        var given = asked.Where(i => i >= 0 && i < things.Count && !things[i].Gone).ToList();
        if (given.Count == 0)
        {
            return "None of those ids is an object of the area.";
        }
        // A linked group's part never goes alone (ObjectLinks: a ship): the whole group does.
        var (ok, _, groups, noun) = session.WithLinked(given);
        session.Commit($"Claude: took {ok.Count} away", null, ok, []);
        _w.AfterClaudeEdit($"Claude took {ok.Count} object(s) away; Ctrl+Z puts them back.");
        return $"Took {ok.Count} away{(given.Count < asked.Length ? $" ({asked.Length - given.Count} id(s) were not objects of the area)" : "")}"
            + $"{(groups > 0 ? $", with {groups} whole {noun}(s)" : "")}. Now pending: {Pending()}";
    });

    /// <summary>support_check: the Workshop's support check, the pieces that would fall and the weakest still standing.</summary>
    [McpServerTool(Name = "support_check", ReadOnly = true, Title = "Would it stand?")]
    [Description("The Workshop's support check, as the game computes it: for the building pieces on the plot, which would fall (nothing holds them up enough) and the weakest ones still standing (support 0 about to fall, 1 strong). Pieces on the ground hold best; support drops with each piece away from it (wood less far than stone, iron further).")]
    public Task<string> SupportCheck() => OnUi(() =>
    {
        if (!_w.InWorkshop)
        {
            return "The support check is the Workshop's: open_workshop first.";
        }
        // The read the pieces' indices are of, taken before they are worked out.
        int reads = _w.Session?.Scene.Reads ?? 0;
        _w.SupportBox.IsChecked = true;
        _w.RefreshSupport();
        if (_w.LastSupport is not { } r || _w.Session?.Scene is not { } scene)
        {
            return "No building piece on the plot.";
        }
        var things = _w.LastSupportThings;
        object Piece(int k) => new
        {
            id = things[k],
            name = _w.NameOfPrefab(scene.Things[things[k]].Prefab),
            x = MathF.Round(scene.Things[things[k]].Position.X, 2),
            y = MathF.Round(scene.Things[things[k]].Position.Y, 2),
            z = MathF.Round(scene.Things[things[k]].Position.Z, 2),
            support = MathF.Round(r.Colour[k], 2),
        };
        var falling = r.Falls.Take(60).Select(Piece).ToList();
        // Support: 0 (red, about to fall) to 1 (green); pieces on the ground (light blue in the editor) hold best and are left out.
        var weakest = Enumerable.Range(0, things.Count).Where(k => !r.Free[k] && !r.Breaks[k] && r.Colour[k] >= 0).OrderBy(k => r.Colour[k]).Take(10).Select(Piece).ToList();
        if (falling.Count > 0 || weakest.Count > 0)
        {
            GaveIds(scene, reads);
        }
        return ToJson(new
        {
            pieces = things.Count,
            wouldFall = r.Breaking,
            falling,
            weakest,
            onTheGround = Enumerable.Range(0, things.Count).Count(k => r.Colour[k] < 0),
        });
    });

    /// <summary>What a container (or a stand) gets: one stack.</summary>
    public sealed class ItemSpec
    {
        /// <summary>The item's prefab name.</summary>
        [Description("The item's prefab name, e.g. Coins, Amber, Ruby, IronScrap, SwordIron (find_prefabs with items: true).")]
        public string Item { get; set; } = "";

        /// <summary>How many in the stack.</summary>
        [Description("How many in the stack (default 1).")]
        public int Stack { get; set; } = 1;

        /// <summary>Quality (upgrade level).</summary>
        [Description("Quality (upgrade level) for weapons and armour (default 1).")]
        public int Quality { get; set; } = 1;

        /// <summary>Variant: a shield's or banner's style, from 0.</summary>
        [Description("Variant: a shield's or banner's style, from 0 (default 0).")]
        public int Variant { get; set; }
    }

    /// <summary>
    /// set_contents: a container's items, a stand's item or armour (with orientation or pose), a sign's text or a
    /// creature's stars, as the inspector changes them; the object is replaced by a changed copy (a new id).
    /// </summary>
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
        if (StaleIds(s.Scene) is string stale)
        {
            return stale;
        }
        var things = s.Scene.Things;
        if (id < 0 || id >= things.Count || things[id].Gone)
        {
            return "No object with that id in the area.";
        }
        var t = things[id];
        var info = Terrain.PrefabCatalog.Details(t.Prefab);
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
                    set.AddRange(TerrainEditor.App.StandData.ForArmourStand(t.Prefab, [.. items.Select(i => new TerrainEditor.App.StandData.Held(i.Item, i.Quality, i.Variant))], pose));
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
            inv = [.. items.Select((it, i) => new TerrainEditor.App.ItemUpload(it.Item, null, Math.Max(1, it.Stack), Math.Max(1, it.Quality), X: i % box.ContainerW, Y: i / box.ContainerW, Variant: Math.Max(0, it.Variant)))];
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
            if (!Terrain.PrefabCatalog.IsCreature(t.Prefab))
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
        var notes = new List<string>();
        var (copy, problem) = Rewrite(id, set, inv, $"Claude: filled {name}", notes);
        if (copy is not int newId)
        {
            return problem!;
        }
        _w.AfterClaudeEdit($"Claude changed what {name} holds; Ctrl+Z puts it back.");
        return $"Changed {name}; its id is now {newId}." + string.Concat(notes.Select(n => " " + n + "."));
    });

    /// <summary>open_workshop: the blank plot, optionally with a library blueprint; refused while there are unsaved changes.</summary>
    [McpServerTool(Name = "open_workshop", Title = "Open the Workshop")]
    [Description("Opens the Workshop: a blank 192 m plot (ground at y = 34, its middle at x = 0, z = 0) to build a blueprint on, optionally with a blueprint of the library on it (list_blueprints): this is how a blueprint is loaded. Refused while a world or the Workshop has changes not saved (save_blueprint saves the Workshop's building).")]
    public Task<string> OpenWorkshop([Description("A blueprint's name from list_blueprints (optional: an empty plot).")] string? blueprint = null) =>
        WithLibraryFile(blueprint, found => OnUi(async () =>
    {
        if (Unsaved() is string why)
        {
            return why;
        }
        string? path = null;
        if (!string.IsNullOrWhiteSpace(blueprint))
        {
            path = found;
            if (path == null)
            {
                return $"No blueprint \"{blueprint}\" in the library (list_blueprints).";
            }
        }
        await _w.OpenWorkshop(path);
        return ToJson(StateOf(_w));
    }));

    /// <summary>
    /// The library file of a blueprint named as list_blueprints names it (its #Name: header, or its file's name when
    /// it has none), whatever its file is called (the game's own files, downloaded ones and numbered copies are not
    /// named after their header): of the entries with that name, the one in the file named after it when there is
    /// one (as the editor saves them: "Tower" is Tower.blueprint, not "Tower (2)"), else the first. With no entry
    /// of that name, a file named after it. Null for none.
    /// </summary>
    /// <param name="folder">The library's folder.</param>
    /// <param name="name">The blueprint's name.</param>
    private static string? BlueprintPath(string folder, string name)
    {
        string file = TerrainEditor.App.Homestead.FileName(name);
        var entries = TerrainEditor.App.Homestead.List(folder).Where(e => e.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if ((entries.Find(e => string.Equals(Path.GetFileName(e.Path), file, StringComparison.OrdinalIgnoreCase)) ?? entries.FirstOrDefault()) is { } entry)
        {
            return entry.Path;
        }
        string path = Path.Combine(folder, file);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Finds a library blueprint's file (<see cref="BlueprintPath"/>) away from the window's thread, as it reads every
    /// blueprint of the library, then answers with <paramref name="then"/> given it (null: none, or no name given).
    /// </summary>
    /// <param name="name">The blueprint's name, or null or blank for none.</param>
    /// <param name="then">The tool's work with the file found.</param>
    private async Task<string> WithLibraryFile(string? name, Func<string?, Task<string>> then)
    {
        string folder = await OnUi(() => _w.Blueprints.Status.Folder);
        string? path = string.IsNullOrWhiteSpace(name) ? null : await Task.Run(() => BlueprintPath(folder, name));
        return await then(path);
    }

    /// <summary>save_blueprint: the Workshop's building saved as a blueprint of the library (MainWindow.SaveWorkshopAs).</summary>
    [McpServerTool(Name = "save_blueprint", Title = "Save the Workshop's building as a blueprint")]
    [Description("Saves the building on the Workshop's plot as a blueprint of the user's library (Homestead's), as the Workshop's Save blueprint does: its name, a description and tags, with what its objects hold (chest contents, stands' items) and a picture. After it, the Workshop holds nothing unsaved, so another blueprint can be opened (open_workshop). Refused when pieces would fall in game (support_check; evenIfFalling saves anyway) and when another blueprint of that name exists: replacing one (overwrite) is the user's call, ask first.")]
    public Task<string> SaveBlueprint(
        [Description("The blueprint's name (also its file's).")] string name,
        [Description("A few words about it, shown in the library (optional).")] string description = "",
        [Description("Words to find it by (optional), e.g. [\"house\", \"log\"].")] string[]? tags = null,
        [Description("Replace a blueprint of that name (only when the user said so; default false).")] bool overwrite = false,
        [Description("Save even if pieces would fall in game (default false).")] bool evenIfFalling = false) => OnUi(() =>
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Give the blueprint a name.";
        }
        var details = new TerrainEditor.App.Homestead.Details(name.Trim(), description.Trim(), [.. (tags ?? []).Select(t => t.Trim()).Where(t => t.Length > 0)]);
        return _w.SaveWorkshopAs(details, overwrite, evenIfFalling) is string problem ? problem : _w.MessageText.Text + " " + ToJson(StateOf(_w));
    });

    /// <summary>list_blueprints: Homestead's library, filtered by words of the name, description or tags.</summary>
    [McpServerTool(Name = "list_blueprints", ReadOnly = true, Title = "The blueprint library")]
    [Description("The user's blueprints (Homestead's library): name, pieces, cost, description, tags. Open one in the Workshop (open_workshop) or add it to the plot (add_blueprint).")]
    public Task<string> ListBlueprints([Description("Words to find in the name, description or tags (optional).")] string? query = null) => OnUi(() =>
    {
        string[] words = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return ToJson(TerrainEditor.App.Homestead.List(_w.Blueprints.Status.Folder)
            .Where(e => words.All(w => e.Name.Contains(w, StringComparison.OrdinalIgnoreCase) || e.Description.Contains(w, StringComparison.OrdinalIgnoreCase) || e.Tags.Any(t => t.Contains(w, StringComparison.OrdinalIgnoreCase))))
            .Select(e => new { name = e.Name, pieces = e.Pieces, cost = Terrain.PieceCost.Of(e.Kinds).Describe(4, english: true), description = e.Description, tags = e.Tags }));
    });

    /// <summary>
    /// add_blueprint: a library blueprint onto the Workshop's plot, with what is there, or into the open area of a
    /// world, its lowest piece on the ground at its middle (or at y); taken back when a piece would leave the area.
    /// </summary>
    [McpServerTool(Name = "add_blueprint", Title = "Add a blueprint")]
    [Description("Adds a blueprint of the library (list_blueprints) to the Workshop's plot or to the open area of a world, with what is there: centred on x, z (default: the plot's or the area's middle), turned by turn degrees (as yaw: 90 turns north to east), its lowest piece at height y (default: the ground at its middle; level the ground first with flatten for a building on a slope). One pending step; refused when a piece would be outside the area. Kinds the game does not know (mods) are left out and named.")]
    public Task<string> AddBlueprint(
        [Description("The blueprint's name (list_blueprints).")] string blueprint,
        [Description("World x of its middle (optional).")] float? x = null,
        [Description("World z of its middle (optional).")] float? z = null,
        [Description("Degrees to turn it round its middle (default 0).")] float turn = 0,
        [Description("In a world: the height of its lowest piece (optional: the ground at its middle).")] float? y = null) =>
        WithLibraryFile(blueprint, found => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        if (found is not string path)
        {
            return $"No blueprint \"{blueprint}\" in the library (list_blueprints).";
        }
        string label = $"Claude: added {blueprint}";
        if (_w.InWorkshop)
        {
            _w.AddToWorkshop(path, x is float ax && z is float az ? new System.Numerics.Vector2(ax, az) : null, turn, label);
            return _w.MessageText.Text + " Now pending: " + Pending();
        }
        var s = _w.Session!;
        var scene = s.Scene;
        int reads = scene.Reads;
        float cx = x ?? scene.Cx, cz = z ?? scene.Cz;
        var (minX, minZ, maxX, maxZ) = Bounds(scene);
        if (cx < minX || cx > maxX || cz < minZ || cz > maxZ)
        {
            return $"({cx}, {cz}) is outside the open area (x {minX} to {maxX}, z {minZ} to {maxZ}): open_area there first.";
        }
        // Worked out before anything is put down: a refusal leaves the history alone (a step committed and
        // undone would clear what the user could redo and leave the refused blueprint there instead).
        Workshop.Planned plan;
        try
        {
            plan = Workshop.Plan(s, path, null, new System.Numerics.Vector2(cx, cz), turn, y ?? GroundAt(scene, cx, cz));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or JsonException)
        {
            return "Could not read that blueprint: " + ex.Message;
        }
        if (plan.Adds.Count == 0)
        {
            return "Nothing added: the blueprint has no piece this world can have.";
        }
        int outside = plan.Adds.Count(a => a.Object.Position.X < minX || a.Object.Position.X > maxX || a.Object.Position.Z < minZ || a.Object.Position.Z > maxZ);
        if (outside > 0)
        {
            return $"Nothing added: {outside} piece(s) would be outside the area (x {minX} to {maxX}, z {minZ} to {maxZ}). Open a larger area (open_area) or put it further in.";
        }
        int before = scene.Things.Count;
        var added = Workshop.Put(s, plan, label);
        GaveIds(scene, reads);
        _w.AfterClaudeEdit($"Claude added “{blueprint}”: {added.Placed} piece(s); Ctrl+Z takes it back.");
        return $"Added {added.Placed} piece(s) of \"{blueprint}\", ids {before} to {scene.Things.Count - 1}."
            + (added.Unknown.Count > 0 ? $" Left out (unknown kinds): {string.Join(", ", added.Unknown.Take(10))}." : "")
            + " Now pending: " + Pending();
    }));

    /// <summary>
    /// generate_dungeon: a dungeon from the Dungeon panel's settings (clamped to their ranges), onto the Workshop's plot
    /// or 5000 m above a point of the open world, behind the entrance of its kind there (a portal pair when it has none).
    /// </summary>
    [McpServerTool(Name = "generate_dungeon", Title = "Generate a dungeon")]
    [Description("Generates a dungeon, as the Dungeon panel does, and puts it on the Workshop's plot (with what is there) or, in a world, 5000 m above the point x, z (where the game keeps its dungeons), behind the entrance of its kind on the ground there (its biome's dungeon location: a burial chamber's mound, a Frost Cave's mouth; a linked portal pair when the biome has none). The view stays at the entrance. One step of the history (pending).")]
    public async Task<string> GenerateDungeon(
        [Description("Its biome: Meadows, Black Forest, Swamp, Mountain, Plains, Mistlands, Ashlands, Deep North.")] string biome = "Black Forest",
        [Description("Its style (optional: the biome's): Crypt, Catacombs, Temple, Fortress, Prison, Dvergr hold, Goblin warren, Ruins.")] string? style = null,
        [Description("Its wall material (optional: the biome's).")] string? walls = null,
        [Description("Rooms per level: 1 small to 4 huge.")] int size = 2,
        [Description("Levels, 1 to 16.")] int levels = 2,
        [Description("Monsters: 0 none to 3 (1 usual).")] float monsters = 1,
        [Description("Chests and treasure: 0 none to 2 (1 usual).")] float loot = 1,
        [Description("Light: 0 dark, 1 dim, 2 bright.")] int light = 1,
        [Description("Furniture: 0 bare, 1 furnished, 2 rich.")] int decor = 1,
        [Description("Any number: each makes another dungeon with the same choices.")] int seed = 1,
        [Description("Its name, on a sign at the entrance (optional: made up).")] string? name = null,
        [Description("In a world: x of the point of the open area it goes above (optional: the view's middle).")] float? x = null,
        [Description("In a world: z of that point.")] float? z = null)
    {
        var settings = new Editing.DungeonGen.Settings(Biome: biome, Style: style, Walls: walls, Size: Math.Clamp(size, 1, 4), Levels: Math.Clamp(levels, 1, Editing.DungeonGen.MaxLevels),
            Monsters: Math.Clamp(monsters, 0, 3), Loot: Math.Clamp(loot, 0, 2), Light: Math.Clamp(light, 0, 2), Decor: Math.Clamp(decor, 0, 2), Seed: seed, Name: name);
        var (why, opened) = await OnUi(() => (CannotEdit(), _w.Session));
        if (why != null)
        {
            return why;
        }
        var result = await Task.Run(() => Editing.DungeonGen.Make(settings));
        return await WithCamera(async () =>
        {
            // The map shown or another area opened while it was made: it goes nowhere.
            if (_w.Session != opened || opened == null)
            {
                return "The area was closed (or another opened) while the dungeon was made: nothing was placed.";
            }
            // Or Auto turned on meanwhile: what forbade editing before the dungeon was made forbids placing it.
            if (CannotEdit() is string problem)
            {
                return problem;
            }
            // One of Claude's steps ("Claude:", as Take back Claude's changes finds them), and no file in the user's
            // blueprint Library.
            string label = $"Claude: generated dungeon {result.Name}";
            if (_w.InWorkshop)
            {
                _w.AddGeneratedToPlot(settings, result, label);
            }
            else
            {
                if (x is float px && z is float pz)
                {
                    // Above a point of the open area (its ground is the area's).
                    var sc = opened.Scene;
                    float minX = sc.X0 * 64f - 32f, minZ = sc.Z0 * 64f - 32f;
                    if (px < minX || pz < minZ || px > minX + sc.ZonesX * 64f || pz > minZ + sc.ZonesZ * 64f)
                    {
                        return $"({px}, {pz}) is outside the open area (x {minX} to {minX + sc.ZonesX * 64f}, z {minZ} to {minZ + sc.ZonesZ * 64f}): open_area there first.";
                    }
                    _w.View.Orbit(px, pz, 45, 55, 120);
                }
                _w.PlaceGenerated(settings, result, label);
            }
            return $"{string.Join(" ", result.Notes)} {_w.MessageText.Text} Now pending: {Pending()}";
        });
    }

    /// <summary>The changes not saved yet, in words.</summary>
    private string Pending() => _w.World is { } w
        ? w.Pending is (0, 0, 0, 0) ? "nothing." : $"{w.Pending.Zones} zone(s) of ground, {w.Pending.Added} object(s) added, {w.Pending.Deleted} taken away."
        : _w.Session is { } s ? $"{Workshop.Pieces(s.Scene)} building piece(s) on the plot, not saved as a blueprint." : "nothing.";

    /// <summary>Why a world cannot be left now (changes the user has not saved), or null.</summary>
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

    /// <summary>
    /// What the window shows, for editor_state and other answers: the page (start, map, area or workshop), the world,
    /// the area's bounds in world metres, what is pending, whether undo is possible, and the last message.
    /// </summary>
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
                maxX = scene.X0 * 64f - 32f + scene.ZonesX * 64f,
                maxZ = scene.Z0 * 64f - 32f + scene.ZonesZ * 64f,
                water = scene.Water,
                objects = scene.Things.Count(t => !t.Gone),
            },
            pending = w.World is { } pw ? new { zones = pw.Pending.Zones, added = pw.Pending.Added, deleted = pw.Pending.Deleted } : null,
            canUndo = s?.CanUndo ?? false,
            message = w.MessageText.Text ?? "",
        };
    }
}
