using SkiaSharp;

namespace TerrainEditor.DocsScreenshots;

/// <summary>The ground tools' pictures: the brushes, stamps, paint, No limit, the Mask, Measure and the overlays.</summary>
public sealed partial class Scenes
{
    /// <summary>The window's rows between the top bar and the status bar, where a before and after pair is put.</summary>
    private const int GroundPairTop = 62, GroundPairBottom = 850;

    /// <summary>The window's column where the 3D view starts right of the tool panel.</summary>
    private const int GroundPairLeft = 410;

    /// <summary>
    /// The window's column where a before and after pair's halves are cut from: right of the cards beside the tool
    /// panel (the Mask card), so the subject is aimed at about 260 pixels right of the view's middle.
    /// </summary>
    private const int GroundPairCut = 718;

    /// <summary>The direction of a row laid along the bare zone's diagonal (north-east), a unit vector.</summary>
    private static readonly (double X, double Z) GroundDiagonal = (Math.Sqrt(0.5), Math.Sqrt(0.5));

    /// <summary>A point on the bare zone's diagonal, t metres north-east of its middle.</summary>
    private static (double X, double Z) OnDiagonal(double t) => P(t * GroundDiagonal.X, t * GroundDiagonal.Z);

    /// <summary>
    /// The camera pointed as <see cref="Camera"/> does, then a pause while it glides there: the pointer moved before it
    /// arrives would put the brush's ring where the old view had that point.
    /// </summary>
    private static void Aim(Editor e, double dx, double dz, double yaw, double pitch, double distance)
    {
        Camera(e, dx, dz, yaw, pitch, distance);
        e.Send("wait 1200");
    }

    /// <summary>
    /// sculpt.jpg: a mound made with three Raise strokes (Size 8, Strength 0.40, Circle, Smooth), the brush's ring on its
    /// top and the Raise panel with its settings.
    /// </summary>
    private void Sculpt(Editor e)
    {
        Bare(e);
        e.Send("key D1");
        e.Send("set Size|8");
        e.Send("set Strength|0.4");
        e.Send("choose Circle");
        e.Send("choose Smooth");
        foreach (var at in new[] { P(-3, 1), P(3, 1), P(0, -2) })
        {
            Hold(e, at, 900);
        }
        Aim(e, 9, 0, 200, 30, 36);
        // The ring is drawn where the pointer is: moved after the camera, so it lands on the top.
        Move(e, P(0, 0));
        e.Send("wait 300");
        e.Shot(Out("sculpt.jpg"));
    }

    /// <summary>
    /// brush-shapes.jpg: the brush's shapes, each a dab of Paved paint (the paint shows the shape best) along the bare
    /// zone's diagonal: Circle, Square, Ring, Ragged (noise) and the Crater rim stamp from left to right, each labelled,
    /// the panel's Shape list open.
    /// </summary>
    private void BrushShapes(Editor e)
    {
        Bare(e);
        e.Send("key D8");
        e.Send("set Size|6");
        e.Send("set Strength|1");
        e.Send("choose Smooth");
        string[] shapes = ["Circle", "Square", "Ring", "Ragged (noise)", "Stamp: Crater rim"];
        for (int i = 0; i < shapes.Length; i++)
        {
            e.Send($"choose {shapes[i]}");
            Hold(e, OnDiagonal(32 - i * 16), 700);
        }
        e.Send("choose Circle");
        Aim(e, 7, 7, 225, 60, 72);
        Move(e, P(24, -26));
        e.Send("wait 300");
        ShotWithList(e, Out("brush-shapes.jpg"), 260, 273);
        // Each dab's shape under it (where this camera shows them).
        Label(Out("brush-shapes.jpg"), [("Circle", 478, 545), ("Square", 655, 545), ("Ring", 826, 545), ("Ragged (noise)", 995, 545), ("Stamp: Crater rim", 1163, 545)]);
    }

