using SkiaSharp;

namespace TerrainEditor.DocsScreenshots;

/// <summary>The shaping tools' pictures: Shape, Mountain and the Path tool's roads, ramps, rivers and caves.</summary>
public sealed partial class Scenes
{
    /// <summary>
    /// shape.jpg: the Shape tool's presets at the default radius of 16 m, front row Mound, Cone, Mesa, back row
    /// Crater, Moat (ring ditch), Ridged hill, each named on the picture, the slope colours on so their slopes read;
    /// the panel shows the Mound and its formula, its ring round the mound.
    /// </summary>
    private void ShapeTool(Editor e)
    {
        OpenGround(e, 5, P(-130, -130), P(130, 130), slope: true);
        Level(e, P(-80, -60), P(80, 60), soft: 10);
        e.Send("click Shape");
        e.Send("set Radius|16");
        // East is left on the picture: the front row (north) reads Mound, Cone, Mesa from the left.
        (string Shape, double Dx, double Dz)[] shapes =
        [
            ("Mound", 40, 20), ("Cone", 0, 20), ("Mesa", -40, 20),
            ("Crater", 40, -22), ("Moat (ring ditch)", 0, -22), ("Ridged hill", -40, -22),
        ];
        Camera(e, 26, -2, 200, 30, 150);
        foreach (var (shape, dx, dz) in shapes)
        {
            e.Send($"choose {shape}");
            e.Send("set Height|5");
            Click(e, P(dx, dz));
        }
        e.Send("choose Mound");
        Move(e, P(40, 20));
        e.Send("wait 300");
        string shot = Out("shape.jpg");
        e.Shot(shot);
        // Where each shape shows on the picture: the front row's names below them, the back row's above.
        Label(shot, [("Mound", 600, 578), ("Cone", 828, 634), ("Mesa", 1102, 698), ("Crater", 690, 360), ("Moat (ring ditch)", 875, 384), ("Ridged hill", 1088, 414)]);
    }

    /// <summary>shape-formula-error.jpg: the Shape panel alone, an unfinished formula and the orange error under it.</summary>
    private void ShapeFormulaError(Editor e)
    {
        Bare(e);
        e.Send("click Shape");
        e.Send("choose Mound");
        // The formula box has no label the driver can set it by: typed into on the display.
        e.Point(250, 290);
        RunXdotool("click", "1");
        RunXdotool("key", "ctrl+a");
        RunXdotool("type", "--delay", "20", "h * smooth(1 - d / ");
        RunXdotool("mousemove", "1915", "1075");
        e.Send("wait 500");
        string shot = Out("shape-formula-error.jpg");
        e.Shot(shot);
        Crop(shot, new SKRectI(90, 62, 430, 492));
    }

    /// <summary>
    /// mountain-preview.jpg: the Mountain panel with a lone peak's orange preview and ring under the pointer in the
    /// middle of a 5 × 5 area; inset top right, the pointer near the area's edge with the ring red.
    /// </summary>
    private void MountainPreview(Editor e)
    {
        OpenGround(e, 5, P(-159, -159), P(159, 159));
        e.Send("click Mountain");
        e.Send("choose Lone peak");
        e.Send("set Height|80");
        e.Send("set Radius|120");
        e.Send("set Rough|0.4");
        e.Send("set Seed|7");
        Camera(e, 25, 0, 200, 30, 420);
        // A first move while the area settles after the clearing: until then the ring came out red at the middle.
        Move(e, P(10, 10));
        e.Send("wait 3000");
        Move(e, P(0, 0));
        e.Send("wait 1000");
        string shot = Out("mountain-preview.jpg");
        e.Shot(shot);
        Camera(e, 0, 0, 200, 45, 480);
        Move(e, P(-100, 10));
        e.Send("wait 1000");
        string edge = Out("mountain-preview-edge.jpg");
        e.Shot(edge);
        // The edge picture's ground and red ring, top right over the sky.
        Inset(shot, edge, new SKRectI(424, 250, 1014, 800), new SKRectI(1080, 80, 1420, 397));
        File.Delete(edge);
    }

