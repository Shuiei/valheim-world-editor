using System.Globalization;
using SkiaSharp;

namespace TerrainEditor.DocsScreenshots;

/// <summary>The building pictures: blueprints, the Workshop and dungeons.</summary>
public sealed partial class Scenes
{
    /// <summary>
    /// A building piece of a blueprint a scene writes: its prefab, its middle in metres from the anchor (the plot's
    /// middle at ground level), and whether it is turned a quarter round (a wall running north to south).
    /// </summary>
    private readonly record struct Part(string Prefab, double X, double Y, double Z, bool Turned = false);

    /// <summary>
    /// A square ring of walls round the anchor, each <paramref name="width"/> m wide, their faces
    /// <paramref name="half"/> m out and their middles at height <paramref name="y"/>; no wall where a gap is.
    /// </summary>
    private static IEnumerable<Part> Ring(string prefab, double half, double width, double y, params (double X, double Z)[] gaps)
    {
        for (double a = -half + width / 2; a < half; a += width)
        {
            foreach (double side in new[] { -half, half })
            {
                if (!gaps.Contains((a, side)))
                {
                    yield return new Part(prefab, a, y, side);
                }
                if (!gaps.Contains((side, a)))
                {
                    yield return new Part(prefab, side, y, a, Turned: true);
                }
            }
        }
    }

    /// <summary>
    /// Writes a Homestead blueprint of these pieces (the format docs/workshop.md describes), as a player gets one from
    /// a friend, and returns its path. Its one terrain contact, at the anchor, says the building stood on flat ground:
    /// without one the Workshop takes each piece's lowest point as the ground it stood on, and every overhang would
    /// count as standing on the ground.
    /// </summary>
    /// <param name="name">The blueprint's name (its file's too).</param>
    /// <param name="parts">Its pieces.</param>
    private static string WriteBlueprint(string name, IEnumerable<Part> parts)
    {
        var inv = CultureInfo.InvariantCulture;
        var lines = new List<string>
        {
            "#Name:" + name,
            "#Creator:Docs",
            "#Description:",
            "#Category:Blueprints",
            "#HomesteadVersion:1",
            "#HomesteadTerrainContact:0;0;0",
            "#Pieces",
        };
        foreach (var p in parts)
        {
            string turn = p.Turned ? "0;0.7071068;0;0.7071068" : "0;0;0;1";
            lines.Add(string.Create(inv, $"{p.Prefab};Building;{p.X};{p.Y};{p.Z};{turn};\"\";1;1;1"));
        }
        string dir = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vwe-docs-blueprints")).FullName;
        string file = System.IO.Path.Combine(dir, name.Replace(' ', '_') + ".blueprint");
        File.WriteAllLines(file, lines);
        return file;
    }

    /// <summary>A wooden hut 6 m across, a doorway on its south side, a fire pit and a chest inside.</summary>
    private static string SmallHut() => WriteBlueprint("Small hut",
    [
        .. Ring("woodwall", 3, 2, 1, (0, -3)),
        new Part("fire_pit", 0, 0, 0),
        new Part("piece_chest_wood", 1.5, 0, 1.5),
    ]);

    /// <summary>A hut on a stone footing a metre high, wooden walls above, a bed and a chest inside.</summary>
    private static string StoneHut() => WriteBlueprint("Stone hut",
    [
        .. Ring("stone_wall_2x1", 3, 2, 0.5),
        .. Ring("woodwall", 3, 2, 2, (0, -3)),
        new Part("bed", -1, 0, 1),
        new Part("piece_chest_wood", 1, 0, 1),
    ]);

    /// <summary>
    /// A tall house 8 m across: a stone ground floor 2 m high, then six rows of wooden walls (three storeys), a fire
    /// pit, a bed, a table and a chest on its ground floor, and on its top row an overhang of three walls reaching
    /// out 6 m to the east, which the support check shows orange, then red, the last one falling.
    /// </summary>
    private static string TallHouse()
    {
        var parts = new List<Part>(Ring("stone_wall_4x2", 4, 4, 1));
        for (int row = 0; row < 6; row++)
        {
            parts.AddRange(Ring("woodwall", 4, 2, 3 + 2 * row));
        }
        parts.AddRange([new("woodwall", 5, 13, -4), new("woodwall", 7, 13, -4), new("woodwall", 9, 13, -4)]);
        parts.AddRange([new("fire_pit", 0, 0, 0), new("bed", 2, 0, 2), new("piece_table", -2, 0, 2), new("piece_chest_wood", 2, 0, -2)]);
        return WriteBlueprint("Tall house", parts);
    }

