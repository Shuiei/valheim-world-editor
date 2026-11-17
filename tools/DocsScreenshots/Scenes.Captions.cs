using System.Reflection;
using SkiaSharp;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// The captions the scenes draw on their pictures, in the pictures' language (Captions.cs): the names written on shots
/// and the labels of before and after pairs, each set a field (<see cref="Name"/> arrays, <see cref="Pair"/>s) so that
/// the check finds them all, and the check itself.
/// </summary>
public sealed partial class Scenes
{
    /// <summary>The shots' size in pixels (the editor's window): the pictures names are written on.</summary>
    private const int ShotWidth = 1440, ShotHeight = 900;

    /// <summary>A name's box: the room left and right of its text, the height of a line, the room kept between two boxes.</summary>
    private const float NamePad = 8, NameLine = 21, NameGap = 4;

    /// <summary>A name written on a shot, white on a dark rounded box (<see cref="WriteNames"/>).</summary>
    /// <param name="Text">The name, in English (written in the pictures' language).</param>
    /// <param name="X">The box's middle, or its left edge when <paramref name="FromLeft"/>.</param>
    /// <param name="Y">The text's baseline (the middle line's, on two lines).</param>
    /// <param name="FromLeft">The box starts at X and grows right (a name at the end of a line drawn to it).</param>
    private readonly record struct Name(string Text, int X, int Y, bool FromLeft = false);

    /// <summary>The two labels of a before and after pair (<see cref="Halves"/>), in English.</summary>
    /// <param name="Before">The left half's.</param>
    /// <param name="After">The right half's.</param>
    private readonly record struct Pair(string Before, string After);

