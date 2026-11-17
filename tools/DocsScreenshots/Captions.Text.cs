using System.Globalization;
using System.Text;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// Drawing and measuring the captions: shaped with HarfBuzz as the app's text is (Thai's marks sit over their letters),
/// each character in the caption's typeface or, when it has none for it (Noto Sans has no Thai), in the one the system
/// picks for that character; and broken into lines that fit the room a caption has.
/// </summary>
public static partial class Captions
{
    /// <summary>The shapers made so far, by typeface (family, weight, width, slant): one per typeface for the whole run.</summary>
    private static readonly Dictionary<string, SKShaper> Shapers = new(StringComparer.Ordinal);

    /// <summary>A caption's width in pixels, drawn with the font (its fallback typefaces included).</summary>
    /// <param name="text">The caption, translated.</param>
    /// <param name="font">The font it is drawn with.</param>
    public static float Width(string text, SKFont font)
    {
        float width = 0;
        foreach (var (run, shaper) in Runs(text, font.Typeface))
        {
            using var runFont = RunFont(font, shaper);
            width += shaper.Shape(run, runFont).Width;
        }
        return width;
    }

    /// <summary>
    /// Draws a caption on one line, shaped, each character in a typeface that has it: from x (left), around it (centre)
    /// or up to it (right), on the baseline y.
    /// </summary>
    /// <param name="canvas">The picture's canvas.</param>
    /// <param name="text">The caption, translated.</param>
    /// <param name="x">Where it is aligned.</param>
    /// <param name="y">Its baseline.</param>
    /// <param name="align">How it is aligned on x.</param>
    /// <param name="font">The font (its typeface first).</param>
    /// <param name="paint">The ink.</param>
    public static void Draw(SKCanvas canvas, string text, float x, float y, SKTextAlign align, SKFont font, SKPaint paint)
    {
        x -= align switch
        {
            SKTextAlign.Center => Width(text, font) / 2,
            SKTextAlign.Right => Width(text, font),
            _ => 0,
        };
        foreach (var (run, shaper) in Runs(text, font.Typeface))
        {
            using var runFont = RunFont(font, shaper);
            canvas.DrawShapedText(shaper, run, x, y, SKTextAlign.Left, runFont, paint);
            x += shaper.Shape(run, runFont).Width;
        }
    }

    /// <summary>
    /// A caption in lines no wider than <paramref name="room"/>, broken at spaces (not at no-break spaces): as few lines as
    /// fit, as even as they can be.
    /// </summary>
    /// <param name="text">The caption, translated.</param>
    /// <param name="font">The font it is drawn with.</param>
    /// <param name="room">The widest a line may be, in pixels.</param>
    /// <param name="most">The most lines there is room for.</param>
    /// <returns>The lines, top first.</returns>
    /// <exception cref="InvalidOperationException">It does not fit in that many lines: a shorter translation is needed.</exception>
    public static string[] Wrap(string text, SKFont font, float room, int most)
    {
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        float widest = words.Max(w => Width(w, font));
        var lines = widest <= room ? Greedy(words, font, room) : null;
        if (lines == null || lines.Count > most)
        {
            throw new InvalidOperationException(
                $"{Current.Code} caption \"{text}\" does not fit in {most} line(s) of {room:0} pixels ({Width(text, font):0} pixels on one line): shorten it in {Current.CaptionFile}");
        }
        // The narrowest width that still takes as few lines: the lines come out even rather than one long, one short.
        float low = widest, high = room;
        while (high - low > 1)
        {
            float mid = (low + high) / 2;
            if (Greedy(words, font, mid).Count == lines.Count)
            {
                high = mid;
            }
            else
            {
                low = mid;
            }
        }
        return [.. Greedy(words, font, high)];
    }

    /// <summary>Words put on lines one after the other, a new line when the next would make it wider than the room.</summary>
    private static List<string> Greedy(string[] words, SKFont font, float room)
    {
        var lines = new List<string>();
        string line = "";
        foreach (string word in words)
        {
            string longer = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && Width(longer, font) > room)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = longer;
            }
        }
        lines.Add(line);
        return lines;
    }

    /// <summary>
    /// A caption cut into runs of one typeface each: the font's own where it has the character, else the system's choice
    /// for that character; marks and spaces stay in the run before them when its typeface has them (a Thai vowel or
    /// tone mark with its letter).
    /// </summary>
    /// <exception cref="InvalidOperationException">No typeface on this computer has a character of the caption.</exception>
    private static List<(string Text, SKShaper Shaper)> Runs(string text, SKTypeface primary)
    {
        var runs = new List<(StringBuilder Text, SKShaper Shaper)>();
        foreach (Rune r in text.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(r);
            bool clings = category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark or UnicodeCategory.SpaceSeparator;
            SKShaper shaper;
            if (runs.Count > 0 && clings && Has(runs[^1].Shaper.Typeface, r.Value))
            {
                shaper = runs[^1].Shaper;
            }
            else if (Has(primary, r.Value))
            {
                shaper = ShaperOf(primary);
            }
            else
            {
                var fallback = SKFontManager.Default.MatchCharacter(primary.FamilyName, primary.FontStyle, null, r.Value);
                if (fallback == null || !Has(fallback, r.Value))
                {
                    throw new InvalidOperationException($"caption \"{text}\": no typeface on this computer draws U+{r.Value:X4} (for Thai, install Noto Sans Thai)");
                }
                shaper = ShaperOf(fallback);
            }
            if (runs.Count > 0 && ReferenceEquals(runs[^1].Shaper, shaper))
            {
                runs[^1].Text.Append(r.ToString());
            }
            else
            {
                runs.Add((new StringBuilder(r.ToString()), shaper));
            }
        }
        return [.. runs.Select(run => (run.Text.ToString(), run.Shaper))];
    }

    /// <summary>Whether a typeface has a glyph for a character.</summary>
    private static bool Has(SKTypeface typeface, int codepoint)
    {
        using var font = new SKFont(typeface);
        return font.ContainsGlyph(codepoint);
    }

    /// <summary>The shaper of a typeface (one per family, weight, width and slant), made the first time.</summary>
    private static SKShaper ShaperOf(SKTypeface typeface)
    {
        string key = $"{typeface.FamilyName}|{typeface.FontWeight}|{typeface.FontWidth}|{typeface.FontSlant}";
        if (!Shapers.TryGetValue(key, out var shaper))
        {
            Shapers[key] = shaper = new SKShaper(typeface);
        }
        return shaper;
    }

    /// <summary>
    /// The font a run is drawn with: the caption's size in the run's typeface, emboldened when the caption is bold and
    /// the system's typeface for the run is not.
    /// </summary>
    private static SKFont RunFont(SKFont font, SKShaper shaper) => new(shaper.Typeface, font.Size)
    {
        Embolden = font.Typeface.FontWeight >= (int)SKFontStyleWeight.SemiBold && shaper.Typeface.FontWeight < (int)SKFontStyleWeight.SemiBold,
        Edging = font.Edging,
        Subpixel = font.Subpixel,
    };
}