    /// <summary>
    /// The Save blueprint dialog (Ctrl+S) filled with this description and these tags, then saved under the name it
    /// shows; the picture is taken first when <paramref name="shot"/> is given.
    /// </summary>
    private static void SaveBlueprint(Editor e, string description, string tags, string? shot = null)
    {
        e.Send("key S ctrl");
        e.Send("wait 500");
        e.Send($"type What it is|{description}");
        e.Send($"type house, viking|{tags}");
        if (shot != null)
        {
            e.Shot(shot);
        }
        e.Send("click Save");
        e.Send("wait 1000");
    }

    /// <summary>The Build panel's Pieces tab on its Building tab, its list scrolled down to the wooden pieces.</summary>
    private static void WoodPieces(Editor e)
    {
        e.Send("click Pieces");
        e.Send("click Building");
        e.Send("wait 500");
        e.Scroll(270, 400, -60);
        e.Scroll(270, 400, 29);
    }

    /// <summary>
    /// The camera on x, z of the plot (yaw, pitch and distance as for <see cref="Camera"/>), and the pointer on the plot
    /// at gx, gz, a point hidden behind the Build panel, so the piece that follows the pointer is not in the picture.
    /// </summary>
    private static void PlotCamera(Editor e, double x, double z, double yaw, double pitch, double distance, double gx, double gz)
    {
        var inv = CultureInfo.InvariantCulture;
        e.Send(string.Create(inv, $"camera {x} {z} {yaw} {pitch} {distance}"));
        e.Send(string.Create(inv, $"mouse move {gx} {gz}"));
        e.Send("wait 500");
    }

    /// <summary>A hut on levelled ground, copied with the Area tool and saved as a blueprint under this name.</summary>
    private static void SaveHut(Editor e, string name, int layers)
    {
        Level(e, P(-12, -10), P(12, 10));
        Hut(e, P(-4, -3), P(4, 3), layers);
        e.Send("key B");
        e.Send("click Box");
        Drag(e, P(-7, -6), P(7, 6));
        e.Send("choose Copy and paste");
        e.Send("key C ctrl");
        e.Send("click Save blueprint…");
        e.Send("wait 500");
        e.Send($"type |{name}");
        e.Send("click Save");
        e.Send("wait 500");
    }

    /// <summary>
    /// blueprints.jpg (a small hut saved as a blueprint, in the Blueprints panel) and guide-paste-blueprint.jpg (its
    /// Paste clicked: the hut follows the cursor see-through over the flat meadow, turned 45°).
    /// </summary>
    private void Blueprints(Editor e)
    {
        Bare(e, unsaved: false);
        SaveHut(e, "Small hut", 2);
        e.Send("click Blueprints…");
        Camera(e, 10, 0, 200, 40, 40);
        e.Shot(Out("blueprints.jpg"), keepMessage: true);
        e.Send("click Paste");
        e.Send("wait 500");
        e.Send("check Clear the site (dig the ground in its way, take away trees and rocks)|off");
        // 45°: three steps of 15° (Shift and the turn key).
        for (int i = 0; i < 3; i++)
        {
            e.Send("key OemComma shift");
        }
        e.Send("panel none");
        // Away from the hut it was saved from, over the open meadow.
        Camera(e, -10, 18, 200, 40, 36);
        Move(e, P(-20, 18));
        e.Send("wait 800");
        e.Shot(Out("guide-paste-blueprint.jpg"));
    }

