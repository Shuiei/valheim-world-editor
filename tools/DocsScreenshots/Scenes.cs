
namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// The documentation's pictures (docs/images), one scene per subject, often a few pictures each (a tool's settings
/// side by side). Each scene gets a fresh editor (Program.cs), builds what its pictures show with the app's own tools
/// on the Docs world's bare zone, takes its pictures and leaves without saving: what a picture shows is only that
/// scene's own changes.
/// </summary>
/// <remarks>
/// The scenes are split by documentation page: the ground tools (Scenes.Ground.cs and Scenes.Shapes.cs), objects
/// (Scenes.Objects.cs), building and dungeons (Scenes.Building.cs), and the pages around the 3D editor (Scenes.Pages.cs).
/// The helpers the scenes share are in Scenes.Input.cs (pointer and key input) and Scenes.Builds.cs (builds and nature). A picture shows
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
            ("select-box", SelectBox),
            ("select-through", SelectThrough),
            ("select-pick", SelectPick),
            ("select-hide", SelectHide),
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

    /// <summary>The whole bare zone levelled flat, a stage on which what a tool does stands out.</summary>
    private static void Stage(Editor e)
    {
        Level(e, P(-31, -31), P(31, 31), soft: 0);
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