    /// <summary>
    /// mountain-peak.jpg, mountain-ridge.jpg, mountain-volcano.jpg and mountain-hills.jpg: the Mountain tool's presets
    /// past the game's ±8 m, each in turn on a 5 × 5 area cleared of trees and rocks and levelled (Ctrl+Z between
    /// them), seen from low down; the peak with the biome's trees grown on it, the others bare so their shape shows.
    /// </summary>
    private void Mountain(Editor e)
    {
        OpenGround(e, 5, P(-159, -159), P(159, 159));
        // Flat ground all round, so each mountain stands out against it.
        Level(e, P(-159, -159), P(159, 159), soft: 0);
        MountainShot[] mountains =
        [
            new("Lone peak", "mountain-peak.jpg", 90, 125, 0.4, 7, new(20, 8, 300, 60)),
            new("Ridge", "mountain-ridge.jpg", 60, 135, 0.5, 8, new(20, 18, 260, 50)),
            new("Volcano", "mountain-volcano.jpg", 70, 125, 0.3, 7, new(20, 12, 280, 55)),
            new("Rolling hills", "mountain-hills.jpg", 30, 135, 0.1, 20, new(110, 22, 230, 40)),
        ];
        foreach (var m in mountains)
        {
            e.Send("click Mountain");
            e.Send($"choose {m.Preset}");
            e.Send($"set Height|{m.Height}");
            e.Send($"set Radius|{m.Radius}");
            e.Send($"set Rough|{m.Rough}");
            e.Send($"set Seed|{m.Seed}");
            if (m.Preset == "Ridge")
            {
                // Its length across the picture.
                e.Send("set Turn|0");
            }
            bool grow = m.Preset == "Lone peak";
            e.Send($"check Grow the biome's trees and rocks on it|{(grow ? "on" : "off")}");
            Camera(e, 0, 0, 200, 60, 420);
            Click(e, P(0, 0));
            e.Send("wait 2500");
            // Back to View: the preview under the pointer would hide the mountain.
            e.Send("key Escape");
            // Aimed above the ground (y): low down, a target on the ground would put the mountain at the top.
            e.Send($"camera {Cx} {Cz} {m.View.Yaw} {m.View.Pitch} {m.View.Distance} {m.View.Y}");
            e.Send("wait 1500");
            e.Shot(Out(m.File), keepMessage: grow);
            e.Send("key Z ctrl");
            if (grow)
            {
                // What grew on it, then the mountain.
                e.Send("key Z ctrl");
            }
            e.Send("wait 1500");
        }
    }

    /// <summary>A camera on the bare zone's middle, aimed at a height.</summary>
    /// <param name="Yaw">The turn (°).</param>
    /// <param name="Pitch">The tilt (°).</param>
    /// <param name="Distance">How far from the target (m).</param>
    /// <param name="Y">The target's height (m).</param>
    private sealed record MountainView(double Yaw, double Pitch, double Distance, double Y);

    /// <summary>One Mountain picture: the preset, the file and the panel's values, and where it is seen from.</summary>
    /// <param name="Preset">The preset's name.</param>
    /// <param name="File">The picture's file.</param>
    /// <param name="Height">Height (m).</param>
    /// <param name="Radius">Radius (m).</param>
    /// <param name="Rough">Rough, 0 to 1.</param>
    /// <param name="Seed">Seed, so the same mountain comes every time.</param>
    /// <param name="View">The camera.</param>
    private sealed record MountainShot(string Preset, string File, double Height, double Radius, double Rough, int Seed, MountainView View);

    /// <summary>The two mounds the Path pictures run over or up to.</summary>
    private static void PathMounds(Editor e)
    {
        Stroke(e, "D1", P(-6, 8), 10, hold: 3000, steps: 5);
        Stroke(e, "D1", P(12, -6), 8, hold: 2500, steps: 5);
    }