    /// <summary>
    /// brush-falloffs.jpg: six mounds raised on levelled ground by the same Raise stroke, one per falloff in the list's
    /// order (Smooth, Linear, Dome, Flat top, Peak, Sharp edge) from left to right along the bare zone's diagonal, slope
    /// colours on, each labelled.
    /// </summary>
    private void BrushFalloffs(Editor e)
    {
        Bare(e, slope: true);
        Stage(e);
        e.Send("key D1");
        e.Send("choose Circle");
        e.Send("set Size|6");
        e.Send("set Strength|0.5");
        string[] falloffs = ["Smooth", "Linear", "Dome", "Flat top", "Peak", "Sharp edge (pickaxe)"];
        for (int i = 0; i < falloffs.Length; i++)
        {
            e.Send($"choose {falloffs[i]}");
            Hold(e, OnDiagonal(31.25 - i * 12.5), 1300);
        }
        e.Send("choose Smooth");
        Aim(e, 6, 6, 225, 27, 66);
        Move(e, P(26, -26));
        e.Send("wait 300");
        ShotWithoutRing(e, "brush-falloffs.jpg");
        // Each mound's falloff under it (where this camera shows them).
        Label(Out("brush-falloffs.jpg"), [("Smooth", 458, 565), ("Linear", 600, 565), ("Dome", 745, 565), ("Flat top", 890, 565), ("Peak", 1030, 565), ("Sharp edge (pickaxe)", 1175, 565)]);
    }

    /// <summary>
    /// flatten.jpg: a big mound whose top one Flatten stroke levelled, started on the top with Level to the height where
    /// the stroke starts on (Height greyed, showing the height it took), the ring on the levelled top.
    /// </summary>
    private void Flatten(Editor e)
    {
        Bare(e);
        e.Send("key D1");
        e.Send("set Strength|0.6");
        e.Send("choose Smooth");
        Stroke(e, "D1", P(0, 0), 14, hold: 3000, steps: 5);
        e.Send("key D3");
        e.Send("set Size|7");
        e.Send("set Strength|1");
        e.Send("check Level to the height where the stroke starts|on");
        Path(e, [P(-3, 0), P(3, 0), P(0, 3), P(0, -3)], steps: 4, hold: 200);
        Aim(e, 10, 0, 200, 32, 48);
        Move(e, P(0, 0));
        e.Send("wait 300");
        e.Shot(Out("flatten.jpg"));
    }

    /// <summary>
    /// lower-smooth-restore.jpg: three brushes on levelled ground, from left to right: a pit dug with Lower, a rough mound
    /// whose right half Smooth evened out, and a mound whose right half Restore took back, its ring on it, each labelled.
    /// </summary>
    private void LowerSmoothRestore(Editor e)
    {
        Bare(e);
        Stage(e);
        e.Send("key D2");
        e.Send("choose Circle");
        e.Send("choose Smooth");
        e.Send("set Strength|0.5");
        Stroke(e, "D2", P(20, 0), 6, hold: 1500);
        // A rough mound: rocky ground stamped up.
        e.Send("key D1");
        e.Send("choose Stamp: Rocky ground");
        e.Send("check Stamp once: a click puts the whole stamp in|on");
        e.Send("set Height|6");
        e.Send("set Size|9");
        Click(e, P(0, 0));
        e.Send("check Stamp once: a click puts the whole stamp in|off");
        e.Send("choose Circle");
        e.Send("key D4");
        e.Send("set Size|5");
        e.Send("set Strength|0.35");
        foreach (double dz in new[] { -6.0, 0, 6 })
        {
            Hold(e, P(-5, dz), 800);
        }
        e.Send("key D1");
        e.Send("set Strength|0.5");
        Stroke(e, "D1", P(-20, 0), 7, hold: 1600);
        e.Send("key D5");
        e.Send("set Size|5");
        e.Send("set Strength|1");
        Path(e, [P(-24, 8), P(-24, -8)], steps: 6, hold: 300);
        Aim(e, 2, 0, 200, 35, 54);
        Move(e, P(-24, 0));
        e.Send("wait 300");
        e.Shot(Out("lower-smooth-restore.jpg"));
        // Each brush's name by what it did (where this camera shows them).
        Label(Out("lower-smooth-restore.jpg"), [("Lower", 492, 468), ("Smooth: right half", 740, 568), ("Restore: right half", 1100, 640)]);
    }

