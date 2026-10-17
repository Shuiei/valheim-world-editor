using System.Text.Json;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// The documentation's pictures (docs/images), one scene per subject, often a few pictures each (a tool's settings
/// side by side). Each scene gets a fresh editor (Program.cs), builds what its pictures show with the app's own tools
/// on the Docs world's bare zone, takes its pictures and leaves without saving: what a picture shows is only that
/// scene's own changes.
/// </summary>
/// <remarks>
/// The scenes are split by documentation page: the ground tools (Scenes.Ground.cs), objects (Scenes.Objects.cs),
/// building and dungeons (Scenes.Building.cs), and the pages around the 3D editor (Scenes.Pages.cs). A picture shows
/// its subject clearly: nothing in front of it (the tool panel stays on the left, the subject right of it), neither
/// too close nor too far.
/// </remarks>
public sealed partial class Scenes
{
    /// <summary>The Docs world's bare zone (WorldCheck create --flat), in zone coordinates.</summary>
    private const int ZoneX = -10, ZoneZ = -5;
    /// <summary>The bare zone's middle, in world metres.</summary>
    private const double Cx = ZoneX * 64, Cz = ZoneZ * 64;

    /// <summary>The folder the pictures go to.</summary>
    private readonly string _out;

    /// <summary>The scenes, run on the Docs world in <paramref name="dir"/>.</summary>
    /// <param name="dir">The folder holding the Docs world (made with WorldCheck create).</param>
    /// <param name="output">The folder the pictures go to.</param>
    public Scenes(string dir, string output)
    {
        World = $"{dir}/Docs";
        _out = output;
        All =
        [
            // Getting around (editor-basics.md).
            ("overview", Overview),
            ("unsaved", Unsaved),
            ("history", History),
            ("help", Help),
            ("walk", Walk),
            // Ground tools (terrain.md).
            ("sculpt", Sculpt),
            ("brush-shapes", BrushShapes),
            ("brush-falloffs", BrushFalloffs),
            ("flatten", Flatten),
            ("naturalize", Naturalize),
            ("erode", Erode),
            ("stamps", Stamps),
            ("paint", Paint),
            ("no-limit", NoLimit),
            ("shape", ShapeTool),
            ("mountain", Mountain),
            // Path (path.md).
            ("path", PathTool),
            ("path-ramp", PathRamp),
            ("path-river", PathRiver),
            ("path-cave", PathCave),
            // Mask, Measure (masks.md, measure.md).
            ("mask", Mask),
            ("measure", Measure),
            ("height-lines", HeightLines),
            // Area, copy and paste (area.md).
            ("area", Area),
            ("area-polygon", AreaPolygon),
            ("area-regrow", AreaRegrow),
            ("paste", Paste),
            ("paste-stack", PasteStack),
            // Place (place.md).
            ("plant-brush", PlantBrush),
            ("plant-line", PlantLine),
            ("line-shapes", LineShapes),
            ("plant-grid", PlantGrid),
            ("plant-zone", PlantZone),
            ("place-chooser", PlaceChooser),
            ("spawn-points", SpawnPoints),
            // Select (select.md).
            ("select-arrows", SelectArrows),
            ("select-building", SelectBuilding),
            ("select-zone", SelectZone),
            ("inspector", Inspector),
            // Dungeons (dungeons.md).
            ("dungeon", Dungeon),
            // The Workshop and blueprints (workshop.md).
            ("blueprints", Blueprints),
            ("workshop", Workshop),
            ("workshop-dungeon", WorkshopDungeon),
            // Script (scripting.md).
            ("script", Script),
            // The map, the start page, a new world (map.md, README, new-world.md).
            ("map", Map),
            ("start", Start),
            // Live mode (live-mode.md), with $DOCS_LIVE.
            ("live", Live),
            ("new-world", NewWorld),
        ];
    }

    /// <summary>The scenes by name, in the order they run when none is named.</summary>
    public IReadOnlyList<(string Name, Action<Editor> Run)> All { get; }

