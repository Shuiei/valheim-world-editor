namespace TerrainEditor.DocsScreenshots;

/// <summary>The ground tools' pictures: the brushes, paint, Shape, Mountain, Path, the Mask and Measure.</summary>
public sealed partial class Scenes
{
    /// <summary>sculpt.jpg: a mound raised with the Raise brush, the brush's ring by it and its panel.</summary>
    private void Sculpt(Editor e)
    {
        Bare(e);
        e.Send("key D1");
        e.Send("set Strength|0.5");
        Stroke(e, "D1", P(0, 0), 9, hold: 2200, steps: 4);
        Move(e, P(9, -5));
        Camera(e, 10, 0, 200, 36, 46);
        // The brush's ring follows the pointer a frame later.
        e.Send("wait 300");
        e.Shot(Out("sculpt.jpg"));
    }

    /// <summary>
    /// brush-shapes.jpg: the brush's shapes, each painted once in paved stone (the paint shows the shape best):
    /// Circle, Square, Ring, Ragged (noise) and the Crater rim stamp, from left to right.
    /// </summary>
    private void BrushShapes(Editor e)
    {
        Bare(e);
        e.Send("key D8");
        e.Send("set Size|5");
        e.Send("set Strength|1");
        string[] shapes = ["Circle", "Square", "Ring", "Ragged (noise)", "Stamp: Crater rim"];
        for (int i = 0; i < shapes.Length; i++)
        {
            e.Send($"choose {shapes[i]}");
            // Left to right on the picture: east to west at this yaw.
            Hold(e, P(26 - i * 13, 0), 500);
        }
        e.Send("choose Circle");
        e.Send("key H");
        Camera(e, -8, -2, 200, 62, 50);
        e.Shot(Out("brush-shapes.jpg"));
    }

    /// <summary>
    /// brush-falloffs.jpg: mounds raised with the same stroke and each falloff (Smooth, Dome, Flat top, Peak, Sharp edge),
    /// seen from the side on levelled ground.
    /// </summary>
    private void BrushFalloffs(Editor e)
    {
        Bare(e);
        Stage(e);
        e.Send("key D1");
        e.Send("choose Circle");
        e.Send("set Size|6");
        e.Send("set Strength|0.5");
        string[] falloffs = ["Smooth", "Dome", "Flat top", "Peak", "Sharp edge (pickaxe)"];
        for (int i = 0; i < falloffs.Length; i++)
        {
            e.Send($"choose {falloffs[i]}");
            Hold(e, P(24 - i * 12, 0), 1000);
        }
        e.Send("choose Smooth");
        e.Send("key H");
        Camera(e, 0, 6, 180, 9, 40);
        e.Shot(Out("brush-falloffs.jpg"));
    }

    /// <summary>flatten.jpg: a pad levelled into a mound's side with Flatten, its panel beside it.</summary>
    private void Flatten(Editor e)
    {
        Bare(e);
        e.Send("key D1");
        e.Send("set Strength|0.6");
        e.Send("choose Dome");
        Stroke(e, "D1", P(0, 4), 14, hold: 4000, steps: 5);
        e.Send("choose Smooth");
        // The pad: levelled at the height where the stroke starts, partway up the mound.
        e.Send("key D3");
        e.Send("set Size|6");
        e.Send("set Strength|1");
        e.Send("check Level to the height where the stroke starts|on");
        Path(e, [P(-6, -2), P(0, -2), P(6, -2)], steps: 6, hold: 200);
        Move(e, P(12, -10));
        Camera(e, 12, 0, 200, 38, 52);
        e.Send("wait 300");
        e.Shot(Out("flatten.jpg"));
    }

