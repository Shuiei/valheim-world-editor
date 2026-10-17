using System.ComponentModel;
using System.Numerics;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's tools for the Area tool's ground actions and for nature: smoothing, naturalizing, eroding, restoring,
/// raising and lowering a rectangle of ground, and regrowing what the game grows there.
/// </summary>
public sealed partial class ClaudeTools
{
    // ---- The Area tool's ground actions, and nature.

    /// <summary>The ground actions shape_ground takes, by name.</summary>
    internal static readonly Dictionary<string, AreaTool.GroundAction> GroundActions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["smooth"] = AreaTool.GroundAction.Smooth,
        ["naturalize"] = AreaTool.GroundAction.Natural,
        ["erode"] = AreaTool.GroundAction.Erode,
        ["restore"] = AreaTool.GroundAction.Restore,
        ["raise"] = AreaTool.GroundAction.Raise,
        ["lower"] = AreaTool.GroundAction.Lower,
        ["paint"] = AreaTool.GroundAction.Paint,
    };

    /// <summary>The paints, by name, as the paint brushes put them down.</summary>
    internal static readonly Dictionary<string, BrushTool> Paints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dirt"] = BrushTool.PaintDirt,
        ["cultivated"] = BrushTool.PaintCultivated,
        ["paved"] = BrushTool.PaintPaved,
        ["clear"] = BrushTool.PaintClear,
    };

    /// <summary>Which ground a change may touch, as the Mask panel's settings; what is left out does not limit.</summary>
    public sealed class MaskSpec
    {
        /// <summary>Lowest ground height that passes, metres.</summary>
        [Description("Lowest ground height that changes, metres (optional).")]
        public float? HeightMin { get; set; }

        /// <summary>Highest ground height that passes, metres.</summary>
        [Description("Highest ground height that changes, metres (optional).")]
        public float? HeightMax { get; set; }

        /// <summary>Smallest slope that passes, degrees.</summary>
        [Description("Smallest slope that changes, degrees (optional; e.g. 30: only steep ground).")]
        public float? SlopeMin { get; set; }

        /// <summary>Largest slope that passes, degrees.</summary>
        [Description("Largest slope that changes, degrees (optional; e.g. 15: only gentle ground).")]
        public float? SlopeMax { get; set; }

        /// <summary>The biomes that pass; none: all.</summary>
        [Description("Only in these biomes (optional): Meadows, Black Forest, Swamp, Mountain, Plains, Mistlands, Ashlands, Deep North, Ocean.")]
        public string[]? Biomes { get; set; }

        /// <summary>The rule on painted ground.</summary>
        [Description("By paint (optional): any, unpainted, painted, dirt, cultivated or paved.")]
        public string? Paint { get; set; }
    }

    /// <summary>The Mask over the session's ground for a spec (null: no mask), or why the spec is wrong.</summary>
    internal static (Func<int, float>? Mask, string? Problem) MaskOf(MaskSpec? spec, EditSession s)
    {
        if (spec == null)
        {
            return (null, null);
        }
        var mask = new Mask { On = true, HeightMin = spec.HeightMin, HeightMax = spec.HeightMax, SlopeMin = spec.SlopeMin, SlopeMax = spec.SlopeMax };
        foreach (string b in spec.Biomes ?? [])
        {
            var found = ZoneFilter.BiomeChoices(english: true).Where(n => string.Equals(n.Name.Replace(" ", "", StringComparison.Ordinal), b.Replace(" ", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase)).ToList();
            if (found.Count == 0)
            {
                return (null, $"{b}: the biomes are {string.Join(", ", ZoneFilter.BiomeChoices(english: true).Select(n => n.Name))}.");
            }
            mask.Biomes.Add(found[0].Value);
        }
        if (spec.Paint != null)
        {
            if (!Enum.TryParse<Mask.PaintRule>(spec.Paint, true, out var rule))
            {
                return (null, "The mask's paint is any, unpainted, painted, dirt, cultivated or paved.");
            }
            mask.Paint = rule;
        }
        if (mask.Warning(english: true) is { Length: > 0 } warning)
        {
            return (null, "The mask lets nothing through: " + warning);
        }
        return (mask.For(s.Ground, s.Scene.Biomes), null);
    }

    /// <summary>
    /// The Area tool's box over a world rectangle of the scene (its corners in grid points), with a soft edge of
    /// edge metres going inwards.
    /// </summary>
    internal static AreaTool BoxOver(WorldScene scene, float x0, float z0, float x1, float z1, float edge)
    {
        float ox = scene.OriginX, oz = scene.OriginZ;
        var box = new AreaTool { Box = true, Soft = MathF.Max(0, edge) };
        box.Points.Add(new Vector2(MathF.Min(x0, x1) - ox, MathF.Min(z0, z1) - oz));
        box.Points.Add(new Vector2(MathF.Max(x0, x1) - ox, MathF.Max(z0, z1) - oz));
        return box;
    }

    /// <summary>shape_ground: an Area tool ground action over a rectangle, as one pending step.</summary>
    [McpServerTool(Name = "shape_ground", Title = "Smooth, naturalize, erode or restore ground")]
    [Description("Changes the ground of a rectangle of the open area as the Area tool does, fading in over a soft edge going inwards: smooth (evens out bumps and steps), naturalize (natural-looking bumps, bumps metres high and bumpSize metres wide: what made-up ground needs to look real), erode (weathers it: slopes settle to restAngle degrees and rain cuts gullies), restore (back to the ground the game generated, paint taken away), raise or lower (by amount metres), paint (with paint: dirt, cultivated, paved or clear). A mask limits it to some ground, as the Mask panel does (heights, slopes, biomes, paint): paint only the gentle slopes, smooth only the steep ones, raise only what is below a height. One pending step. The game's ±8 m limit from the original ground holds.")]
    public Task<string> ShapeGround(
        [Description("smooth, naturalize, erode, restore, raise, lower or paint.")] string action,
        [Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
        [Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
        [Description("The soft edge, metres inwards from the rectangle's sides (default 3; 0: hard).")] float edge = 3,
        [Description("raise and lower: metres (default 1).")] float amount = 1,
        [Description("naturalize: how high the bumps are, metres (default 1.2).")] float bumps = 1.2f,
        [Description("naturalize: how wide the bumps are, metres (default 16).")] float bumpSize = 16,
        [Description("erode: the slope ground settles to, degrees (default 33).")] float restAngle = 33,
        [Description("naturalize and erode: any number, another pattern with the same choices.")] int seed = 1,
        [Description("paint: dirt, cultivated, paved or clear.")] string paint = "dirt",
        [Description("Which ground may change (optional: all of it).")] MaskSpec? mask = null) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        if (!GroundActions.TryGetValue(action, out var act))
        {
            return "The action is smooth, naturalize, erode, restore, raise, lower or paint.";
        }
        if (!Paints.TryGetValue(paint, out var paintTool))
        {
            return "The paint is dirt, cultivated, paved or clear.";
        }
        var s = _w.Session!;
        var (only, wrong) = MaskOf(mask, s);
        if (wrong != null)
        {
            return wrong;
        }
        var (minX, minZ, maxX, maxZ) = Bounds(s.Scene);
        if (MathF.Max(x0, x1) < minX || MathF.Min(x0, x1) > maxX || MathF.Max(z0, z1) < minZ || MathF.Min(z0, z1) > maxZ)
        {
            return $"The rectangle is outside the open area (x {minX} to {maxX}, z {minZ} to {maxZ}).";
        }
        var box = BoxOver(s.Scene, x0, z0, x1, z1, edge);
        var brush = new Brush
        {
            NoiseAmp = Math.Clamp(bumps, 0, 20),
            NoiseSize = Math.Clamp(bumpSize, 2, 200),
            Noise = new Noise(seed),
            RestAngle = Math.Clamp(restAngle, 5, 80),
            Random = new Random(seed),
        };
        string name = act == AreaTool.GroundAction.Paint ? $"paint {paint.ToLowerInvariant()}" : AreaTool.Label(act).ToLowerInvariant();
        bool clamped = false;
        var touched = s.EditGround($"Claude: {name}", g =>
        {
            var (t, rect, c) = box.Apply(g, brush, act, 0, MathF.Max(0, amount), Brush.PaintOf(paintTool)!, only);
            clamped = c;
            return (t, rect);
        });
        _w.AfterClaudeEdit($"Claude's {name}: {touched.Count} ground point(s); Ctrl+Z takes it back.");
        return touched.Count == 0 ? "Nothing changed." : $"{AreaTool.Label(act)}: {touched.Count} ground point(s) changed.{(clamped ? " Part of it reached the game's ±8 m limit." : "")} Now pending: {Pending()}";
    });

    /// <summary>The Path tool's actions dig_path takes, by name.</summary>
    internal static readonly Dictionary<string, PathTool.Action> PathActions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["river"] = PathTool.Action.River,
        ["cave"] = PathTool.Action.Cave,
        ["ramp"] = PathTool.Action.Ramp,
        ["flatten"] = PathTool.Action.Flatten,
        ["raise"] = PathTool.Action.Raise,
        ["lower"] = PathTool.Action.Lower,
        ["smooth"] = PathTool.Action.Smooth,
    };

    /// <summary>
    /// The Path tool on a line of world points of the scene (in grid points), with the values given; ramp's ends and
    /// flatten's height default to the ground at the line's ends.
    /// </summary>
    internal static PathTool LineOver(WorldScene scene, float[][] points, PathTool.Action act, float width, float soft, float? depth, float headroom,
        float? height, float amount, bool natural, bool curved, PathTool.CaveRock rock, int seed)
    {
        float ox = scene.OriginX, oz = scene.OriginZ;
        var path = new PathTool
        {
            Act = act,
            Width = Math.Clamp(width, 1, 64),
            Soft = Math.Clamp(soft, 0, 32),
            Amount = MathF.Max(0, amount),
            Natural = natural,
            Curved = curved,
            Rock = rock,
            Seed = seed,
            Headroom = Math.Clamp(headroom, 2, 30),
            RampEdited = true,
        };
        path.Points.AddRange(points.Select(p => new Vector2(p[0] - ox, p[1] - oz)));
        float first = GroundAt(scene, points[0][0], points[0][1]), last = GroundAt(scene, points[^1][0], points[^1][1]);
        (path.Start, path.End) = (height ?? first, height is null ? last : height.Value);
        path.Height = height ?? first;
        if (act == PathTool.Action.Cave)
        {
            path.CaveDepth = Math.Clamp(depth ?? 7.5f, 2, 40);
        }
        else
        {
            path.Depth = Math.Clamp(depth ?? 2, 0, 30);
        }
        return path;
    }

    /// <summary>dig_path: a Path tool action along a line of points, as one pending step.</summary>
    [McpServerTool(Name = "dig_path", Title = "A river, cave, ramp or flattened way along a line")]
    [Description("Changes the ground along a line of points of the open area as the Path tool does, a smooth curve through them (curved: false for straight stretches): river (digs a bed depth metres below the water level, so the sea fills it: a river, canal or moat), cave (digs a trench depth metres deep and roofs it over with the game's boulders, headroom metres high inside: a walkable tunnel or cave; its ends slope up to the ground; trees and rocks in the way go), ramp (a slope from the ground at the first point to the ground at the last, or height for both), flatten (to height, default: the ground at the first point), raise or lower (by amount), smooth. width metres wide, fading out over soft metres beyond. natural: true gives a varying width, ragged edges and bumps. One pending step.")]
    public Task<string> DigPath(
        [Description("river, cave, ramp, flatten, raise, lower or smooth.")] string action,
        [Description("The line's points, in order: [[x, z], [x, z], …] (at least 2).")] float[][] points,
        [Description("Width, metres (default 8).")] float width = 8,
        [Description("Soft edge beyond the width, metres (default 4).")] float soft = 4,
        [Description("river: metres below the water level (default 2); cave: metres below the ground in the middle (default 7.5; more than headroom + 1.5 for a roof).")] float? depth = null,
        [Description("cave: headroom inside, metres (default 4.5).")] float headroom = 4.5f,
        [Description("flatten: the height; ramp: one height for both ends (optional: the ground at the ends).")] float? height = null,
        [Description("raise and lower: metres (default 2).")] float amount = 2,
        [Description("A varying width, ragged edges and bumps (default false).")] bool natural = false,
        [Description("A smooth curve through the points (default true).")] bool curved = true,
        [Description("cave: the roof's boulders: auto (by biome), forest, coast, heath or mountain.")] string rock = "auto",
        [Description("Any number: another roof and natural pattern with the same choices.")] int seed = 1,
        [Description("Which ground may change (optional: all of it).")] MaskSpec? mask = null) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        if (!PathActions.TryGetValue(action, out var act))
        {
            return "The action is river, cave, ramp, flatten, raise, lower or smooth.";
        }
        if (points.Length < 2 || points.Any(p => p.Length < 2))
        {
            return "A line needs at least 2 points, each [x, z].";
        }
        if (!Enum.TryParse<PathTool.CaveRock>(rock, true, out var caveRock))
        {
            return "The rock is auto, forest, coast, heath or mountain.";
        }
        var s = _w.Session!;
        var (minX, minZ, maxX, maxZ) = Bounds(s.Scene);
        if (points.FirstOrDefault(p => p[0] < minX || p[0] > maxX || p[1] < minZ || p[1] > maxZ) is { } outside)
        {
            return $"({outside[0]}, {outside[1]}) is outside the open area (x {minX} to {maxX}, z {minZ} to {maxZ}).";
        }
        var (only, wrong) = MaskOf(mask, s);
        if (wrong != null)
        {
            return wrong;
        }
        var path = LineOver(s.Scene, points, act, width, soft, depth, headroom, height, amount, natural, curved, caveRock, seed);
        var brush = new Brush { Noise = new Noise(seed) };
        string label = $"Claude: {action.ToLowerInvariant()} along a line";
        string said;
        if (act == PathTool.Action.Cave)
        {
            said = MainWindow.ApplyCave(s, path, brush, only, label);
        }
        else
        {
            bool clamped = false;
            var touched = s.EditGround(label, g =>
            {
                var (t, rect, c) = path.Apply(g, brush, s.Scene.Water, only);
                clamped = c;
                return (t, rect);
            });
            said = touched.Count == 0 ? "Nothing changed along the line."
                : $"{PathTool.Label(act)} along {PathTool.Length(path.Curve()):0} m: {touched.Count} ground point(s).{(clamped ? " Part of it reached the game's ±8 m limit." : "")}";
        }
        _w.AfterClaudeEdit(said);
        return said + " Now pending: " + Pending();
    });

    /// <summary>The stamps offered: the built-in ones, then those the user loaded (by name or label).</summary>
    internal static List<Stamps.Stamp> StampsOffered() => [.. Stamps.BuiltIn, .. Stamps.LoadKept()];

    /// <summary>stamp: a stamp's picture pressed into the ground at a point, raised or lowered, as one pending step.</summary>
    [McpServerTool(Name = "stamp", Title = "Press a stamp into the ground")]
    [Description("Presses a stamp into the ground of the open area as the Raise or Lower brush does with a stamp shape and Stamp once: the stamp's picture (white high, black untouched) radius metres round x, z, height metres up (negative: down, a hollow), turned by turn degrees. Stamps: Mountain, Mesa (flat top), Crater rim, Dunes, Rocky ground, and those the user loaded (a picture's name, or a PNG file's path to use any greyscale picture). A mask limits it to some ground. One pending step; the game's ±8 m limit holds.")]
    public Task<string> Stamp(
        [Description("The stamp's name (Mountain, Mesa, Crater rim, Dunes, Rocky ground, a loaded one), or the path of a PNG picture.")] string stamp,
        [Description("World x of its middle.")] float x,
        [Description("World z of its middle.")] float z,
        [Description("Radius, metres (half its side).")] float radius = 20,
        [Description("Metres up at its whitest (negative: down).")] float height = 4,
        [Description("Degrees to turn it (default 0).")] float turn = 0,
        [Description("Which ground may change (optional: all of it).")] MaskSpec? mask = null) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        float[]? data = StampsOffered().FirstOrDefault(st => st.Label.StartsWith(stamp, StringComparison.OrdinalIgnoreCase) || st.Name.Equals(stamp, StringComparison.OrdinalIgnoreCase))?.Data;
        if (data == null && File.Exists(stamp))
        {
            try
            {
                data = Stamps.FromPicture(File.ReadAllBytes(stamp));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return $"That picture cannot be read: {ex.Message}";
            }
        }
        if (data == null)
        {
            return $"No stamp \"{stamp}\": {string.Join(", ", StampsOffered().Select(st => st.Label))}, or a PNG picture's path.";
        }
        var s = _w.Session!;
        var (minX, minZ, maxX, maxZ) = Bounds(s.Scene);
        if (x < minX || x > maxX || z < minZ || z > maxZ)
        {
            return $"({x}, {z}) is outside the open area (x {minX} to {maxX}, z {minZ} to {maxZ}).";
        }
        var (only, wrong) = MaskOf(mask, s);
        if (wrong != null)
        {
            return wrong;
        }
        var brush = new Brush { Shape = BrushShape.Stamp, StampData = data, Radius = Math.Clamp(radius, 2, 500), Turn = turn };
        float ox = s.Scene.OriginX, oz = s.Scene.OriginZ;
        bool clamped = false;
        var touched = s.EditGround($"Claude: stamp {stamp}", g =>
        {
            var (t, rect, c) = Sculpt.StampOnce(g, brush, x - ox, z - oz, height, only);
            clamped = c;
            return (t, rect);
        });
        _w.AfterClaudeEdit($"Claude pressed a stamp into {touched.Count} ground point(s); Ctrl+Z takes it back.");
        return touched.Count == 0 ? "Nothing changed." : $"Stamped {touched.Count} ground point(s){(clamped ? "; part of it reached the game's ±8 m limit" : "")}. Now pending: {Pending()}";
    });

    /// <summary>import_heightmap: a greyscale picture's heights into a rectangle of the ground, as one pending step.</summary>
    [McpServerTool(Name = "import_heightmap", Title = "Import a heightmap")]
    [Description("Puts a heightmap picture (a PNG file on this computer, greyscale: black low, white high) into the ground of a rectangle of the open area, as the Area tool's Heightmap import does: stretched over the rectangle, from lowest (black) to highest (white) metres, fading in over a soft edge going inwards. Pictures the editor exported carry their own lowest and highest; otherwise they default to the range of the ground there now. A mask limits it to some ground. One pending step; the game's ±8 m limit holds.")]
    public Task<string> ImportHeightmap(
        [Description("The PNG picture's path.")] string path,
        [Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
        [Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
        [Description("Height of black, metres (optional).")] float? lowest = null,
        [Description("Height of white, metres (optional).")] float? highest = null,
        [Description("The soft edge, metres inwards (default 3; 0: hard).")] float edge = 3,
        [Description("Which ground may change (optional: all of it).")] MaskSpec? mask = null) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        var s = _w.Session!;
        var (only, wrong) = MaskOf(mask, s);
        if (wrong != null)
        {
            return wrong;
        }
        if (BoxOver(s.Scene, x0, z0, x1, z1, edge).WeightsIn(s.Ground.W, s.Ground.H, only) is not { Cells.Count: > 0 } a)
        {
            return "The rectangle holds no ground of the open area.";
        }
        int w = a.X1 - a.X0 + 1, h = a.Z1 - a.Z0 + 1;
        TerrainEditor.App.Heightmaps.Picture pic;
        try
        {
            pic = TerrainEditor.App.Heightmaps.Read(File.ReadAllBytes(path), w, h);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return $"That picture cannot be used: {ex.Message}";
        }
        float lo = lowest ?? pic.Min ?? a.Cells.Min(c => s.Ground.HeightOf(c.G)), hi = highest ?? pic.Max ?? a.Cells.Max(c => s.Ground.HeightOf(c.G));
        int clamped = 0;
        var touched = s.EditGround("Claude: heightmap import", g =>
        {
            var (list, c) = AreaPanel.PutHeights(g, pic.Values, a.X0, a.Z0, w, h, a.Cells, lo, hi);
            clamped = c;
            return (list, (0, 0, g.W - 1, g.H - 1));
        });
        _w.AfterClaudeEdit($"Claude imported a heightmap into {touched.Count} ground point(s); Ctrl+Z takes it back.");
        return $"Imported {pic.Width} × {pic.Height} into {w} × {h} m, {lo:0.##} to {hi:0.##} m: {touched.Count} ground point(s){(clamped > 0 ? $", {clamped} kept from their height by the game's ±8 m" : "")}. Now pending: {Pending()}";
    });

    /// <summary>The most points ground_at answers for at once.</summary>
    private const int MostPoints = 500;

    /// <summary>ground_at: the ground at world points of the open area: its height now, as generated, its biome.</summary>
    [McpServerTool(Name = "ground_at", ReadOnly = true, Title = "The ground at points")]
    [Description("The ground at points of the open area (or the plot), exactly: its height now (between the 1 m grid points, as the game draws it), its height as the game generated it (the difference is what edits changed; the game keeps ground within 8 m of it, ground discs aside), its biome, and how deep water stands there (0: dry). Up to 500 points at once. Use it to set pieces' heights, check slopes and levels before building, and read a place exactly.")]
    public Task<string> GroundAtPoints(
        [Description("The points: [[x, z], [x, z], …].")] float[][] points) => OnUi(() =>
    {
        if (_w.Session is not { } s)
        {
            return "No area is open (open_area, or the Workshop).";
        }
        if (points.Length == 0 || points.Length > MostPoints || points.Any(p => p.Length < 2))
        {
            return $"Give 1 to {MostPoints} points, each [x, z].";
        }
        var scene = s.Scene;
        var (minX, minZ, maxX, maxZ) = Bounds(scene);
        float ox = scene.OriginX, oz = scene.OriginZ;
        return ToJson(points.Select(p =>
        {
            float x = p[0], z = p[1];
            if (x < minX || x > maxX || z < minZ || z > maxZ)
            {
                return (object)new { x, z, error = "outside the area" };
            }
            int gx = Math.Clamp((int)MathF.Round(x - ox), 0, scene.W - 1), gz = Math.Clamp((int)MathF.Round(z - oz), 0, scene.H - 1), i = gz * scene.W + gx;
            float h = Picking.HeightAt(scene, x - scene.Cx, -(z - scene.Cz));
            return new
            {
                x,
                z,
                height = MathF.Round(h, 3),
                original = MathF.Round(s.Ground.Original(i), 3),
                biome = ((ValheimGen.Heightmap.Biome)scene.Biomes[i]).ToString(),
                water = MathF.Round(MathF.Max(0, scene.Water - h), 2),
            };
        }));
    });

    /// <summary>regrow_nature: what the game grows in a rectangle (Growth), of the kinds asked, where nothing stands.</summary>
    [McpServerTool(Name = "regrow_nature", Title = "Regrow nature")]
    [Description("Regrows nature over a rectangle of the open area of a world, as the Area tool's Regrow nature does: the trees, rocks, bushes and pickables the game itself grows there from the world's seed (its kinds, sizes and places, for the biome), left out where something stands (anything within 1 m, the same kind within 3 m, buildings within 4 m). Use it after clearing or reshaping ground to make it look untouched. Up to 16 zones (256 x 256 m) at once. One pending step.")]
    public async Task<string> RegrowNature(
        [Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
        [Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
        [Description("Kinds to grow: Trees, Rocks, Bushes, Pickables (default: all four).")] string[]? kinds = null)
    {
        var chosen = new HashSet<TerrainEditor.App.ObjectKind>();
        foreach (string k in kinds is { Length: > 0 } ? kinds : ["Trees", "Rocks", "Bushes", "Pickables"])
        {
            if (!Enum.TryParse<TerrainEditor.App.ObjectKind>(k, true, out var kind) || kind is not (TerrainEditor.App.ObjectKind.Trees or TerrainEditor.App.ObjectKind.Rocks or TerrainEditor.App.ObjectKind.Bushes or TerrainEditor.App.ObjectKind.Pickables))
            {
                return $"{k}: the kinds are Trees, Rocks, Bushes and Pickables.";
            }
            chosen.Add(kind);
        }
        string? problem = null;
        var session = await OnUi(() =>
        {
            if (CannotEdit() is string why)
            {
                problem = why;
                return null;
            }
            if (_w.InWorkshop)
            {
                problem = "Nature regrows in a world, not on the Workshop's plot.";
                return null;
            }
            return _w.Session;
        });
        if (session == null)
        {
            return problem!;
        }
        float ax = MathF.Min(x0, x1), az = MathF.Min(z0, z1), bx = MathF.Max(x0, x1), bz = MathF.Max(z0, z1);
        string said = "";
        var spots = await OnUi(() => Growth.Spots(session, ax, az, bx, bz,
            o => o.X >= ax && o.X <= bx && o.Z >= az && o.Z <= bz && chosen.Contains(TerrainEditor.App.ObjectKinds.Of(o.Name, false)),
            _w.NameOfPrefab, m => said = m.English));
        if (spots == null)
        {
            return said;
        }
        if (spots.Count == 0)
        {
            return "Nothing to regrow: the game grows none of those kinds here, or it all still stands.";
        }
        return await OnUi(() =>
        {
            // The area may have changed while the spots were worked out.
            if (_w.Session != session || CannotEdit() is string why)
            {
                return "The open area changed meanwhile: nothing regrown.";
            }
            Growth.Commit(session, $"Claude: regrew {spots.Count} object(s)", spots);
            _w.AfterClaudeEdit($"Claude regrew {spots.Count} object(s); Ctrl+Z takes them back.");
            var counts = spots.GroupBy(o => o.Name).OrderByDescending(g => g.Count()).Select(g => $"{g.Count()} {g.Key}");
            return $"Regrew {spots.Count} object(s): {string.Join(", ", counts.Take(12))}. Now pending: {Pending()}";
        });
    }
}
