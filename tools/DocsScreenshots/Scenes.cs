using System.Text.Json;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// The documentation's pictures (docs/images), one scene per picture. Each scene gets a fresh editor (Program.cs),
/// builds what its picture shows with the app's own tools on the Docs world's bare zone, takes its pictures and
/// leaves without saving: what a picture shows is only that scene's own changes.
/// </summary>
public sealed class Scenes
{
    /// <summary>The Docs world's bare zone (WorldCheck create --flat), in zone coordinates.</summary>
    private const int ZoneX = -8, ZoneZ = 1;
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
            ("overview", Overview),
            ("unsaved", Unsaved),
            ("sculpt", Sculpt),
            ("paint", Paint),
            ("path", PathTool),
            ("script", Script),
            ("mask", Mask),
            ("measure", Measure),
            ("area", Area),
            ("paste", Paste),
            ("plant-brush", PlantBrush),
            ("plant-line", PlantLine),
            ("plant-grid", PlantGrid),
            ("plant-zone", PlantZone),
            ("select-arrows", SelectArrows),
            ("inspector", Inspector),
            ("select-zone", SelectZone),
            ("history", History),
            ("help", Help),
            ("place-chooser", PlaceChooser),
            ("blueprints", Blueprints),
            ("mountain", Mountain),
            ("workshop", Workshop),
            ("map", Map),
            ("start", Start),
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
    /// off (it would hide the edits), the green unsaved marks as asked.
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
        Camera(e, 0, 0, 200, 70, 95);
    }

    /// <summary>
    /// Points the camera at dx, dz from the zone's middle, with yaw and pitch in degrees and distance in metres.
    /// </summary>
    private static void Camera(Editor e, double dx, double dz, double yaw, double pitch, double distance)
    {
        e.Send($"camera {Cx + dx} {Cz + dz} {yaw} {pitch} {distance}");
    }

    /// <summary>The pointer moved over a ground point, with the modifier keys held.</summary>
    private static JsonElement Move(Editor e, (double X, double Z) p, string mods = "")
    {
        return e.Send(Cmd($"mouse move {p.X} {p.Z} left {mods}"));
    }

    /// <summary>A mouse drag on the ground from a to b (release false: the button stays down).</summary>
    /// <returns>The state after the release; null when the button stays down.</returns>
    private static JsonElement? Drag(Editor e, (double X, double Z) a, (double X, double Z) b, int steps = 4, string button = "left", string mods = "", int hold = 30, bool release = true)
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
        return release ? e.Send(Cmd($"mouse up {b.X} {b.Z} {button} {mods}")) : null;
    }

    /// <summary>A drag through several points (a brush stroke along them).</summary>
    /// <returns>The state after the release; null when the button stays down.</returns>
    private static JsonElement? Path(Editor e, (double X, double Z)[] points, int steps = 3, int hold = 60, string button = "left", string mods = "", bool release = true)
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
        return release ? e.Send(Cmd($"mouse up {endX} {endZ} {button} {mods}")) : null;
    }

    /// <summary>A left click at a ground point.</summary>
    private static JsonElement Click(Editor e, (double X, double Z) p, string mods = "")
    {
        Move(e, p, mods);
        e.Send(Cmd($"mouse down {p.X} {p.Z} left {mods}"));
        return e.Send(Cmd($"mouse up {p.X} {p.Z} left {mods}"));
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
        Move(e, at);
        e.Send($"mouse down {at.X} {at.Z}");
        for (int i = 0; i < Math.Max(1, steps); i++)
        {
            e.Send($"wait {hold / Math.Max(1, steps)}");
        }
        return e.Send($"mouse up {at.X} {at.Z}");
    }

    /// <summary>The Area tool: a box from a to b, the action, Enter.</summary>
    /// <param name="e">The editor.</param>
    /// <param name="a">One corner.</param>
    /// <param name="b">The opposite corner.</param>
    /// <param name="action">The action to choose, by its name in the panel.</param>
    /// <param name="apply">Press Enter; false leaves the action chosen but not done.</param>
    private static JsonElement AreaAction(Editor e, (double X, double Z) a, (double X, double Z) b, string action, bool apply = true)
    {
        e.Send("key B");
        Drag(e, a, b);
        e.Send($"choose {action}");
        return apply ? e.Send("key Return") : e.Send("state");
    }

    /// <summary>The Place tool's kinds: exactly these.</summary>
    private static void Kinds(Editor e, params string[] names)
    {
        e.Send("click + Add kinds");
        e.Send("click Untick all");
        foreach (string n in names)
        {
            e.Send($"type Search kinds|{n}");
            e.Send($"click {n}");
        }
        e.Send("type Search kinds|");
        e.Send("click Done");
    }

    /// <summary>Building pieces as the game places them: true size, upright, all facing the rotation.</summary>
    private static void Pieces(Editor e)
    {
        e.Send("set Tilt|0");
        e.Send("set Size %|100,100");
        e.Send("set Rotation|0");
        e.Send("click Random facing (off: all face the rotation)");
    }

    /// <summary>Trees and rocks as nature has them: sizes, tilts and facings vary.</summary>
    private static void Nature(Editor e)
    {
        e.Send("set Tilt|4");
        e.Send("set Size %|80,120");
    }

    /// <summary>Ground levelled at its average height, its edge softened (a change of this scene, not saved).</summary>
    private static void Level(Editor e)
    {
        Level(e, P(-16, -14), P(16, 14));
    }

    /// <summary>The ground of the box from a to b levelled at its average height, its edge softened.</summary>
    private static void Level(Editor e, (double X, double Z) a, (double X, double Z) b)
    {
        AreaAction(e, a, b, "Flatten", apply: false);
        e.Send("set Soft edge|8");
        e.Send("click avg");
        e.Send("key Return");
        e.Send("click Clear (Esc)");
    }

    /// <summary>The trees <c>Trees</c> plants unless told otherwise.</summary>
    private static readonly string[] Woods = ["Beech1", "Oak1", "Birch1"];

    /// <summary>A wood planted with the Place brush along a stroke.</summary>
    private static void Trees(Editor e, (double X, double Z) a, (double X, double Z) b, int density = 6, string[]? kinds = null)
    {
        e.Send("key T");
        Kinds(e, kinds ?? Woods);
        e.Send("click Brush");
        Nature(e);
        e.Send("set Brush size|8");
        e.Send($"set Density|{density}");
        Drag(e, a, b, steps: 6, hold: 120);
        e.Send("key Escape");
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
        e.Send("key T");
        Pieces(e);
        // The floor: a grid of 2 m floor pieces, then walls around it, end to end, two high.
        Kinds(e, "wood_floor");
        e.Send("click Grid");
        e.Send("set Spacing|2");
        Drag(e, P(-14, 2), P(-2, 10));
        e.Send("key Return");
        Kinds(e, "woodwall");
        e.Send("click Line");
        e.Send("click Rectangle");
        e.Send("set Layers|2");
        Drag(e, P(-14, 2), P(-2, 10));
        e.Send("key Return");
        // Carrots and turnips in the field.
        Kinds(e, "sapling_carrot", "sapling_turnip");
        e.Send("click Grid");
        e.Send("set Spacing|1");
        Drag(e, P(-1, -15), P(13, -7));
        e.Send("key Return");
        // A fire pit and a chest by the house.
        Kinds(e, "fire_pit");
        e.Send("click Brush");
        e.Send("click One at a time, exactly at the cursor");
        Click(e, P(4, 4));
        Kinds(e, "piece_chest_wood");
        Click(e, P(-1, 8));
        e.Send("click One at a time, exactly at the cursor");
        // A palisade around it all.
        Kinds(e, "stake_wall");
        e.Send("click Line");
        e.Send("click Circle");
        Drag(e, P(0, 0), P(24, 0));
        e.Send("key Return");
        e.Send("key Escape");
    }

    // ---- The scenes.

    /// <summary>overview.jpg: the base and a wood behind it, in the 3D editor.</summary>
    private void Overview(Editor e)
    {
        // The new pieces' green marks off: every piece of this base is unsaved, the dots would hide it.
        // The limit marks on, as by default (nothing here reaches the limit).
        Bare(e, view: true, unsaved: false, limit: true);
        BuildBase(e);
        Trees(e, P(-26, 26), P(26, 27));
        e.Send("panel view");
        Camera(e, -2, 2, 20, 30, 60);
        e.Shot(Out("overview.jpg"));
    }

    /// <summary>unsaved.jpg: levelled ground and new trees, with their unsaved marks and the status message.</summary>
    private void Unsaved(Editor e)
    {
        Bare(e);
        Level(e);
        Trees(e, P(-10, 4), P(10, 10), density: 4);
        Camera(e, 0, 0, 20, 40, 48);
        e.Shot(Out("unsaved.jpg"), keepMessage: true);
    }

    /// <summary>sculpt.jpg: a raised mound and the next brush's circle by it.</summary>
    private void Sculpt(Editor e)
    {
        Bare(e);
        Stroke(e, "D1", P(0, 0), 9, hold: 2500, steps: 5);
        e.Send("key D3");
        e.Send("set Size|7");
        Move(e, P(7, -3));
        Camera(e, 0, 0, 200, 38, 40);
        // The brush's circle follows the pointer a frame later.
        e.Send("wait 300");
        e.Shot(Out("sculpt.jpg"));
    }

    /// <summary>paint.jpg: three painted stripes on levelled ground.</summary>
    private void Paint(Editor e)
    {
        Bare(e);
        Level(e);
        foreach (var (key, dz) in new[] { ("D6", 9), ("D7", 0), ("D8", -9) })
        {
            e.Send($"key {key}");
            e.Send("set Size|4");
            e.Send("set Strength|1");
            Path(e, [P(-16, dz), P(16, dz)], steps: 8, hold: 150);
        }
        Move(e, P(20, -9));
        Camera(e, 0, 0, 200, 50, 46);
        e.Shot(Out("paint.jpg"));
    }

    /// <summary>path.jpg: the Path tool's points across two mounds.</summary>
    private void PathTool(Editor e)
    {
        Bare(e);
        Stroke(e, "D1", P(-6, 8), 10, hold: 3000, steps: 5);
        Stroke(e, "D1", P(12, -6), 8, hold: 2500, steps: 5);
        e.Send("key P");
        foreach (var p in new[] { P(-24, -20), P(-10, -6), P(2, 2), P(14, 12), P(24, 24) })
        {
            Click(e, p);
        }
        Camera(e, 0, 0, 200, 40, 62);
        e.Shot(Out("path.jpg"));
    }

    /// <summary>script.jpg: the Terraced hill example script, run on a 3 x 3 area.</summary>
    private void Script(Editor e)
    {
        Bare(e, size: 3);
        e.Send("click Script");
        e.Send("choose Example: Terraced hill");
        e.Send("click Run");
        // Compiling the first script takes a moment.
        e.Send("wait 8000");
        Camera(e, 0, 0, 200, 34, 210);
        e.Send("wait 1500");
        e.Shot(Out("script.jpg"), keepMessage: true);
    }

    /// <summary>
    /// mask.jpg: the Mask limiting Raise to Meadows below 64 m and under 25°: the stroke across a steep mound raises
    /// the flat ground on both sides and leaves the mound's steep sides as they were.
    /// </summary>
    private void Mask(Editor e)
    {
        Bare(e);
        Stroke(e, "D1", P(0, 2), 6, hold: 4000, steps: 6);
        e.Send("click Mask");
        e.Send("click Meadows");
        e.Send("set Height|,64");
        e.Send("set Slope|,25");
        e.Send("key D1");
        e.Send("set Size|9");
        e.Send("set Strength|0.6");
        Path(e, [P(-26, 2), P(26, 2)], steps: 12, hold: 150);
        Move(e, P(20, 2));
        Camera(e, 0, 0, 20, 30, 46);
        e.Shot(Out("mask.jpg"));
    }

    /// <summary>measure.jpg: a measure across a mound, with the slope colours on.</summary>
    private void Measure(Editor e)
    {
        Bare(e, slope: true);
        Stroke(e, "D1", P(0, 0), 10, hold: 2200, steps: 6);
        e.Send("key M");
        Click(e, P(-20, -6));
        Click(e, P(16, 6));
        Camera(e, 0, 0, 200, 35, 48);
        e.Shot(Out("measure.jpg"));
    }

    /// <summary>area.jpg: the Area tool's Remove objects chosen on a box of trees and rocks, not yet done.</summary>
    private void Area(Editor e)
    {
        Bare(e, unsaved: false);
        Trees(e, P(-22, -10), P(22, 12), density: 5, kinds: ["Beech1", "Birch1", "Rock_3", "Bush01"]);
        AreaAction(e, P(-14, -12), P(10, 10), "Remove objects", apply: false);
        Camera(e, 0, 0, 200, 50, 55);
        e.Shot(Out("area.jpg"));
    }

    /// <summary>paste.jpg: a copied wood being pasted (one copy).</summary>
    private void Paste(Editor e)
    {
        Bare(e, unsaved: false);
        Trees(e, P(-24, -18), P(-12, -8), density: 8);
        e.Send("key B");
        Drag(e, P(-27, -21), P(-9, -5));
        e.Send("key C ctrl");
        e.Send("key V ctrl");
        // One copy: the caption says "a copy", and more 22 m copies run off the 64 m zone.
        e.Send("set Copies|1");
        for (int i = 0; i < 2; i++)
        {
            e.Send("key OemComma shift");
        }
        Move(e, P(10, 10));
        Camera(e, 0, 0, 200, 62, 95);
        e.Shot(Out("paste.jpg"));
    }

    /// <summary>plant-brush.jpg: a wood planted with the Place brush and its preview under the pointer.</summary>
    private void PlantBrush(Editor e)
    {
        Bare(e, unsaved: false);
        e.Send("key T");
        e.Send("choose Meadows woods");
        Nature(e);
        e.Send("click Brush");
        e.Send("set Brush size|10");
        Drag(e, P(-26, 18), P(-6, 22), steps: 6, hold: 150);
        // Far enough that the preview (a beech is about 20 m tall) does not fill the picture.
        Camera(e, 2, 2, 20, 58, 105);
        Move(e, P(8, -8));
        e.Send("wait 500");
        Move(e, P(8, -7));
        e.Shot(Out("plant-brush.jpg"));
    }

    /// <summary>plant-line.jpg: a fence being placed along a line through points.</summary>
    private void PlantLine(Editor e)
    {
        Bare(e);
        Level(e);
        e.Send("key T");
        Pieces(e);
        Kinds(e, "wood_fence");
        e.Send("click Line");
        e.Send("click Points");
        foreach (var p in new[] { P(-22, -12), P(-6, -2), P(8, -6), P(20, 10) })
        {
            Click(e, p);
        }
        Move(e, P(20, 10));
        Camera(e, 0, 0, 200, 40, 48);
        e.Shot(Out("plant-line.jpg"));
    }

    /// <summary>plant-grid.jpg: carrots and turnips being placed on a turned grid over cultivated ground.</summary>
    private void PlantGrid(Editor e)
    {
        Bare(e);
        Level(e);
        e.Send("key D7");
        e.Send("set Size|4");
        e.Send("set Strength|1");
        for (int dz = -12; dz < 13; dz += 4)
        {
            Path(e, [P(-16, dz), P(16, dz)], steps: 6, hold: 150);
        }
        e.Send("key T");
        Pieces(e);
        Kinds(e, "sapling_carrot", "sapling_turnip");
        e.Send("click Grid");
        e.Send("set Spacing|1.5");
        e.Send("set Rotation|15");
        Drag(e, P(-12, -9), P(12, 9));
        Camera(e, 0, 0, 200, 45, 42);
        e.Shot(Out("plant-grid.jpg"));
    }

    /// <summary>plant-zone.jpg: bushes and young trees for a zone drawn point by point.</summary>
    private void PlantZone(Editor e)
    {
        Bare(e);
        e.Send("key T");
        Nature(e);
        Kinds(e, "Beech_small1", "Bush01", "RaspberryBush");
        e.Send("click Zone");
        foreach (var p in new[] { P(-20, -14), P(-4, -20), P(16, -12), P(22, 6), P(6, 18), P(-14, 14) })
        {
            Click(e, p);
        }
        Camera(e, 0, 0, 20, 50, 58);
        e.Shot(Out("plant-zone.jpg"));
    }

    /// <summary>A chest on levelled ground, selected.</summary>
    private static void Chest(Editor e)
    {
        Level(e);
        e.Send("key T");
        Pieces(e);
        Kinds(e, "piece_chest_wood");
        e.Send("click Brush");
        e.Send("click One at a time, exactly at the cursor");
        Click(e, P(0, 0));
        e.Send("key E");
        Click(e, P(0, 0));
    }

    /// <summary>select-arrows.jpg: a selected rock and its arrows.</summary>
    private void SelectArrows(Editor e)
    {
        // A rock, as the caption says (the chest is the Inspector's picture).
        Bare(e);
        Level(e);
        e.Send("key T");
        Nature(e);
        Kinds(e, "Rock_3");
        e.Send("click Brush");
        e.Send("click One at a time, exactly at the cursor");
        Click(e, P(0, 0));
        e.Send("key E");
        Click(e, P(0, 0));
        Camera(e, 0, 0, 200, 35, 14);
        e.Shot(Out("select-arrows.jpg"));
    }

    /// <summary>inspector.jpg: a selected chest in the Inspector.</summary>
    private void Inspector(Editor e)
    {
        Bare(e);
        Chest(e);
        e.Send("key I");
        Camera(e, 0, 0, 200, 35, 10);
        e.Shot(Out("inspector.jpg"));
    }

    /// <summary>
    /// select-zone-drawing.jpg and select-zone.jpg: a selection ring drawn with Alt around rocks and bushes, then the
    /// selection it made.
    /// </summary>
    private void SelectZone(Editor e)
    {
        Bare(e, unsaved: false);
        Trees(e, P(-20, -8), P(20, 8), density: 6, kinds: ["Rock_3", "Bush01", "Beech_small1", "RaspberryBush"]);
        e.Send("key E");
        (double X, double Z)[] ring = [P(-12, -10), P(6, -12), P(14, -2), P(10, 10), P(-6, 12), P(-14, 2), P(-12, -9)];
        Path(e, ring, steps: 2, hold: 30, mods: "alt", release: false);
        Camera(e, 0, 0, 200, 48, 36);
        e.Shot(Out("select-zone-drawing.jpg"));
        e.Send($"mouse up {ring[^1].X} {ring[^1].Z} left alt");
        e.Send("wait 300");
        e.Shot(Out("select-zone.jpg"), keepMessage: true);
    }

    /// <summary>history.jpg: the History panel after a few edits.</summary>
    private void History(Editor e)
    {
        Bare(e);
        Stroke(e, "D1", P(-8, 0), 8, hold: 1200, steps: 3);
        Stroke(e, "D1", P(8, 4), 8, hold: 1200, steps: 3);
        Stroke(e, "D2", P(0, -10), 6, hold: 1000, steps: 3);
        e.Send("key D6");
        e.Send("set Size|3");
        Path(e, [P(-16, 12), P(16, 12)], steps: 6);
        Stroke(e, "D4", P(0, 0), 10, hold: 1500, steps: 3);
        e.Send("key Escape");
        e.Send("click History");
        e.Send("click Back to here");
        Camera(e, 0, 0, 200, 40, 55);
        e.Shot(Out("history.jpg"));
    }

    /// <summary>help.jpg: the help (F3) over the bare zone.</summary>
    private void Help(Editor e)
    {
        Bare(e);
        e.Send("key F3");
        Camera(e, 0, 0, 200, 40, 55);
        e.Shot(Out("help.jpg"));
    }

    /// <summary>place-chooser.jpg: the Place tool's kinds chooser.</summary>
    private void PlaceChooser(Editor e)
    {
        Bare(e);
        e.Send("key T");
        e.Send("click + Add kinds");
        Camera(e, 0, 0, 200, 40, 55);
        e.Shot(Out("place-chooser.jpg"));
    }

    /// <summary>blueprints.jpg: a small hut saved as a blueprint, in the Blueprints panel.</summary>
    private void Blueprints(Editor e)
    {
        Bare(e, unsaved: false);
        Level(e, P(-12, -10), P(12, 10));
        e.Send("key T");
        Pieces(e);
        Kinds(e, "wood_floor");
        e.Send("click Grid");
        e.Send("set Spacing|2");
        Drag(e, P(-4, -3), P(4, 3));
        e.Send("key Return");
        Kinds(e, "woodwall");
        e.Send("click Line");
        e.Send("click Rectangle");
        e.Send("set Layers|2");
        Drag(e, P(-4, -3), P(4, 3));
        e.Send("key Return");
        e.Send("key B");
        Drag(e, P(-7, -6), P(7, 6));
        e.Send("choose Copy and paste");
        e.Send("key C ctrl");
        e.Send("click Save blueprint…");
        e.Send("wait 500");
        e.Send("type |Small hut");
        e.Send("click Save");
        e.Send("wait 500");
        e.Send("click Blueprints…");
        Camera(e, 0, 0, 200, 40, 55);
        e.Shot(Out("blueprints.jpg"), keepMessage: true);
    }

    /// <summary>mountain.jpg: a lone peak made with the Mountain tool, past the game's ±8 m.</summary>
    private void Mountain(Editor e)
    {
        // A lone peak past the game's +-8 m, in a 3 x 3 area so it has room.
        Bare(e, size: 3, unsaved: false);
        e.Send("click Mountain");
        e.Send("choose Lone peak");
        e.Send("set Height|80");
        e.Send("set Rough|0.3");
        e.Send("click Grow the biome's trees and rocks on it");
        Camera(e, 0, 0, 200, 35, 260);
        Click(e, P(0, 0));
        e.Send("wait 1500");
        // Back to View: the preview under the pointer would hide the mountain.
        e.Send("key Escape");
        Camera(e, 0, 0, 210, 32, 220);
        e.Shot(Out("mountain.jpg"), keepMessage: true);
    }

    /// <summary>workshop.jpg: a small house kept as a blueprint, opened in the Workshop.</summary>
    private void Workshop(Editor e)
    {
        // A small house built with the Place tool in a world, kept as a blueprint, opened in the Workshop:
        // its pieces tinted by the support check, as the game's build mode shows them.
        Bare(e, unsaved: false);
        Level(e, P(-12, -10), P(12, 10));
        e.Send("key T");
        Pieces(e);
        Kinds(e, "wood_floor");
        e.Send("click Grid");
        e.Send("set Spacing|2");
        Drag(e, P(-4, -3), P(4, 3));
        e.Send("key Return");
        Kinds(e, "woodwall");
        e.Send("click Line");
        e.Send("click Rectangle");
        e.Send("set Layers|3");
        Drag(e, P(-4, -3), P(4, 3));
        e.Send("key Return");
        e.Send("key B");
        Drag(e, P(-7, -6), P(7, 6));
        e.Send("choose Copy and paste");
        e.Send("key C ctrl");
        e.Send("click Save blueprint…");
        e.Send("wait 500");
        e.Send("type |Small house");
        e.Send("click Save");
        e.Send("wait 500");
        e.Send("click Blueprints");
        e.Send("click Edit");
        e.Send("wait 500");
        e.Send("click Leave anyway");
        e.Send("wait 2500");
        e.Send("key T");
        // The Workshop's plot is centred on 0, 0 (not the world's zone).
        e.Send("camera 0 0 30 28 24");
        e.Shot(Out("workshop.jpg"));
    }

    /// <summary>map.jpg: the map view with the bare zone picked.</summary>
    private void Map(Editor e)
    {
        e.Send($"world {World}");
        e.Send($"mapview {Cx + 200} {Cz} 1.6");
        e.Send($"mappick {ZoneX} {ZoneZ}");
        e.Shot(Out("map.jpg"));
    }

    // ---- The start page and its settings (no world opened).

    /// <summary>
    /// The start page's pictures (start-home, start-game, start-server, start-offline, start-workshop) and
    /// settings.jpg; no world is opened.
    /// </summary>
    private void Start(Editor e)
    {
        // Home: the running game (the stand-in plugin) and the worlds opened before.
        e.Send("wait 3000");
        e.Shot(Out("start-home.jpg"));
        e.Send("click My game");
        e.Send("wait 1500");
        e.Shot(Out("start-game.jpg"));
        e.Send("click A dedicated server");
        e.Shot(Out("start-server.jpg"));
        e.Send("click A saved world");
        e.Send("wait 1500");
        e.Shot(Out("start-offline.jpg"));
        e.Send("click The Workshop");
        e.Send("wait 800");
        e.Shot(Out("start-workshop.jpg"));
        e.Send("click Settings");
        e.Send("wait 800");
        e.Shot(Out("settings.jpg"));
    }
}