    /// <summary>
    /// naturalize.jpg: levelled ground, one half naturalized into bumps, with the slope colours on (the flat half is
    /// plain green, the bumps show as coloured patches).
    /// </summary>
    private void Naturalize(Editor e)
    {
        Bare(e, slope: true);
        Stage(e);
        e.Send("key D0");
        e.Send("set Size|10");
        e.Send("set Strength|1");
        e.Send("set Bumps|2.5");
        e.Send("set Bump size|6");
        foreach (int dz in new[] { -18, -6, 6, 18 })
        {
            Path(e, [P(-24, dz), P(0, dz)], steps: 6, hold: 200);
        }
        Move(e, P(-10, -26));
        Camera(e, 4, 2, 200, 40, 60);
        e.Send("wait 300");
        e.Shot(Out("naturalize.jpg"));
    }

    /// <summary>
    /// erode.jpg: two steep mounds; Erode's water weathers the right one into gullies and fans, the left one is left as
    /// raised.
    /// </summary>
    private void Erode(Editor e)
    {
        Bare(e);
        Stage(e);
        e.Send("key D1");
        e.Send("choose Sharp edge (pickaxe)");
        e.Send("set Strength|0.6");
        Stroke(e, "D1", P(13, 0), 9, hold: 2500, steps: 4);
        Stroke(e, "D1", P(-13, 0), 9, hold: 2500, steps: 4);
        e.Send("choose Smooth");
        e.Send("key O");
        e.Send("click Water");
        e.Send("set Size|14");
        e.Send("set Strength|1");
        Hold(e, P(-13, 0), 6000, steps: 6);
        e.Send("key H");
        Camera(e, 2, 0, 200, 40, 50);
        e.Shot(Out("erode.jpg"));
    }

    /// <summary>
    /// stamps.jpg: the built-in stamps put into levelled ground with Stamp once (Mountain, Mesa, Crater rim, Dunes,
    /// Rocky ground).
    /// </summary>
    private void Stamps(Editor e)
    {
        Bare(e);
        Stage(e);
        e.Send("key D1");
        e.Send("set Size|8");
        (string Stamp, double Dx, double Dz)[] stamps =
        [
            ("Stamp: Mountain", 20, 11), ("Stamp: Mesa (flat top)", 0, 11), ("Stamp: Crater rim", -20, 11),
            ("Stamp: Dunes", 10, -11), ("Stamp: Rocky ground", -10, -11),
        ];
        foreach (var (stamp, dx, dz) in stamps)
        {
            e.Send($"choose {stamp}");
            e.Send("check Stamp once: a click puts the whole stamp in|on");
            e.Send("set Height|6");
            Click(e, P(dx, dz));
        }
        e.Send("choose Circle");
        e.Send("key H");
        Camera(e, 0, 0, 200, 42, 64);
        e.Shot(Out("stamps.jpg"));
    }