    /// <summary>
    /// path.jpg (the Path tool's points across two mounds, Flatten to height chosen, before Apply) and path-road.jpg
    /// (the same line and camera after Flatten to height with a natural look, then Paint paved narrower along it).
    /// </summary>
    private void PathTool(Editor e)
    {
        Bare(e);
        Stage(e);
        PathMounds(e);
        Camera(e, 14, 4, 200, 45, 86);
        e.Send("key P");
        e.Send("choose Flatten to height");
        e.Send("set Width|6");
        e.Send("set Soft edge|4");
        e.Send("check Natural look (ragged edges, bumps)|on");
        // The road's height from the flat ground beside it.
        Click(e, P(-20, 20), "alt");
        // Through both mounds' tops.
        foreach (var p in new[] { P(28, -16), P(12, -6), P(3, 1), P(-6, 8), P(-22, 18) })
        {
            Click(e, p);
        }
        e.Send("wait 300");
        e.Shot(Out("path.jpg"));
        e.Send("key Return");
        e.Send("choose Paint paved");
        e.Send("set Width|4");
        e.Send("set Soft edge|1");
        e.Send("key Return");
        e.Send("wait 300");
        e.Shot(Out("path-road.jpg"));
    }

    /// <summary>
    /// path-soft-edge.jpg: two straight trenches dug with Lower by 3 m, Width 6, side by side: left Soft edge 0
    /// (straight steep walls), right Soft edge 8 m (blended banks).
    /// </summary>
    private void PathSoftEdge(Editor e)
    {
        Bare(e);
        Stage(e);
        Camera(e, 14, 6, 200, 30, 70);
        e.Send("key P");
        e.Send("choose Lower by");
        e.Send("set Amount|3");
        e.Send("set Width|6");
        e.Send("check Natural look (ragged edges, bumps)|off");
        // East is left on the picture: the sharp trench east, the soft one west.
        foreach (var (dx, soft) in new[] { (11.0, 0.0), (-11.0, 8.0) })
        {
            e.Send($"set Soft edge|{soft}");
            Click(e, P(dx, 26));
            Click(e, P(dx, -26));
            e.Send("key Return");
            e.Send("key Escape");
        }
        // The pointer's ring on the far part of the sharp trench, small and inside the zone.
        Move(e, P(11, -18));
        Camera(e, 12, 4, 200, 22, 58);
        e.Send("wait 300");
        e.Shot(Out("path-soft-edge.jpg"));
    }

    /// <summary>
    /// path-ramp.jpg: a pad raised 8 m with a sheer side, and a ramp of two points from the ground at its foot to
    /// its top, seen from the side with the panel's Start and End.
    /// </summary>
    private void PathRamp(Editor e)
    {
        Bare(e);
        Stage(e);
        // A pad 8 m up with straight sides, west of the middle.
        AreaAction(e, P(-30, -12), P(-8, 14), "Flatten", apply: false);
        e.Send("set Soft edge|0");
        e.Send("set Height|43");
        e.Send("key Return");
        e.Send("click Clear (Esc)");
        Camera(e, 17, 0, 180, 9, 54);
        e.Send("key P");
        e.Send("choose Ramp (start → end)");
        e.Send("set Width|6");
        e.Send("set Soft edge|2");
        e.Send("check Natural look (ragged edges, bumps)|off");
        Click(e, P(26, 1));
        Click(e, P(-12, 1));
        e.Send("key Return");
        e.Send("wait 300");
        e.Shot(Out("path-ramp.jpg"));
    }

    /// <summary>path-river.jpg: a river dug across the zone with Path's River / canal, the water shown.</summary>
    private void PathRiver(Editor e)
    {
        Bare(e, water: true);
        e.Send("key P");
        e.Send("choose River / canal (water)");
        e.Send("set Width|7");
        e.Send("check Natural look (ragged edges, bumps)|on");
        foreach (var p in new[] { P(-30, -24), P(-12, -4), P(4, -2), P(18, 14), P(30, 26) })
        {
            Click(e, p);
        }
        e.Send("key Return");
        e.Send("key H");
        Camera(e, 4, 0, 200, 38, 62);
        e.Shot(Out("path-river.jpg"));
    }