    /// <summary>A point on the bare zone, dx east and dz north of its middle (metres).</summary>
    private static (double X, double Z) P(double dx, double dz) => (Cx + dx, Cz + dz);

    /// <summary>The Docs world's folder.</summary>
    private string World { get; }

    /// <summary>A picture's path in the output folder.</summary>
    private string Out(string file) => System.IO.Path.Combine(_out, file);

    /// <summary>A command with its trailing modifiers (none: no trailing space).</summary>
    private static string Cmd(string command) => command.Trim();

    /// <summary>
    /// The bare zone in the 3D editor, a camera that sees all of it while the scene works. The View switches are set
    /// first, while the View panel is open (closed afterwards unless view): the sea hidden, the red ±8 m limit tint
    /// off (it would hide the edits), the green unsaved marks as asked. The note on the locations nearby is closed.
    /// </summary>
    /// <param name="e">The editor.</param>
    /// <param name="view">Leave the View panel open.</param>
    /// <param name="water">Show the sea.</param>
    /// <param name="size">The area's width in zones, around the bare zone.</param>
    /// <param name="unsaved">Show the green marks of unsaved changes.</param>
    /// <param name="slope">Show the slope colours.</param>
    /// <param name="limit">Show the red ±8 m limit marks.</param>
    private void Bare(Editor e, bool view = false, bool water = false, int size = 1, bool unsaved = true, bool slope = false, bool limit = false)
    {
        e.Send($"world {World}");
        e.Send($"area {ZoneX} {ZoneZ} {size}");
        e.CloseLocationNote();
        foreach (var (sw, wanted, byDefault) in new[] { ("Water", water, true), ("Limit marks", limit, true), ("Unsaved marks", unsaved, true), ("Slope colours", slope, false) })
        {
            if (wanted != byDefault)
            {
                e.Send($"click {sw}");
            }
        }
        if (!view)
        {
            e.Send("click View");
        }
        Camera(e, 0, 0, 200, 70, 95 * size);
    }

    /// <summary>
    /// Points the camera at dx, dz from the zone's middle, with yaw and pitch in degrees and distance in metres. At the
    /// usual yaw of 200° a target further east (dx up) moves the ground left on the picture, a target further west
    /// moves it right: a subject at dx 0 sits right of the tool panel with the target at dx 8 to 12.
    /// </summary>
    private static void Camera(Editor e, double dx, double dz, double yaw, double pitch, double distance)
    {
        e.Send($"camera {Cx + dx} {Cz + dz} {yaw} {pitch} {distance}");
    }

    /// <summary>The pointer moved over a ground point, with the modifier keys held.</summary>
    private static void Move(Editor e, (double X, double Z) p, string mods = "")
    {
        e.Send(Cmd($"mouse move {p.X} {p.Z} left {mods}"));
    }

    /// <summary>A mouse drag on the ground from a to b (release false: the button stays down).</summary>
    /// <returns>The state after the release; null when the button stays down.</returns>
    private static void Drag(Editor e, (double X, double Z) a, (double X, double Z) b, int steps = 4, string button = "left", string mods = "", int hold = 30, bool release = true)
    {
        Move(e, a, mods);
        e.Send(Cmd($"mouse down {a.X} {a.Z} {button} {mods}"));
        for (int i = 1; i <= steps; i++)
        {
            double x = a.X + (b.X - a.X) * i / steps;
            double z = a.Z + (b.Z - a.Z) * i / steps;
            e.Send(Cmd($"mouse move {x} {z} {button} {mods}"));
            e.Send($"wait {hold}");
        }
        if (release)
        {
            e.Send(Cmd($"mouse up {b.X} {b.Z} {button} {mods}"));
        }
    }

