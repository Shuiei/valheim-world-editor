using System.ComponentModel;
using System.Numerics;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's tools for the whole world and for copies: searching every object of the open world (by kind, items in
/// containers, texts), the world's locations (the start, traders, bosses' altars, dungeons, villages…), and copying
/// objects of the open area.
/// </summary>
public sealed partial class ClaudeTools
{
    // ---- The whole world.

    /// <summary>The most hits search_world gives.</summary>
    private const int MostHits = 500;

    /// <summary>search_world: WorldSearch over the open world, with the open area's id of each hit inside it.</summary>
    [McpServerTool(Name = "search_world", ReadOnly = true, Title = "Search the whole world")]
    [Description("Searches every object of the open world, as the map's Search does (also where no area is open): by kind (what: kinds; e.g. \"portal\", \"Beech\", \"piece_chest\"), by item in containers (what: items; e.g. \"Coins\", \"Surtling\"), or by text (what: texts; signs, portal tags, ward and tombstone names). Gives how many were found by name and where each is (world x, y, z; nearest a point first when x, z are given), with its id in the open area (areaId) when it is in it. Open the area around a hit (open_area) to work on it.")]
    public async Task<string> SearchWorld(
        [Description("What to look for: part of a name, an item or a text (case does not matter).")] string query,
        [Description("kinds (objects by name), items (in containers) or texts (default kinds).")] string what = "kinds",
        [Description("World x of a point: nearest hits first (optional, with z).")] float? x = null,
        [Description("World z of that point.")] float? z = null,
        [Description("At most this many hits (default 50, up to 500).")] int limit = 50)
    {
        if (what is not ("kinds" or "items" or "texts"))
        {
            return "what is kinds, items or texts.";
        }
        var world = await OnUi(() => _w.World);
        if (world == null)
        {
            return "Open a world first (open_world).";
        }
        TerrainEditor.App.WorldSearch.Result res;
        try
        {
            res = await Task.Run(() => TerrainEditor.App.WorldSearch.Search(world.World, world.Edits, query, what));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "The search failed: " + ex.Message;
        }
        return await OnUi(() =>
        {
            // The open area's things by world id, to give the hits there their ids.
            var inArea = new Dictionary<int, int>();
            if (_w.Session?.Scene is { } scene && !_w.InWorkshop)
            {
                lock (scene.Things)
                {
                    for (int i = 0; i < scene.Things.Count; i++)
                    {
                        if (!scene.Things[i].Gone)
                        {
                            inArea.TryAdd(scene.Things[i].Id, i);
                        }
                    }
                }
            }
            var hits = res.Hits.AsEnumerable();
            if (x is float px && z is float pz)
            {
                hits = hits.OrderBy(h => (h.X - px) * (h.X - px) + (h.Z - pz) * (h.Z - pz));
            }
            return ToJson(new
            {
                found = res.Total,
                byName = res.Counts.OrderByDescending(c => c.Value).Take(40).Select(c => new { name = c.Key, count = c.Value }),
                hits = hits.Take(Math.Clamp(limit, 1, MostHits)).Select(h => new
                {
                    name = h.Name,
                    x = MathF.Round(h.X, 1),
                    y = MathF.Round(h.Y, 1),
                    z = MathF.Round(h.Z, 1),
                    match = h.Match,
                    areaId = inArea.TryGetValue(h.Id, out int i) ? i : (int?)null,
                }),
                note = res.Truncated ? $"Only the first {res.Hits.Count} hits were kept: ask for something narrower." : null,
            });
        });
    }

    /// <summary>
    /// What the world's locations are called by players, by the game's location name: the start, traders and bosses'
    /// altars (SeedPreview.Landmarks), and the dungeons' entrances (SeedPreview.Dungeons).
    /// </summary>
    internal static readonly Dictionary<string, (string What, string Kind)> Known = BuildKnown();