    /// <summary>
    /// path-cave.jpg: a cave dug through a big mound with the Cave button (preset Cave, No limit on so its walls
    /// rise to the roof), seen at head height from its entrance slope, the Cave panel beside it.
    /// </summary>
    private void PathCave(Editor e)
    {
        Bare(e);
        Stage(e);
        e.Send("key D1");
        e.Send("check No limit: past the game's ±8 m|on");
        // A big round hill, 18 m high, to dig through.
        e.Send("click Shape");
        e.Send("choose Mound");
        e.Send("set Radius|26");
        e.Send("set Height|18");
        Click(e, P(0, 0));
        Camera(e, 0, 0, 200, 70, 90);
        e.Send("click Cave");
        e.Send("choose Cave");
        foreach (var p in new[] { P(30, -2), P(10, 0), P(-10, 1), P(-30, 2) })
        {
            Click(e, p);
        }
        e.Send("key Return");
        e.Send("wait 1500");
        // The line off, so only the cave shows; the panel stays.
        e.Send("key Escape");
        // The pointer's ring away behind the camera, so it does not show on the walls.
        Move(e, P(30, -30));
        // At the east mouth, above the entrance slope, looking in (west).
        e.Send($"camera {Cx + 10} {Cz} 262 10 24 43");
        e.Send("wait 500");
        e.Shot(Out("path-cave.jpg"), keepMessage: true);
    }

    /// <summary>
    /// An area of this many zones round the bare zone, its zone borders off and its trees and rocks taken away
    /// within the box from a to b, so what a scene shapes stands bare.
    /// </summary>
    /// <param name="e">The editor.</param>
    /// <param name="size">The area's width in zones.</param>
    /// <param name="a">One corner of the box cleared.</param>
    /// <param name="b">The opposite corner.</param>
    /// <param name="slope">Show the slope colours.</param>
    private void OpenGround(Editor e, int size, (double X, double Z) a, (double X, double Z) b, bool slope = false)
    {
        Bare(e, size: size, unsaved: false, slope: slope);
        e.Send("click View");
        e.Send("click Zone borders");
        e.Send("click View");
        Camera(e, 0, 0, 200, 89, 110 * size);
        AreaAction(e, a, b, "Remove objects");
        e.Send("click Clear (Esc)");
    }

    /// <summary>Names written on a picture, white on a dark rounded box, centred on each point.</summary>
    private static void Label(string file, (string Text, int X, int Y)[] labels)
    {
        using var bitmap = LoadBitmap(file);
        using (var canvas = new SKCanvas(bitmap))
        {
            using var font = new SKFont(SKTypeface.FromFamilyName("Noto Sans", SKFontStyle.Bold), 17);
            using var box = new SKPaint { Color = new SKColor(20, 24, 30, 210), IsAntialias = true };
            using var ink = new SKPaint { Color = SKColors.White, IsAntialias = true };
            foreach (var (text, x, y) in labels)
            {
                float width = font.MeasureText(text);
                canvas.DrawRoundRect(new SKRect(x - width / 2 - 8, y - 15, x + width / 2 + 8, y + 9), 6, 6, box);
                canvas.DrawText(text, x, y, SKTextAlign.Center, font, ink);
            }
        }
        SavePicture(bitmap, file);
    }

    /// <summary>A picture cut down to a rectangle of it.</summary>
    private static void Crop(string file, SKRectI keep)
    {
        using var bitmap = LoadBitmap(file);
        using var part = new SKBitmap(keep.Width, keep.Height);
        if (!bitmap.ExtractSubset(part, keep))
        {
            throw new InvalidOperationException($"{file}: {keep} is not inside the picture");
        }
        SavePicture(part, file);
    }

    /// <summary>A part of another picture drawn small, framed, over a corner of this one.</summary>
    /// <param name="file">The picture drawn on.</param>
    /// <param name="other">The picture the inset comes from.</param>
    /// <param name="from">The part of the other picture shown.</param>
    /// <param name="to">Where it goes on this one.</param>
    private static void Inset(string file, string other, SKRectI from, SKRectI to)
    {
        using var bitmap = LoadBitmap(file);
        using var inset = LoadBitmap(other);
        using (var canvas = new SKCanvas(bitmap))
        {
            using var image = SKImage.FromBitmap(inset);
            using var frame = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true };
            canvas.DrawImage(image, from, to, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            canvas.DrawRect(to, frame);
        }
        SavePicture(bitmap, file);
    }
}