    /// <summary>
    /// The Workshop's pictures, with blueprints made as a player has them from friends (files opened in the Workshop,
    /// then saved to the library): workshop-save-dialog.jpg (the Save blueprint dialog over a small hut),
    /// workshop-overview.jpg (the hut on the plot, the Build panel on the wooden pieces), workshop.jpg (a tall stone
    /// and wood house tinted by the support check, its overhang red), workshop-library.jpg (the Library searching
    /// "hut": two huts with their pictures), workshop-cut.jpg (the house cut at 3 m, seen from above) and
    /// start-workshop.jpg (the start page's Workshop with the three blueprints).
    /// </summary>
    private void Workshop(Editor e)
    {
        e.Send($"workshop {SmallHut()}");
        e.Send("wait 1500");
        WoodPieces(e);
        // The hut right of where the dialog stands.
        PlotCamera(e, 9, 0, 200, 35, 24, 28, 0);
        SaveBlueprint(e, "A hut with a fire pit", "house, wood", Out("workshop-save-dialog.jpg"));
        PlotCamera(e, 4, 0, 200, 40, 20, 18, 0);
        e.Shot(Out("workshop-overview.jpg"));
        e.Send($"workshop {StoneHut()}");
        e.Send("wait 1500");
        SaveBlueprint(e, "A stone footing, a bed and a chest", "house, stone");
        e.Send($"workshop {TallHouse()}");
        e.Send("wait 2000");
        // Some pieces would fall: Save blueprint asks first.
        e.Send("key S ctrl");
        e.Send("wait 500");
        e.Send("click Save anyway");
        e.Send("wait 500");
        e.Send("type What it is|Stone below, three wooden storeys above");
        e.Send("type house, viking|house, stone, wood");
        e.Send("click Save");
        e.Send("wait 1000");
        WoodPieces(e);
        PlotCamera(e, 4, 2, 30, 12, 40, -30, -10);
        e.Shot(Out("workshop.jpg"));
        e.Send("click Library");
        e.Send("type Search blueprints|hut");
        e.Send("wait 800");
        e.Shot(Out("workshop-library.jpg"));
        WoodPieces(e);
        e.Send("set Cut: off|3");
        PlotCamera(e, 6, 0, 200, 55, 26, 26, 0);
        e.Shot(Out("workshop-cut.jpg"));
        e.Send("click Start");
        e.Send("wait 1500");
        e.Send("click The Workshop");
        e.Send("wait 1000");
        e.Shot(Out("start-workshop.jpg"));
    }

    /// <summary>
    /// concept-support-stone-wood.jpg: the support check on two builds side by side, seen from the side: left, three
    /// stone walls stacked (light blue, then green) with a stone wall reaching 2 m out from the top, red and falling;
    /// right, two wooden walls stacked with three more hanging out sideways from the upper one, green, then yellow,
    /// then orange.
    /// </summary>
    private void SupportStoneWood(Editor e)
    {
        string file = WriteBlueprint("Stone and wood",
        [
            new("stone_wall_2x1", 6, 0.5, 0),
            new("stone_wall_2x1", 6, 1.5, 0),
            new("stone_wall_2x1", 6, 2.5, 0),
            new("stone_wall_2x1", 4, 2.5, 0),
            new("woodwall", -5, 1, 0),
            new("woodwall", -5, 3, 0),
            new("woodwall", -7, 3, 0),
            new("woodwall", -9, 3, 0),
            new("woodwall", -11, 3, 0),
        ]);
        e.Send($"workshop {file}");
        e.Send("wait 1500");
        // View: the Build panel closed, nothing in front of the builds.
        e.Send("key Escape");
        e.Send("camera -2.5 0 180 8 15 36");
        e.Send("wait 800");
        e.Shot(Out("concept-support-stone-wood.jpg"));
    }

    /// <summary>
    /// workshop-dungeon.jpg: a dungeon of one level made of building pieces by the generator, opened on the
    /// Workshop's plot and cut at 3 m: its entrance hall with the portal where players arrive, and the rooms beyond,
    /// the Build panel back on its Pieces tab. The picture leaves out the top bar, which a dungeon's long cost line
    /// pushes past the window's edge.
    /// </summary>
    private void WorkshopDungeon(Editor e)
    {
        WaitForLook(e);
        e.Send("workshop");
        e.Send("wait 1500");
        e.Send("click Library");
        e.Send("wait 500");
        e.Send("click Generate dungeon…");
        e.Send("wait 500");
        e.Send("choose Black Forest");
        e.Send("choose Medium");
        e.Send("choose 1 level");
        e.Send("wait 1500");
        e.Send("click Open on the plot");
        e.Send("wait 5000");
        WoodPieces(e);
        e.Send("set Cut: off|3");
        // Above the entrance hall: its portal stands at 11, -50 (the same seed makes the same dungeon).
        PlotCamera(e, 15, -38, 200, 58, 52, 48, -40);
        e.Send("wait 1000");
        string full = Out("workshop-dungeon-full.jpg");
        e.Shot(full);
        Crop(full, Out("workshop-dungeon.jpg"), new SKRectI(0, 64, 1440, 900));
        File.Delete(full);
    }