    /// <summary>
    /// naturalize.jpg: levelled ground whose left half was naturalized (Bumps 3, Bump size 12) into soft bumps, the right
    /// half left flat; the slope colours on show the bumps (the game's light leaves low bumps hard to see), the flat half
    /// plain green.
    /// </summary>
    private void Naturalize(Editor e)
    {
        Bare(e, slope: true);
        Stage(e);
        e.Send("key D0");
        e.Send("set Size|10");
        e.Send("set Strength|1");
        e.Send("set Bumps|3");
        e.Send("set Bump size|12");
        foreach (int dz in new[] { -18, -6, 6, 18 })
        {
            Path(e, [P(28, dz), P(2, dz)], steps: 6, hold: 250);
        }
        Aim(e, 6, 0, 200, 30, 60);
        e.Send("wait 300");
        ShotWithoutRing(e, "naturalize.jpg");
    }

    /// <summary>
    /// erode.jpg: one mound before (left) and after (right) Erode's Water held over it, from the same camera, the Erode
    /// panel with Water on.
    /// </summary>
    private void Erode(Editor e)
    {
        Bare(e);
        Stage(e);
        e.Send("key D1");
        e.Send("choose Smooth");
        e.Send("set Strength|0.5");
        Stroke(e, "D1", P(0, 0), 11, hold: 1600, steps: 4);
        Aim(e, 11, 0, 200, 35, 40);
        GroundPair(e, "erode.jpg", ("Before", "After Erode (Water)"), () =>
        {
            e.Send("key O");
            e.Send("click Water");
            e.Send("set Size|12");
            e.Send("set Strength|0.8");
            Hold(e, P(0, 0), 2000, steps: 4);
            e.Send("wait 300");
        });
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

    /// <summary>
    /// stamp-once.jpg: Raise with the Mesa stamp, Stamp once on and Height 8 m: the stamp's square outline on flat ground
    /// before the click (left), the mesa it put in after (right), the same camera.
    /// </summary>
    private void StampOnce(Editor e)
    {
        Bare(e);
        Stage(e);
        e.Send("key D1");
        e.Send("set Size|9");
        e.Send("choose Stamp: Mesa (flat top)");
        e.Send("check Stamp once: a click puts the whole stamp in|on");
        e.Send("set Height|8");
        Aim(e, 14.5, 0, 200, 35, 50);
        Move(e, P(0, 0));
        e.Send("wait 300");
        GroundPair(e, "stamp-once.jpg", ("Before the click", "After one click"), () =>
        {
            Click(e, P(0, 0));
            e.Send("wait 300");
        }, ring: true);
    }

    /// <summary>
    /// paint.jpg: stripes of paved, cultivated and dirt ground before (left) and after (right) one Clear paint stroke
    /// across all three, from the same camera, the Clear paint panel.
    /// </summary>
    private void Paint(Editor e)
    {
        Bare(e);
        Stage(e);
        e.Send("key D8");
        e.Send("choose Circle");
        e.Send("choose Smooth");
        foreach (var (key, dz) in new[] { ("D8", 9), ("D7", 0), ("D6", -9) })
        {
            e.Send($"key {key}");
            e.Send("set Size|4");
            e.Send("set Strength|1");
            Path(e, [P(12, dz), P(-12, dz)], steps: 8, hold: 150);
        }
        Aim(e, 16, 0, 200, 45, 58);
        GroundPair(e, "paint.jpg", ("Before", "After Clear paint"), () =>
        {
            e.Send("key D9");
            e.Send("set Size|3");
            e.Send("set Strength|1");
            Path(e, [P(0, 16), P(0, -16)], steps: 8, hold: 200);
            e.Send("wait 300");
        });
    }

    /// <summary>
    /// limit-red.jpg: a Raise stroke held on flat ground until it stops at the game's 8 m, its top tinted red (Limit
    /// marks on), the pointer on it: the status bar's message and the cursor readout say it reached the limit.
    /// </summary>
    private void LimitRed(Editor e)
    {
        Bare(e, limit: true);
        Stage(e);
        e.Send("key D1");
        e.Send("choose Circle");
        e.Send("choose Smooth");
        e.Send("set Strength|1");
        e.Send("check No limit: past the game's ±8 m|off");
        Stroke(e, "D1", P(0, 0), 9, hold: 4000, steps: 4);
        Aim(e, 9, 0, 200, 30, 44);
        Move(e, P(0, 0));
        e.Send("wait 300");
        e.Shot(Out("limit-red.jpg"), keepMessage: true);
    }

    /// <summary>
    /// no-limit.jpg: two stacks raised by the same Raise stroke held as long, with the brush panel's No limit off (left:
    /// stopped at the game's 8 m, its top red) and on (right: a spire far higher), the switch ticked.
    /// </summary>
    private void NoLimit(Editor e)
    {
        Bare(e, limit: true);
        Stage(e);
        e.Send("key D1");
        e.Send("choose Circle");
        e.Send("choose Smooth");
        e.Send("set Size|6");
        e.Send("set Strength|1");
        e.Send("check No limit: past the game's ±8 m|off");
        Hold(e, P(13, 0), 6000, steps: 6);
        e.Send("check No limit: past the game's ±8 m|on");
        Hold(e, P(-13, 0), 6000, steps: 6);
        Aim(e, 0, 0, 200, 12, 74);
        e.Send("wait 300");
        ShotWithoutRing(e, "no-limit.jpg");
    }

    /// <summary>
    /// mask.jpg: the same wide Paved stroke across a steep mound with the Mask off (left: all paved) and on with Slope max
    /// 15° (right: the flat ground paved, the steep sides not), the Mask card beside the panel.
    /// </summary>
    private void Mask(Editor e)
    {
        Bare(e);
        Stage(e);
        e.Send("key D1");
        e.Send("choose Circle");
        e.Send("choose Smooth");
        e.Send("set Strength|0.6");
        Stroke(e, "D1", P(0, 0), 8, hold: 3000, steps: 5);
        e.Send("key D8");
        e.Send("set Size|10");
        e.Send("set Strength|1");
        (double X, double Z)[] stroke = [P(18, 0), P(-18, 0)];
        Path(e, stroke, steps: 12, hold: 150);
        Aim(e, 15, -8, 200, 40, 56);
        GroundPair(e, "mask.jpg", ("Mask off", "Mask on, slope max 15°"), () =>
        {
            e.Send("key Z ctrl");
            e.Send("key D8");
            e.Send("click Mask");
            e.Send("set Slope|,15");
            Path(e, stroke, steps: 12, hold: 150);
            e.Send("wait 300");
        }, keep: [new SKRectI(GroundPairLeft, GroundPairTop, 712, 450)]);
    }

    /// <summary>mask-warning.jpg: the Mask card alone, Slope min 30 above max 10, its orange warning under the settings.</summary>
    private void MaskWarning(Editor e)
    {
        Bare(e);
        e.Send("key D8");
        e.Send("click Mask");
        e.Send("set Slope|30,10");
        e.Send("wait 300");
        string full = Out("mask-warning-full.jpg");
        e.Shot(full);
        Crop(full, Out("mask-warning.jpg"), new SKRectI(GroundPairLeft - 6, 64, 718, 408));
        File.Delete(full);
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

    /// <summary>
    /// height-lines.jpg: height lines every 2 m over two mounds and a hollow, zone borders off, the View panel scrolled to
    /// its GROUND (game look) section.
    /// </summary>
    private void HeightLines(Editor e)
    {
        Bare(e, view: true);
        e.Send("check Zone borders|off");
        Stroke(e, "D1", P(-8, 4), 10, hold: 2500, steps: 5);
        Stroke(e, "D1", P(12, -6), 7, hold: 2000, steps: 5);
        Stroke(e, "D2", P(6, 14), 6, hold: 1500, steps: 3);
        e.Send("key H");
        e.Send("panel view");
        e.Send("check Height lines every|on");
        Aim(e, -2, 0, 200, 45, 44);
        e.Scroll(1260, 500, 10);
        e.Send("wait 500");
        e.Shot(Out("height-lines.jpg"));
    }

    /// <summary>
    /// slope-colours.jpg: one mound with a gentle side, a steeper side and a cliff cut into it, slope colours on and zone
    /// borders off, the View panel scrolled to its GROUND (game look) section.
    /// </summary>
    private void SlopeColours(Editor e)
    {
        Bare(e, view: true, slope: true);
        e.Send("check Zone borders|off");
        Stage(e);
        e.Send("key D1");
        e.Send("choose Circle");
        e.Send("choose Smooth");
        e.Send("set Strength|0.5");
        e.Send("set Size|16");
        Hold(e, P(-4, 0), 2200);
        e.Send("set Size|7");
        Hold(e, P(-8, 2), 1200);
        e.Send("key D2");
        e.Send("choose Sharp edge (pickaxe)");
        e.Send("set Size|5");
        e.Send("set Strength|1");
        Path(e, [P(-15, -6), P(-15, 4)], steps: 4, hold: 400);
        e.Send("choose Smooth");
        e.Send("key H");
        e.Send("panel view");
        Aim(e, -6, 0, 200, 40, 50);
        e.Scroll(1260, 500, 10);
        e.Send("wait 500");
        e.Shot(Out("slope-colours.jpg"));
    }

    /// <summary>
    /// Two pictures of the same view, before and after a change, side by side right of the tool panel: the halves are
    /// cut from the view right of the cards (see <see cref="GroundPairCut"/>), each labelled, the rest of the window
    /// (panel, bars) as after the change. Unless the ring is wanted, both halves are taken with the View tool (no brush
    /// ring on the subject) and the rest from a picture with the brush. The pictures taken on the way are deleted.
    /// </summary>
    /// <param name="e">The editor.</param>
    /// <param name="file">The picture's name.</param>
    /// <param name="labels">The two halves' labels.</param>
    /// <param name="change">Makes the change between the two pictures, choosing its tool.</param>
    /// <param name="keep">Parts of the after picture drawn over the halves where they are in the window (a card).</param>
    /// <param name="ring">Take the halves with the brush and its ring (a stamp's outline before the click).</param>
    private void GroundPair(Editor e, string file, (string Before, string After) labels, Action change, IReadOnlyList<SKRectI>? keep = null, bool ring = false)
    {
        string before = Out("before-" + file), after = Out("after-" + file), view = Out("view-" + file);
        if (!ring)
        {
            e.Send("key H");
        }
        e.Shot(before);
        change();
        e.Shot(after);
        if (!ring)
        {
            e.Send("key H");
            e.Shot(view);
        }
        Halves(Out(file), labels, before, after, new HalvesCut(0, Keep: keep?.ToArray(), From: GroundPairCut), ring ? null : view);
    }

    /// <summary>
    /// The window's picture without the brush's ring: the view taken with the View tool, the tool panel and the Mask card
    /// from a picture taken with the brush just before.
    /// </summary>
    /// <param name="e">The editor.</param>
    /// <param name="file">The picture's name.</param>
    private void ShotWithoutRing(Editor e, string file)
    {
        string brush = Out("brush-" + file);
        e.Shot(brush);
        e.Send("key H");
        e.Shot(Out(file));
        try
        {
            using var b = LoadPicture(brush);
            using var shot = SKBitmap.Decode(Out(file)) ?? throw new InvalidOperationException($"{file}: not a picture");
            using (var canvas = new SKCanvas(shot))
            {
                foreach (var r in new[] { new SKRectI(0, GroundPairTop, GroundPairLeft, GroundPairBottom), new SKRectI(GroundPairLeft, GroundPairTop, GroundPairCut, 124) })
                {
                    canvas.DrawImage(b, r, r, Exact);
                }
            }
            SavePicture(shot, Out(file));
        }
        finally
        {
            File.Delete(brush);
        }
    }

    /// <summary>A label in a dark rounded box, centred on x near the bottom of the pair's rows.</summary>
    private static void GroundLabel(SKCanvas canvas, string text, float x)
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Noto Sans", SKFontStyle.Bold), 17);
        using var ink = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var box = new SKPaint { Color = new SKColor(24, 28, 36, 225), IsAntialias = true };
        float width = font.MeasureText(text) + 28;
        var r = new SKRect(x - width / 2, GroundPairBottom - 52, x + width / 2, GroundPairBottom - 18);
        canvas.DrawRoundRect(r, 8, 8, box);
        canvas.DrawText(text, x, r.Bottom - 11, SKTextAlign.Center, font, ink);
    }

    /// <summary>
    /// The window's picture with a list (a combo box at x, y in the window) open over it. The open list is a window of
    /// its own that the editor's picture leaves out, so it is taken from the display (ImageMagick's import) and drawn
    /// over the picture.
    /// </summary>
    /// <param name="e">The editor.</param>
    /// <param name="path">The picture to write.</param>
    /// <param name="x">The list's box, pixels from the window's left.</param>
    /// <param name="y">The list's box, pixels from the window's top.</param>
    private static void ShotWithList(Editor e, string path, int x, int y)
    {
        e.Shot(path);
        e.Point(x, y);
        string[] pointer = RunOnDisplay("xdotool", "getmouselocation", "--shell").Split('\n');
        RunOnDisplay("xdotool", "click", "1");
        e.Send("wait 600");
        string screen = path + ".display.png";
        try
        {
            RunOnDisplay("import", "-window", "root", screen);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            int mx = int.Parse(pointer.First(l => l.StartsWith("X=", StringComparison.Ordinal))[2..], inv);
            int my = int.Parse(pointer.First(l => l.StartsWith("Y=", StringComparison.Ordinal))[2..], inv);
            // The window's top left on the display: where the pointer is less where it was put in the window.
            var (left, top) = (mx - x, my - y);
            using var display = LoadPicture(screen);
            using var shot = LoadBitmap(path);
            using (var canvas = new SKCanvas(shot))
            {
                // The panel's column below the box, where the list opens.
                var part = new SKRectI(100, y - 20, 400, 849);
                canvas.DrawImage(display, new SKRectI(part.Left + left, part.Top + top, part.Right + left, part.Bottom + top), part, Exact);
            }
            SavePicture(shot, path);
        }
        finally
        {
            File.Delete(screen);
            RunOnDisplay("xdotool", "key", "Escape");
        }
    }

    /// <summary>
    /// concept-location-flattening.jpg: a 3 × 3 area round the Meadows altar of Eikthyr (a location of the Docs world's
    /// generated land) with View → Location flattening on: the solid ring round the flat part the game levels, the fainter
    /// ring where it blends into the land, and the note at the bottom left counting the locations.
    /// </summary>
    private void LocationFlattening(Editor e)
    {
        // The altar, at -374, -13: the middle of zone -6, 0.
        e.Send($"world {World}");
        e.Send("area -6 0 3");
        e.Send("wait 1000");
        e.Send("click Location flattening");
        e.Send("camera -374 -13 200 50 80");
        e.Send("wait 1500");
        e.Scroll(1290, 600, 6);
        e.Shot(Out("concept-location-flattening.jpg"));
    }
}