    /// <summary>A drag through several points (a brush stroke along them).</summary>
    /// <returns>The state after the release; null when the button stays down.</returns>
    private static void Path(Editor e, (double X, double Z)[] points, int steps = 3, int hold = 60, string button = "left", string mods = "", bool release = true)
    {
        Move(e, points[0], mods);
        e.Send(Cmd($"mouse down {points[0].X} {points[0].Z} {button} {mods}"));
        for (int k = 1; k < points.Length; k++)
        {
            var (a, b) = (points[k - 1], points[k]);
            for (int i = 1; i <= steps; i++)
            {
                double x = a.X + (b.X - a.X) * i / steps;
                double z = a.Z + (b.Z - a.Z) * i / steps;
                e.Send(Cmd($"mouse move {x} {z} {button} {mods}"));
                e.Send($"wait {hold}");
            }
        }
        var (endX, endZ) = points[^1];
        if (release)
        {
            e.Send(Cmd($"mouse up {endX} {endZ} {button} {mods}"));
        }
    }

    /// <summary>A left click at a ground point.</summary>
    private static void Click(Editor e, (double X, double Z) p, string mods = "")
    {
        Move(e, p, mods);
        e.Send(Cmd($"mouse down {p.X} {p.Z} left {mods}"));
        e.Send(Cmd($"mouse up {p.X} {p.Z} left {mods}"));
    }

    /// <summary>The button held still at a point for a while (a brush: raise a mound, dig a hole).</summary>
    private static JsonElement Hold(Editor e, (double X, double Z) at, int ms, int steps = 1)
    {
        Move(e, at);
        e.Send($"mouse down {at.X} {at.Z}");
        for (int i = 0; i < Math.Max(1, steps); i++)
        {
            e.Send($"wait {ms / Math.Max(1, steps)}");
        }
        return e.Send($"mouse up {at.X} {at.Z}");
    }

    /// <summary>A brush held still at a point for a while (raise a mound, dig a hole).</summary>
    /// <param name="e">The editor.</param>
    /// <param name="key">The key choosing the brush (D1 is Raise).</param>
    /// <param name="at">Where the brush is held.</param>
    /// <param name="size">The brush size.</param>
    /// <param name="hold">How long the button is held, in milliseconds.</param>
    /// <param name="steps">How many waits the hold is split into (0 counts as 1).</param>
    private static JsonElement Stroke(Editor e, string key, (double X, double Z) at, double size, int hold = 1200, int steps = 0)
    {
        e.Send($"key {key}");
        e.Send($"set Size|{size}");
        return Hold(e, at, hold, steps);
    }

    /// <summary>The Area tool: a box from a to b, the action, Enter.</summary>
    /// <param name="e">The editor.</param>
    /// <param name="a">One corner.</param>
    /// <param name="b">The opposite corner.</param>
    /// <param name="action">The action to choose, by its name in the panel.</param>
    /// <param name="apply">Press Enter; false leaves the action chosen but not done.</param>
    private static void AreaAction(Editor e, (double X, double Z) a, (double X, double Z) b, string action, bool apply = true)
    {
        e.Send("key B");
        e.Send("click Box");
        Drag(e, a, b);
        e.Send($"choose {action}");
        e.Send(apply ? "key Return" : "state");
    }

    /// <summary>The Place tool's kinds: exactly these.</summary>
    private static void Kinds(Editor e, params string[] names)
    {
        e.Send("click Choose kinds…");
        e.Send("click Untick all");
        foreach (string n in names)
        {
            e.Send($"type Search kinds|{n}");
            e.Send($"click {n}");
        }
        e.Send("type Search kinds|");
        e.Send("click Done");
    }

    /// <summary>
    /// Building pieces as the game places them: true size, upright, all facing the rotation (set in Brush mode, the only
    /// one with Tilt; the mode is chosen again afterwards).
    /// </summary>
    private static void Pieces(Editor e)
    {
        e.Send("click Brush");
        e.Send("set Tilt|0");
        e.Send("set Size %|100,100");
        e.Send("set Rotation|0");
        e.Send("check Random facing (off: all face the rotation)|off");
    }

    /// <summary>Trees and rocks as nature has them: sizes, tilts and facings vary.</summary>
    private static void Nature(Editor e)
    {
        e.Send("set Tilt|4");
        e.Send("set Size %|80,120");
    }

    /// <summary>One object of the chosen kinds exactly where clicked (Place, Brush mode).</summary>
    private static void Single(Editor e, (double X, double Z) at)
    {
        e.Send("click Brush");
        e.Send("check One at a time, exactly at the cursor|on");
        Click(e, at);
    }

