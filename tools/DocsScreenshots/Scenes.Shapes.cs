namespace TerrainEditor.DocsScreenshots;

/// <summary>The shaping tools' pictures: Shape, Mountain and the Path tool's mounds, ramps, rivers and caves.</summary>
public sealed partial class Scenes
{
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
}