    /// <summary>paint.jpg: stripes of dirt, cultivated soil and paved stone, Clear paint wiping a band across them.</summary>
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
        e.Send("key D9");
        e.Send("set Size|3");
        Path(e, [P(6, 16), P(6, -16)], steps: 8, hold: 150);
        Move(e, P(6, -20));
        Camera(e, 8, 0, 200, 50, 46);
        e.Send("wait 300");
        e.Shot(Out("paint.jpg"));
    }

    /// <summary>
    /// no-limit.jpg: two stacks raised by the same long stroke, the left stopped at the game's 8 m (red), the right with No
    /// limit, far higher.
    /// </summary>
    private void NoLimit(Editor e)
    {
        Bare(e, limit: true);
        Stage(e);
        e.Send("key D1");
        e.Send("set Strength|1");
        e.Send("check No limit: past the game's ±8 m|off");
        Stroke(e, "D1", P(12, 0), 6, hold: 5000, steps: 5);
        e.Send("check No limit: past the game's ±8 m|on");
        Stroke(e, "D1", P(-12, 0), 6, hold: 9000, steps: 9);
        e.Send("check No limit: past the game's ±8 m|off");
        e.Send("key H");
        Camera(e, 0, 4, 180, 14, 105);
        e.Shot(Out("no-limit.jpg"), keepMessage: true);
    }

    /// <summary>shape.jpg: the Shape tool's presets side by side (Mound, Cone, Mesa, Crater, Moat, Ridged hill).</summary>
    private void ShapeTool(Editor e)
    {
        Bare(e);
        Stage(e);
        e.Send("click Shape");
        e.Send("set Radius|8");
        (string Shape, double Dx, double Dz)[] shapes =
        [
            ("Mound", 20, 11), ("Cone", 0, 11), ("Mesa", -20, 11),
            ("Crater", 20, -11), ("Moat (ring ditch)", 0, -11), ("Ridged hill", -20, -11),
        ];
        foreach (var (shape, dx, dz) in shapes)
        {
            e.Send($"choose {shape}");
            e.Send("set Height|5");
            Click(e, P(dx, dz));
        }
        e.Send("choose Mound");
        Move(e, P(-20, -26));
        Camera(e, 12, 0, 200, 42, 70);
        e.Send("wait 300");
        e.Shot(Out("shape.jpg"));
    }

    /// <summary>
    /// mountain-peak.jpg, mountain-ridge.jpg, mountain-volcano.jpg and mountain-hills.jpg: the Mountain tool's presets past
    /// the game's ±8 m, each in turn on a 5 × 5 area (Ctrl+Z between them), seen from low down; the peak with the biome's
    /// trees grown on it, the others bare so their shape shows.
    /// </summary>
    private void Mountain(Editor e)
    {
        Bare(e, size: 5, unsaved: false);
        (string Preset, string File, double Height)[] presets =
        [
            ("Lone peak", "mountain-peak.jpg", 80), ("Ridge", "mountain-ridge.jpg", 60),
            ("Volcano", "mountain-volcano.jpg", 70), ("Rolling hills", "mountain-hills.jpg", 25),
        ];
        foreach (var (preset, file, height) in presets)
        {
            e.Send("click Mountain");
            e.Send($"choose {preset}");
            e.Send($"set Height|{height}");
            e.Send("set Rough|0.4");
            // Bare, so its shape shows (the trees would cover it); the peak shows them grown.
            e.Send($"check Grow the biome's trees and rocks on it|{(preset == "Lone peak" ? "on" : "off")}");
            Camera(e, 0, 0, 200, 35, 300);
            Click(e, P(0, 0));
            e.Send("wait 2000");
            // Back to View: the preview under the pointer would hide the mountain.
            e.Send("key Escape");
            Camera(e, -30, 0, 200, 18, 360);
            e.Send("wait 1500");
            e.Shot(Out(file), keepMessage: preset == "Lone peak");
            // Twice: what grew on it, then the mountain.
            e.Send("key Z ctrl");
            e.Send("key Z ctrl");
            e.Send("wait 1500");
        }
    }

    /// <summary>The two mounds the Path pictures run over or up to.</summary>
    private static void PathMounds(Editor e)
    {
        Stroke(e, "D1", P(-6, 8), 10, hold: 3000, steps: 5);
        Stroke(e, "D1", P(12, -6), 8, hold: 2500, steps: 5);
    }

    /// <summary>
    /// path.jpg (the Path tool's points across two mounds, before Enter) and path-road.jpg (the same line applied: a
    /// level road cut through, with a natural look, then paved along).
    /// </summary>
    private void PathTool(Editor e)
    {
        Bare(e);
        PathMounds(e);
        e.Send("key P");
        e.Send("choose Flatten to height");
        e.Send("set Width|6");
        e.Send("check Natural look (ragged edges, bumps)|on");
        (double X, double Z)[] points = [P(-24, -20), P(-10, -6), P(2, 2), P(14, 12), P(24, 24)];
        foreach (var p in points)
        {
            Click(e, p);
        }
        Camera(e, 10, 0, 200, 40, 62);
        e.Shot(Out("path.jpg"));
        e.Send("key Return");
        // Paved along the same line, narrower.
        e.Send("choose Paint paved");
        e.Send("set Width|3");
        foreach (var p in points)
        {
            Click(e, p);
        }
        e.Send("key Return");
        e.Send("key H");
        Camera(e, 0, 0, 200, 40, 58);
        e.Shot(Out("path-road.jpg"));
    }

    /// <summary>path-ramp.jpg: a ramp from the ground up onto a raised pad, with Path's Ramp.</summary>
    private void PathRamp(Editor e)
    {
        Bare(e);
        Stage(e);
        // A pad 7 m up, flat on top.
        e.Send("key D1");
        e.Send("choose Flat top");
        e.Send("set Strength|0.8");
        Stroke(e, "D1", P(-14, 4), 9, hold: 5000, steps: 5);
        e.Send("choose Smooth");
        e.Send("key P");
        e.Send("choose Ramp (start → end)");
        e.Send("set Width|5");
        // From the ground to the middle of the pad: the ramp's ends follow the ground there (35 m up to 43 m).
        Click(e, P(24, -8));
        Click(e, P(-14, 4));
        e.Send("key Return");
        e.Send("key H");
        // From the side, where the slope shows.
        Camera(e, 2, -2, 180, 10, 62);
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
    /// path-cave.jpg (a cave dug through a mound with Path's Cave, roofed with the game's boulders) and
    /// path-cave-inside.jpg (the view down the tunnel).
    /// </summary>
    private void PathCave(Editor e)
    {
        Bare(e);
        e.Send("key D1");
        e.Send("set Strength|0.6");
        Stroke(e, "D1", P(0, 0), 16, hold: 6000, steps: 6);
        e.Send("key P");
        e.Send("choose Cave (dig and roof)");
        e.Send("choose Cave");
        foreach (var p in new[] { P(-26, -6), P(-6, 0), P(10, 4), P(26, 10) })
        {
            Click(e, p);
        }
        e.Send("key Return");
        e.Send("wait 1500");
        e.Send("key H");
        // At the west mouth, looking in.
        Camera(e, -26, -6, 270, 14, 50);
        e.Shot(Out("path-cave.jpg"), keepMessage: true);
    }

    /// <summary>
    /// mask.jpg: a wide paved stroke across a mound, the Mask letting it pave only ground under 15°: the mound's steep sides
    /// stay grass.
    /// </summary>
    private void Mask(Editor e)
    {
        Bare(e);
        e.Send("key D1");
        e.Send("set Strength|0.6");
        Stroke(e, "D1", P(0, 2), 9, hold: 3000, steps: 5);
        e.Send("click Mask");
        e.Send("set Slope|,15");
        e.Send("key D8");
        e.Send("set Size|10");
        e.Send("set Strength|1");
        Path(e, [P(-26, 2), P(26, 2)], steps: 12, hold: 150);
        Move(e, P(20, -12));
        Camera(e, 14, 0, 200, 42, 52);
        e.Send("wait 300");
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
        Camera(e, 8, 0, 200, 35, 50);
        e.Shot(Out("measure.jpg"));
    }

    /// <summary>height-lines.jpg: height lines every 2 m over two mounds and a hollow, from above.</summary>
    private void HeightLines(Editor e)
    {
        Bare(e, view: true);
        Stroke(e, "D1", P(-8, 4), 10, hold: 2500, steps: 5);
        Stroke(e, "D1", P(12, -6), 7, hold: 2000, steps: 5);
        Stroke(e, "D2", P(6, 14), 6, hold: 1500, steps: 3);
        e.Send("key H");
        e.Send("panel view");
        e.Send("check Height lines every|on");
        Camera(e, -12, 0, 200, 60, 58);
        e.Send("wait 500");
        e.Shot(Out("height-lines.jpg"));
    }
}