    /// <summary>Ground levelled at its average height, its edge softened (a change of this scene, not saved).</summary>
    private static void Level(Editor e)
    {
        Level(e, P(-16, -14), P(16, 14));
    }

    /// <summary>The ground of the box from a to b levelled at its average height, its edge softened.</summary>
    private static void Level(Editor e, (double X, double Z) a, (double X, double Z) b, double soft = 8)
    {
        AreaAction(e, a, b, "Flatten", apply: false);
        e.Send($"set Soft edge|{soft}");
        e.Send("click avg");
        e.Send("key Return");
        e.Send("click Clear (Esc)");
    }

    /// <summary>The whole bare zone levelled flat, a stage on which what a tool does stands out.</summary>
    private static void Stage(Editor e)
    {
        Level(e, P(-31, -31), P(31, 31), soft: 0);
    }

    /// <summary>The trees <c>Trees</c> plants unless told otherwise.</summary>
    private static readonly string[] Woods = ["Beech1", "Oak1", "Birch1"];

    /// <summary>A wood planted with the Place brush along a stroke.</summary>
    private static void Trees(Editor e, (double X, double Z) a, (double X, double Z) b, int density = 6, string[]? kinds = null)
    {
        e.Send("key T");
        Kinds(e, kinds ?? Woods);
        e.Send("click Brush");
        e.Send("check One at a time, exactly at the cursor|off");
        Nature(e);
        e.Send("set Brush size|8");
        e.Send($"set Density|{density}");
        Drag(e, a, b, steps: 6, hold: 120);
        e.Send("key Escape");
    }

    /// <summary>A wooden hut: a floor of 2 m pieces with walls end to end around it, this many high.</summary>
    private static void Hut(Editor e, (double X, double Z) a, (double X, double Z) b, int layers = 2)
    {
        e.Send("key T");
        Pieces(e);
        Kinds(e, "wood_floor");
        e.Send("click Grid");
        e.Send("set Spacing|2");
        Drag(e, a, b);
        e.Send("key Return");
        Kinds(e, "woodwall");
        e.Send("click Line");
        e.Send("click Rectangle");
        e.Send($"set Layers|{layers}");
        Drag(e, a, b);
        e.Send("key Return");
    }

    /// <summary>A small wooden house on a floor, a crop field and a fire pit, inside a palisade.</summary>
    private static void BuildBase(Editor e)
    {
        Level(e, P(-18, -18), P(18, 14));
        // The field: cultivated ground.
        e.Send("key D7");
        e.Send("set Size|3");
        e.Send("set Strength|1");
        foreach (int dz in new[] { -14, -11, -8 })
        {
            Path(e, [P(-2, dz), P(14, dz)], steps: 6, hold: 150);
        }
        Hut(e, P(-14, 2), P(-2, 10));
        e.Send("key T");
        // Carrots and turnips in the field.
        Kinds(e, "sapling_carrot", "sapling_turnip");
        e.Send("click Grid");
        e.Send("set Spacing|1");
        Drag(e, P(-1, -15), P(13, -7));
        e.Send("key Return");
        // A fire pit and a chest by the house.
        Kinds(e, "fire_pit");
        Single(e, P(4, 4));
        Kinds(e, "piece_chest_wood");
        Single(e, P(-1, 8));
        e.Send("check One at a time, exactly at the cursor|off");
        // A palisade around it all.
        Kinds(e, "stake_wall");
        e.Send("click Line");
        e.Send("click Circle");
        Drag(e, P(0, 0), P(24, 0));
        e.Send("key Return");
        e.Send("key Escape");
    }

    /// <summary>
    /// Waits (up to 3 minutes) until the start page no longer reads the game's look, then a moment for the page to
    /// settle.
    /// </summary>
    private static void WaitForLook(Editor e)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (e.Send("wait 500").GetProperty("look").GetString() is "" or "running" && watch.Elapsed < TimeSpan.FromMinutes(3))
        {
        }
        e.Send("wait 1500");
    }
}