    /// <summary>Builds <see cref="Known"/>.</summary>
    private static Dictionary<string, (string What, string Kind)> BuildKnown()
    {
        var known = new Dictionary<string, (string What, string Kind)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (prefab, name, kind) in TerrainEditor.App.SeedPreview.Landmarks)
        {
            known[prefab] = (name, kind);
        }
        foreach (var (name, _, prefabs) in TerrainEditor.App.SeedPreview.Dungeons)
        {
            foreach (string p in prefabs)
            {
                known.TryAdd(p, (name, "dungeon"));
            }
        }
        return known;
    }

    /// <summary>The game's location names by hash: the location rules read from the game, and the prefab catalogue.</summary>
    internal static string? LocationName(int hash, Dictionary<int, string> rules) =>
        rules.TryGetValue(hash, out string? n) ? n : Terrain.PrefabCatalog.NameOf(hash);

    /// <summary>list_locations: the world's locations from its zone file (or, without one, those of zones generated).</summary>
    [McpServerTool(Name = "list_locations", ReadOnly = true, Title = "The world's locations")]
    [Description("The locations the game laid out in the open world: the start, the traders (Haldor, Hildir, the Bog Witch), the bosses' altars (Eikthyr, the Elder, Bonemass, Moder, Yagluth, the Queen, Fader), dungeon entrances (burial chambers, troll caves, sunken crypts, frost caves, infested mines…), villages, towers, runestones and the rest: game name, what players call it, kind (start, trader, boss, dungeon, other), world x, y, z, and whether its zone is generated yet (visited in game). Filter by words of the name, what or kind (\"boss\", \"trader\", \"Haldor\", \"crypt\"), and/or near a point; nearest first when near a point. Use it to find where to open_area.")]
    public Task<string> ListLocations(
        [Description("Words to match in the name, what players call it or its kind (optional).")] string? query = null,
        [Description("World x of a point to look near (optional, with z).")] float? x = null,
        [Description("World z of that point.")] float? z = null,
        [Description("How far from it, metres (default 2000).")] float radius = 2000,
        [Description("At most this many (default 100).")] int limit = 100) => OnUi(() =>
    {
        if (_w.World?.World is not { } world)
        {
            return "Open a world first (open_world).";
        }
        var rules = ValheimGen.SeedLocations.Current.locations.GroupBy(r => Save.StableHash.Of(r.prefab)).ToDictionary(g => g.Key, g => g.First().prefab);
        var all = world.Zones is { Locations.Count: > 0 } zones
            ? zones.Locations.Select(l => (l.Hash, l.Position, Generated: (bool?)l.Placed))
            : world.Locations.Select(l => (Hash: l.Location, l.Position, Generated: (bool?)true));
        string[] words = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var found = all.Where(l => !Editing.Uplift.IsDisc(l.Hash)).Select(l =>
        {
            string name = LocationName(l.Hash, rules) ?? l.Hash.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var (what, kind) = Known.TryGetValue(name, out var k) ? k : (null!, "other");
            return (Name: name, What: what, Kind: kind, l.Position, l.Generated);
        }).Where(l => words.All(w => l.Name.Contains(w, StringComparison.OrdinalIgnoreCase) || (l.What?.Contains(w, StringComparison.OrdinalIgnoreCase) ?? false)
            || l.Kind.Equals(w, StringComparison.OrdinalIgnoreCase) || (l.Kind + "es").Equals(w, StringComparison.OrdinalIgnoreCase) || (l.Kind + "s").Equals(w, StringComparison.OrdinalIgnoreCase)));
        if (x is float px && z is float pz)
        {
            found = found.Where(l => (l.Position.X - px) * (l.Position.X - px) + (l.Position.Z - pz) * (l.Position.Z - pz) <= radius * radius)
                .OrderBy(l => (l.Position.X - px) * (l.Position.X - px) + (l.Position.Z - pz) * (l.Position.Z - pz));
        }
        else
        {
            // The ones players look for first, then by name.
            found = found.OrderBy(l => l.Kind switch { "start" => 0, "boss" => 1, "trader" => 2, "dungeon" => 3, _ => 4 }).ThenBy(l => l.Name, StringComparer.Ordinal);
        }
        var list = found.ToList();
        return ToJson(new
        {
            count = list.Count,
            locations = list.Take(Math.Clamp(limit, 1, 2000)).Select(l => new
            {
                name = l.Name,
                what = l.What,
                kind = l.Kind,
                x = MathF.Round(l.Position.X, 1),
                y = MathF.Round(l.Position.Y, 1),
                z = MathF.Round(l.Position.Z, 1),
                generated = l.Generated,
            }),
        });
    });

    /// <summary>The zones (world column, row) a world rectangle touches.</summary>
    internal static List<(int X, int Z)> ZonesOf(float x0, float z0, float x1, float z1)
    {
        static int Zone(float v) => (int)MathF.Floor((v + 32) / 64);
        var zones = new List<(int X, int Z)>();
        for (int zz = Zone(MathF.Min(z0, z1)); zz <= Zone(MathF.Max(z0, z1)); zz++)
        {
            for (int zx = Zone(MathF.Min(x0, x1)); zx <= Zone(MathF.Max(x0, x1)); zx++)
            {
                zones.Add((zx, zz));
            }
        }
        return zones;
    }

    /// <summary>reset_zones: zones of the open area marked for reset when saving (or unmarked), as one pending step.</summary>
    [McpServerTool(Name = "reset_zones", Title = "Give zones back to the world generator")]
    [Description("Marks the 64 m zones a rectangle of the open area touches for reset, as the Area tool's Reset zones does: when the user saves (or applies live), every tree, rock, ruin and dungeon entrance in them is taken away (the players' buildings too unless keepBuildings), and Valheim generates them anew the next time a player comes near; with ground, their ground edits are undone at once. It throws away what is there: do it only when the user asked for it, and say which zones. cancel: true unmarks them instead. One pending step (Ctrl+Z, or cancel, takes it back).")]
    public Task<string> ResetZones(
        [Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
        [Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
        [Description("Keep the pieces players built (default true).")] bool keepBuildings = true,
        [Description("Also undo the zones' ground edits (default false).")] bool ground = false,
        [Description("Unmark them instead (default false).")] bool cancel = false) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        if (_w.InWorkshop)
        {
            return "Zones are reset in a world, not on the Workshop's plot.";
        }
        var s = _w.Session!;
        var scene = s.Scene;
        var zones = ZonesOf(x0, z0, x1, z1).Where(z => z.X >= scene.X0 && z.X < scene.X0 + scene.ZonesX && z.Z >= scene.Z0 && z.Z < scene.Z0 + scene.ZonesZ).ToList();
        if (zones.Count == 0)
        {
            return "The rectangle touches no zone of the open area (editor_state gives its bounds).";
        }
        string list = string.Join(" · ", zones.Select(z => $"{z.X}, {z.Z}"));
        if (cancel)
        {
            int n = s.UnmarkResets(zones, $"Claude: cancelled reset of {zones.Count} zone(s)");
            _w.AfterClaudeEdit($"Claude unmarked {n} zone(s).");
            return n == 0 ? "None of those zones was marked for reset." : $"Unmarked {n} zone(s). Now pending: {Pending()}";
        }
        s.MarkResets(zones, keepBuildings, ground, $"Claude: reset {zones.Count} zone(s)");
        _w.AfterClaudeEdit($"Claude marked {zones.Count} zone(s) for reset; Ctrl+Z cancels.");
        return $"Marked {zones.Count} zone(s) for reset when saved ({list}){(keepBuildings ? ", the players' buildings kept" : ", the players' buildings too")}{(ground ? ", their ground edits undone" : "")}. Now pending: {Pending()}";
    });

    // ---- New worlds.

    /// <summary>preview_seed: what a seed's world will be like, with a map of its biomes and landmarks.</summary>
    [McpServerTool(Name = "preview_seed", ReadOnly = true, Title = "Preview a seed")]
    [Description("What a world of this seed will be like, as the New world page shows it, without making it: a map of its biomes (north up, 10 km round the middle) with the start (white), traders (yellow) and bosses' altars (red), how much of it is land and in each biome, how far each biome is from the start, and where the start, traders and bosses will be. Use it to pick a seed before create_world; a new world's locations are only laid out by the game when it first loads it, so list_locations has none until then: these predictions stand in for them.")]
    public static async Task<ModelContextProtocol.Protocol.CallToolResult> PreviewSeed(
        [Description("The seed: 1 to 10 characters, as in the game.")] string seed)
    {
        seed = seed.Trim();
        if (seed.Length is 0 or > 10)
        {
            return new() { IsError = true, Content = { new ModelContextProtocol.Protocol.TextContentBlock { Text = "The seed is 1 to 10 characters, as in the game." } } };
        }
        var p = await Task.Run(() => TerrainEditor.App.SeedPreview.Make(seed, 160, parallel: true, landmarks: true));
        var st = p.Stats;
        string text = ToJson(new
        {
            seed,
            land = MathF.Round(st.Land, 3),
            biomes = st.Shares.OrderByDescending(b => b.Value).Select(b => new { biome = b.Key.ToString(), share = MathF.Round(b.Value, 3) }),
            start = new { x = MathF.Round(st.Start.X), z = MathF.Round(st.Start.Z), temple = st.TempleStart },
            fromStart = st.Distance.Where(d => float.IsFinite(d.Value)).Select(d => new { biome = d.Key.ToString(), metres = MathF.Round(d.Value), sameLand = st.SameLand.GetValueOrDefault(d.Key) }),
            landmarks = (p.Landmarks ?? []).Select(l => new { name = l.Name, kind = l.Kind, x = MathF.Round(l.X), z = MathF.Round(l.Z), oneOfSeveral = l.OneOf }),
            note = p.Landmarks == null ? "The game's location rules could not be read: no landmarks." : null,
        });
        return new()
        {
            Content =
            {
                new ModelContextProtocol.Protocol.TextContentBlock { Text = text },
                ModelContextProtocol.Protocol.ImageContentBlock.FromBytes(SeedPicture(p), "image/jpeg"),
            },
        };
    }

    /// <summary>A seed preview's biomes as a 640-pixel picture, north up, with its start, traders and bosses as dots.</summary>
    internal static byte[] SeedPicture(TerrainEditor.App.SeedPreview.Preview p)
    {
        const int Side = 640;
        using var cells = new SkiaSharp.SKBitmap(p.Size, p.Size);
        for (int j = 0; j < p.Size; j++)
        {
            for (int i = 0; i < p.Size; i++)
            {
                var (r, g, b) = SeedMap.Shade(p.Biome[j * p.Size + i], p.Height[j * p.Size + i]);
                // Row 0 is the south: drawn at the bottom.
                cells.SetPixel(i, p.Size - 1 - j, new SkiaSharp.SKColor(r, g, b));
            }
        }
        using var sheet = new SkiaSharp.SKBitmap(Side, Side);
        using (var canvas = new SkiaSharp.SKCanvas(sheet))
        {
            using (var image = SkiaSharp.SKImage.FromBitmap(cells))
            {
                canvas.DrawImage(image, SkiaSharp.SKRect.Create(0, 0, Side, Side), new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Nearest));
            }
            float k = Side / (2 * TerrainEditor.App.SeedPreview.Radius);
            using var dot = new SkiaSharp.SKPaint { IsAntialias = true };
            using var rim = new SkiaSharp.SKPaint { IsAntialias = true, Color = SkiaSharp.SKColors.Black, Style = SkiaSharp.SKPaintStyle.Stroke, StrokeWidth = 1 };
            foreach (var l in p.Landmarks ?? [])
            {
                dot.Color = l.Kind switch { "start" => SkiaSharp.SKColors.White, "trader" => SkiaSharp.SKColors.Gold, _ => SkiaSharp.SKColors.Red };
                float x = (l.X + TerrainEditor.App.SeedPreview.Radius) * k, y = Side - (l.Z + TerrainEditor.App.SeedPreview.Radius) * k;
                canvas.DrawCircle(x, y, 4, dot);
                canvas.DrawCircle(x, y, 4, rim);
            }
        }
        using var final = SkiaSharp.SKImage.FromBitmap(sheet);
        return final.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 85).ToArray();
    }

    /// <summary>create_world: a new world of a name and seed in the first worlds folder, then opened (the New world page's Create).</summary>
    [McpServerTool(Name = "create_world", Title = "Make a new world")]
    [Description("Makes a new world, as the New world page's Create does: its name, its seed (preview_seed first), in the game's worlds folder (or the one given, from list_worlds' folders), and opens it on its map. Nothing existing is changed: a name already taken is refused, and so is leaving a world or the Workshop with changes not saved. The game lays out the start, traders and dungeons the first time it loads the world, and makes each place as players reach it: shaping an area before anyone played there works, but locations come later.")]
    public Task<string> CreateWorld(
        [Description("The world's name (it must be a valid file name).")] string name,
        [Description("The seed: 1 to 10 characters, as in the game.")] string seed,
        [Description("The worlds folder to make it in (optional: the game's).")] string? folder = null) => OnUi(async () =>
    {
        if (Unsaved() is string why)
        {
            return why;
        }
        name = name.Trim();
        seed = seed.Trim();
        string root = folder ?? NewWorldPage.Roots().FirstOrDefault() ?? TerrainEditor.App.Places.WorldRoots().First();
        if (NewWorldPage.Check(root, name, seed) is string problem)
        {
            return problem;
        }
        if (await _w.CreateWorld(Path.Combine(root, name), name, seed) is string failed)
        {
            return failed;
        }
        return _w.World?.World.Name == name ? $"{_w.MessageText.Text} {ToJson(StateOf(_w))}" : $"Made {name}, but it could not be opened: {_w.MessageText.Text}";
    });

    // ---- Copies.

    /// <summary>
    /// copy_objects: copies of objects, shifted and turned as one group, once or several times in a row (each copy a
    /// step further), new independent objects as the Select tool's copy makes them.
    /// </summary>
    [McpServerTool(Name = "copy_objects", Title = "Copy objects")]
    [Description("Copies objects of the open area (by id) as one group, as the editor's copy and paste does: shifted by dx, dy, dz metres and turned by turn degrees round one point (default: their middle), copies times in a row, each copy a step further (a row of houses, a fence, a ring of pillars with turn and a pivot). Copies are new objects of the same kinds, sizes and builder; what the originals hold (chest contents, text, portal tags) is not copied. ground: true keeps each copy as high above the ground as its original. Refused when a copy would leave the area. One pending step; the answer gives the new ids.")]
    public Task<string> CopyObjects(
        [Description("Their ids (list_objects, get_selection).")] int[] ids,
        [Description("Metres east of each copy from the one before (negative: west).")] float dx = 0,
        [Description("Metres up.")] float dy = 0,
        [Description("Metres north (negative: south).")] float dz = 0,
        [Description("Degrees each copy is turned from the one before, round the pivot (as yaw: 90 turns north to east).")] float turn = 0,
        [Description("World x of the point to turn round (optional, with pivotZ: their middle).")] float? pivotX = null,
        [Description("World z of that point.")] float? pivotZ = null,
        [Description("How many copies (default 1, up to 100).")] int copies = 1,
        [Description("Keep each copy as high above the ground as its original (default false).")] bool ground = false,
        [Description("A few words for the history.")] string label = "Claude: copied") => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        var s = _w.Session!;
        var scene = s.Scene;
        var (ok, problems) = Usable(scene, ids);
        if (ok.Count == 0)
        {
            return "Nothing copied: " + string.Join("; ", problems);
        }
        if (copies is < 1 or > 100)
        {
            return "copies is 1 to 100.";
        }
        if (dx == 0 && dy == 0 && dz == 0 && turn % 360 == 0)
        {
            return "The copies would be on top of the originals: give dx, dy, dz or turn.";
        }
        float px = pivotX ?? ok.Average(i => scene.Things[i].Position.X), pz = pivotZ ?? ok.Average(i => scene.Things[i].Position.Z);
        var (minX, minZ, maxX, maxZ) = Bounds(scene);
        var adds = new List<(Editing.NewObject, bool)>();
        var from = new List<int>();
        for (int k = 1; k <= copies; k++)
        {
            foreach (int i in ok)
            {
                var t = scene.Things[i];
                var (ox, oz) = Turned(t.Position.X - px, t.Position.Z - pz, turn * k);
                float cx = px + ox + dx * k, cz = pz + oz + dz * k;
                if (cx < minX || cx > maxX || cz < minZ || cz > maxZ)
                {
                    return $"Nothing copied: copy {k} of {i} ({_w.NameOfPrefab(t.Prefab)}) would be at ({cx:0.##}, {cz:0.##}), outside the area (x {minX} to {maxX}, z {minZ} to {maxZ}).";
                }
                float cy = t.Position.Y + dy * k + (ground ? GroundAt(scene, cx, cz) - GroundAt(scene, t.Position.X, t.Position.Z) : 0);
                // As the Select tool's copy: what it was copied from (its kind's data, its builder), fresh.
                var added = t.Id < 0 ? s.Edits.FindAdded(t.Id) : null;
                adds.Add((new Editing.NewObject(0, t.Prefab, new Vector3(cx, cy, cz), t.Rotation + new Vector3(0, turn * k, 0), t.Scale,
                    t.Id >= 0 ? t.Id : added?.SourceId, true, Data: added?.Data), t.Piece));
                from.Add(i);
            }
        }
        var made = s.Commit(label.StartsWith("Claude", StringComparison.Ordinal) ? label : "Claude: " + label, null, [], adds);
        _w.AfterClaudeEdit($"Claude made {made.Count} cop{(made.Count == 1 ? "y" : "ies")}; Ctrl+Z takes them back.");
        return ToJson(new { copied = made.Select((c, k) => Moved(scene, from[k], c)), problems, pending = Pending() });
    });
}