    /// <summary>A part of a picture saved as a picture of its own.</summary>
    private static void Crop(string from, string to, SKRectI part)
    {
        using var shot = SKImage.FromEncodedData(from) ?? throw new InvalidOperationException($"{from}: not a picture");
        using var result = new SKBitmap(part.Width, part.Height);
        using (var canvas = new SKCanvas(result))
        {
            canvas.DrawImage(shot, part, new SKRect(0, 0, part.Width, part.Height), new SKSamplingOptions(SKFilterMode.Nearest));
        }
        SaveJpeg(result, to);
    }

    /// <summary>A picture saved as a JPEG, as the editor's shots are.</summary>
    private static void SaveJpeg(SKBitmap picture, string path)
    {
        using var jpeg = picture.Encode(SKEncodedImageFormat.Jpeg, 85);
        using var file = File.Create(path);
        jpeg.SaveTo(file);
    }

    /// <summary>
    /// The generator's picture: the window with the Dungeon panel at its top (the heading and the choices), and over
    /// the 3D view, the plan box of each level (with the Level buttons) and the summary, taken from the same panel
    /// scrolled down and drawn 1.7 times larger, so its colours can be told apart.
    /// </summary>
    /// <param name="top">The window with the panel at its top.</param>
    /// <param name="levels">The window with the panel scrolled to the plan, one per level shown.</param>
    /// <param name="to">The picture to write.</param>
    private static void PlanPicture(string top, IReadOnlyList<string> levels, string to)
    {
        // The plan box and the Level buttons under it; the summary under those (window pixels).
        var plan = new SKRectI(108, 376, 390, 600);
        var summary = new SKRectI(108, 600, 390, 652);
        const float Scale = 1.6f, Left = 456, Top = 120, Gap = 24;
        using var window = SKBitmap.Decode(top) ?? throw new InvalidOperationException($"{top}: not a picture");
        using (var canvas = new SKCanvas(window))
        {
            using var back = new SKPaint { Color = new SKColor(22, 25, 31) };
            using var frame = new SKPaint { Color = new SKColor(70, 76, 88), Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
            float width = plan.Width * Scale, height = plan.Height * Scale;
            var all = new SKRect(Left - Gap, Top - Gap, Left + levels.Count * (width + Gap), Top + height + summary.Height * Scale + 2 * Gap);
            canvas.DrawRoundRect(all, 8, 8, back);
            canvas.DrawRoundRect(all, 8, 8, frame);
            for (int i = 0; i < levels.Count; i++)
            {
                using var level = SKImage.FromEncodedData(levels[i]) ?? throw new InvalidOperationException($"{levels[i]}: not a picture");
                float x = Left + i * (width + Gap);
                canvas.DrawImage(level, plan, new SKRect(x, Top, x + width, Top + height), new SKSamplingOptions(SKFilterMode.Linear));
                if (i == 0)
                {
                    canvas.DrawImage(level, summary, new SKRect(x, Top + height, x + summary.Width * Scale, Top + height + summary.Height * Scale), new SKSamplingOptions(SKFilterMode.Linear));
                }
            }
        }
        SaveJpeg(window, to);
    }

    /// <summary>
    /// dungeon-generate.jpg (the Dungeon tool's generator: its heading and choices, and its plan of both levels drawn
    /// larger), dungeon-entrance.jpg (the entrance the game would put, placed on the ground, its door facing the
    /// camera) and dungeon-rooms.jpg (a dungeon of the game's own rooms, 5000 m up, cut 2 m above the floor, with the
    /// panel's room editing: the rooms list, doors, Turn, Delete room, Close open ends).
    /// </summary>
    private void Dungeon(Editor e)
    {
        Bare(e, unsaved: false);
        e.Send("click Dungeon");
        e.Send("choose Black Forest");
        e.Send("choose Medium");
        e.Send("wait 1500");
        Camera(e, 0, 0, 200, 40, 60);
        string top = Out("dungeon-generate-top.jpg"), one = Out("dungeon-generate-1.jpg"), two = Out("dungeon-generate-2.jpg");
        e.Shot(top);
        // Down to the plan, under the choices.
        e.Scroll(250, 600, 15);
        e.Shot(one);
        e.Send("click Level 2");
        e.Send("wait 300");
        e.Shot(two);
        PlanPicture(top, [one, two], Out("dungeon-generate.jpg"));
        foreach (string f in new[] { top, one, two })
        {
            File.Delete(f);
        }
        e.Send("click Level 1");
        e.Send("click Place in the world");
        e.Send("wait 4000");
        // No spawn point markers or birds in front of the entrance.
        e.Send("click View");
        e.Send("click Spawn points");
        e.Send("click Creatures & NPCs");
        e.Send("click View");
        // The summary and the Go to buttons in the panel, the line under them below its edge.
        e.Scroll(250, 500, -2);
        Camera(e, 3, 1, 200, 10, 17);
        e.Send("wait 1000");
        e.Shot(Out("dungeon-entrance.jpg"));
        e.Send("click View");
        e.Send("click Spawn points");
        e.Send("click Creatures & NPCs");
        e.Send("click View");
        // The game's own rooms: a dungeon the game lays out, which the panel edits room by room (placed where the view
        // looks: the same spot, the same rooms).
        Camera(e, 8, 0, 200, 30, 34);
        e.Send("key Z ctrl");
        e.Scroll(250, 400, -30);
        e.Send("choose The game's own rooms");
        e.Send("wait 1500");
        e.Send("click Place in the world");
        e.Send("wait 4000");
        e.Send("click Go to the dungeon");
        e.Send("wait 3000");
        e.Scroll(250, 600, 30);
        // Go inside frames the dungeon's rooms (wherever its layout put them) and cuts their roofs at 4 m.
        e.Send("click Go inside");
        e.Send("wait 1500");
        e.Send("set Cut 4 m above the floor|2");
        // Closer, with the wheel over the rooms in the middle of the view (Go inside frames them from afar).
        e.Scroll(730, 470, -4);
        // Off the rooms (onto the sky), so none is marked as hovered.
        e.Point(1300, 150);
        e.Send("wait 1500");
        e.Shot(Out("dungeon-rooms.jpg"));
    }

    /// <summary>
    /// dungeon-inside.jpg (a Frost Cave of the game's own rooms, 5000 m up, after Go inside: cut 4 m above the floor,
    /// one room deleted so its neighbours' open ends show green, the doors' squares orange and blue) and
    /// dungeon-add-room.jpg (an ice corridor picked in ROOMS, shown see-through at an open end under the cursor).
    /// </summary>
    private void DungeonFrost(Editor e)
    {
        Bare(e, unsaved: false);
        e.Send("click Dungeon");
        e.Send("choose The game's own rooms");
        e.Send("choose Mountain");
        e.Send("wait 2000");
        Camera(e, 0, 0, 200, 40, 60);
        e.Send("click Place in the world");
        e.Send("wait 5000");
        e.Send("click Go to the dungeon");
        e.Send("wait 3000");
        e.Scroll(250, 600, 30);
        e.Send("click Go inside");
        e.Send("wait 2000");
        // The same seed lays out the same rooms: an end cap, then the slope room it closes, deleted where they lie.
        foreach (var (camera, x, z) in new[] { ("", -636, -350), ("camera -650 -350 30 85 75 5035", -650, -320) })
        {
            if (camera.Length > 0)
            {
                e.Send(camera);
                e.Send("wait 500");
            }
            e.Send($"mouse3 move {x} 5000 {z}");
            e.Send($"mouse3 down {x} 5000 {z}");
            e.Send($"mouse3 up {x} 5000 {z}");
            e.Send("click Delete room (Del)");
            e.Send("wait 800");
        }
        e.Send("camera -648 -342 30 70 82 5035");
        e.Point(1350, 820);
        e.Send("wait 800");
        e.Shot(Out("dungeon-inside.jpg"));
        // An ice corridor, held at the open end where the slope room was.
        e.Send("camera -650 -350 30 85 75 5035");
        e.Send("type Find a room|icecorridor");
        e.Send("wait 500");
        ClickWindow(e, 182, 487);
        e.Point(765, 335);
        e.Send("wait 800");
        e.Shot(Out("dungeon-add-room.jpg"));
    }
}