    /// <summary>
    /// Names written on a picture, white on dark rounded boxes, in the pictures' language. A name that would overlap
    /// another or leave the picture goes on two lines (the widest first) until none does.
    /// </summary>
    /// <param name="canvas">The picture's canvas.</param>
    /// <param name="width">The picture's width.</param>
    /// <param name="height">The picture's height.</param>
    /// <param name="names">The names.</param>
    /// <exception cref="InvalidOperationException">Names still overlap or leave the picture on two lines: shorter
    /// translations are needed.</exception>
    private static void WriteNames(SKCanvas canvas, int width, int height, Name[] names)
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Noto Sans", SKFontStyle.Bold), 17);
        using var box = new SKPaint { Color = new SKColor(20, 24, 30, 210), IsAntialias = true };
        using var ink = new SKPaint { Color = SKColors.White, IsAntialias = true };
        string[] texts = [.. names.Select(n => Captions.Tr(n.Text))];
        string[][] lines = [.. texts.Select(t => new[] { t })];
        SKRect Box(int i)
        {
            float w = lines[i].Max(l => Captions.Width(l, font)), extra = (lines[i].Length - 1) * NameLine / 2;
            float left = names[i].FromLeft ? names[i].X : names[i].X - (w / 2) - NamePad;
            return new SKRect(left, names[i].Y - 15 - extra, left + w + (2 * NamePad), names[i].Y + 9 + extra);
        }
        while (Clash([.. Enumerable.Range(0, names.Length).Select(Box)], width, height) is { } clash)
        {
            int[] wrappable = [.. clash.Where(k => lines[k].Length == 1 && texts[k].Contains(' '))];
            if (wrappable.Length == 0)
            {
                string what = string.Join(" and ", clash.Select(k => $"\"{texts[k]}\""));
                throw new InvalidOperationException($"{Captions.Current.Code} names {what} overlap or leave the picture even on two lines: shorten them in {Captions.Current.CaptionFile}");
            }
            int widest = wrappable.MaxBy(k => Box(k).Width);
            lines[widest] = Captions.Wrap(texts[widest], font, Captions.Width(texts[widest], font) - 1, most: 2);
        }
        for (int i = 0; i < names.Length; i++)
        {
            var r = Box(i);
            canvas.DrawRoundRect(r, 6, 6, box);
            for (int k = 0; k < lines[i].Length; k++)
            {
                float baseline = r.Top + 15 + (k * NameLine);
                if (names[i].FromLeft)
                {
                    Captions.Draw(canvas, lines[i][k], r.Left + NamePad, baseline, SKTextAlign.Left, font, ink);
                }
                else
                {
                    Captions.Draw(canvas, lines[i][k], names[i].X, baseline, SKTextAlign.Center, font, ink);
                }
            }
        }
    }

    /// <summary>The first box that leaves the picture, or the first two that overlap; null when none.</summary>
    private static int[]? Clash(SKRect[] boxes, int width, int height)
    {
        for (int i = 0; i < boxes.Length; i++)
        {
            if (boxes[i].Left < 0 || boxes[i].Top < 0 || boxes[i].Right > width || boxes[i].Bottom > height)
            {
                return [i];
            }
            for (int j = 0; j < i; j++)
            {
                var near = boxes[j];
                near.Inflate(NameGap, NameGap);
                if (near.IntersectsWith(boxes[i]))
                {
                    return [j, i];
                }
            }
        }
        return null;
    }

    /// <summary>
    /// A pair's label in a dark rounded box, centred on x near the bottom of the pair's rows, in the pictures' language;
    /// on two lines (the box growing up) when it is wider than its half.
    /// </summary>
    /// <param name="canvas">The picture's canvas.</param>
    /// <param name="english">The label, in English.</param>
    /// <param name="x">The box's middle.</param>
    /// <param name="room">The widest the box may be (its half of the pair, less a margin).</param>
    /// <exception cref="InvalidOperationException">It does not fit on two lines: a shorter translation is needed.</exception>
    private static void PairLabel(SKCanvas canvas, string english, float x, float room)
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Noto Sans", SKFontStyle.Bold), 17);
        using var ink = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var box = new SKPaint { Color = new SKColor(24, 28, 36, 225), IsAntialias = true };
        string[] lines = Captions.Wrap(Captions.Tr(english), font, room - 28, most: 2);
        float width = lines.Max(l => Captions.Width(l, font)) + 28;
        var r = new SKRect(x - (width / 2), GroundPairBottom - 52 - ((lines.Length - 1) * NameLine), x + (width / 2), GroundPairBottom - 18);
        canvas.DrawRoundRect(r, 8, 8, box);
        for (int k = 0; k < lines.Length; k++)
        {
            Captions.Draw(canvas, lines[k], x, r.Top + 23 + (k * NameLine), SKTextAlign.Center, font, ink);
        }
    }

    /// <summary>
    /// <c>--captions-check</c>, also run before the scenes in the pictures' language: in each language, every set of names
    /// drawn on a blank shot, every pair's labels on a blank shot with the narrowest halves the scenes use, and the drawn
    /// diagrams drawn, as the scenes draw them (no app needed). Each caption must have a translation, fit where it goes,
    /// and each caption file entry must be drawn by some scene.
    /// </summary>
    /// <param name="codes">The languages checked.</param>
    /// <param name="pictures">The folder the check's pictures are kept in, one folder per language, to look at; null: a
    /// temporary folder, deleted.</param>
    /// <returns>0 when all is well; 1 otherwise, each problem written out.</returns>
    public static int CheckCaptions(IReadOnlyList<string> codes, string? pictures)
    {
        string root = pictures ?? Directory.CreateTempSubdirectory("vwe-captions-").FullName;
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var nameSets = typeof(Scenes).GetFields(flags).Where(f => f.FieldType == typeof(Name[])).ToList();
        var pairs = typeof(Scenes).GetFields(flags).Where(f => f.FieldType == typeof(Pair)).ToList();
        int failed = 0;
        try
        {
            foreach (string code in codes)
            {
                var tr = Captions.Use(code, collect: true);
                string dir = System.IO.Path.Combine(root, code);
                Directory.CreateDirectory(dir);
                var problems = new List<string>();
                // Each set drawn on its own, so that one that does not fit does not hide the others' problems.
                void Try(string what, Action draw)
                {
                    try
                    {
                        draw();
                    }
                    catch (InvalidOperationException ex)
                    {
                        problems.Add($"{what}: {ex.Message}");
                    }
                }
                foreach (var f in nameSets)
                {
                    Try(f.Name, () => Blank(System.IO.Path.Combine(dir, f.Name + ".jpg"), canvas => WriteNames(canvas, ShotWidth, ShotHeight, (Name[])f.GetValue(null)!)));
                }
                // The narrowest halves: the widest panel left of a pair.
                int left = Math.Max(GroundPairLeft, Math.Max(AreaPanelRight, LandformPanelRight)), half = (ShotWidth - left) / 2;
                foreach (var f in pairs)
                {
                    var pair = (Pair)f.GetValue(null)!;
                    Try(f.Name, () => Blank(System.IO.Path.Combine(dir, f.Name + ".jpg"), canvas =>
                    {
                        PairLabel(canvas, pair.Before, left + (half / 2f), half - 16);
                        PairLabel(canvas, pair.After, left + (half * 1.5f), half - 16);
                    }));
                }
                var scenes = new Scenes(root, dir);
                foreach (var (name, draw) in scenes.Drawn)
                {
                    Try(name, draw);
                }
                // A set stopped by a problem did not ask for its later captions: the unused entries are only known when none was.
                bool whole = problems.Count == 0;
                problems.AddRange(tr.Missing.Order(StringComparer.Ordinal).Select(m => $"no translation of \"{m}\": add it to {tr.CaptionFile}"));
                if (whole)
                {
                    problems.AddRange(tr.FileEntries.Where(e => !tr.Asked.Contains(e)).Order(StringComparer.Ordinal)
                        .Select(e => $"\"{e}\" in {tr.CaptionFile} is drawn by no scene: remove it"));
                }
                Console.WriteLine(problems.Count == 0 ? $"{code}: {tr.Asked.Count} captions, all translated and fitting" : $"{code}: {problems.Count} problem(s)");
                foreach (string p in problems)
                {
                    Console.WriteLine("  " + p);
                }
                failed += problems.Count;
            }
        }
        finally
        {
            Captions.Use(Captions.AppLanguage());
            if (pictures == null)
            {
                Directory.Delete(root, recursive: true);
            }
        }
        return failed == 0 ? 0 : 1;
    }

    /// <summary>A blank shot (the app's background) drawn on and saved: what the check draws captions on.</summary>
    private static void Blank(string path, Action<SKCanvas> draw)
    {
        using var picture = new SKBitmap(ShotWidth, ShotHeight);
        using (var canvas = new SKCanvas(picture))
        {
            canvas.Clear(Bg);
            draw(canvas);
        }
        SavePicture(picture, path);
    }
}
