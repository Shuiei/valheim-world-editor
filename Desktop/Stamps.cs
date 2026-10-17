using System.Text.Json;
using SkiaSharp;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Stamps, like the web editor's (editor/stamps.js; WorldPainter's custom brushes, Axiom's stamp tool):
/// a picture as the brush shape. White works fully, black not at all. A few are built in (mountain,
/// mesa, crater rim…); any picture can be loaded (kept in stamps.json for next time).
/// </summary>
public static class Stamps
{
    /// <summary>A stamp's width and height in pixels: every stamp is Size × Size weights.</summary>
    public const int Size = 128;

    /// <summary>One stamp offered in the brush's shape list.</summary>
    /// <param name="Name">Its key (the built-in ones start with "stamp:").</param>
    /// <param name="Label">Its name in English (Claude asks for a stamp by it); a loaded one's is its picture's name.</param>
    /// <param name="Data">Size × Size weights, 0..1, row by row from the north.</param>
    /// <param name="Loaded">Loaded from a picture (kept in stamps.json and can be forgotten), not built in.</param>
    public sealed record Stamp(string Name, string Label, float[] Data, bool Loaded = false)
    {
        /// <summary>Its name in the language in use (a loaded one's is its picture's name).</summary>
        public string Shown => Name switch
        {
            "stamp:mountain" => Lang.T("brush.stamp.mountain"),
            "stamp:mesa" => Lang.T("brush.stamp.mesa"),
            "stamp:crater" => Lang.T("brush.stamp.crater"),
            "stamp:dunes" => Lang.T("brush.stamp.dunes"),
            "stamp:rocky" => Lang.T("brush.stamp.rocky"),
            _ => Label,
        };

        /// <summary>
        /// Its entry in the brush's Shape list. A built-in one's is a whole catalogue text, so the remembered choice
        /// (Prefs, kept in English) comes back in any language.
        /// </summary>
        public string Item => Name switch
        {
            "stamp:mountain" => Lang.T("brush.shape.stampMountain"),
            "stamp:mesa" => Lang.T("brush.shape.stampMesa"),
            "stamp:crater" => Lang.T("brush.shape.stampCrater"),
            "stamp:dunes" => Lang.T("brush.shape.stampDunes"),
            "stamp:rocky" => Lang.T("brush.shape.stampRocky"),
            _ => Lang.T("brush.shape.stamp", Label),
        };
    }

    /// <summary>The web editor's value noise (the same numbers, so the built-in stamps look the same).</summary>
    private static double Hash(double x, double y)
    {
        double s = Math.Sin(x * 127.1 + y * 311.7) * 43758.5453;
        return s - Math.Floor(s);
    }

    /// <summary>Smooth value noise, 0..1, between the hashes of the four nearest whole points.</summary>
    private static double VNoise(double x, double y)
    {
        double ix = Math.Floor(x), iy = Math.Floor(y), fx = x - ix, fy = y - iy, sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
        double a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
        return (a + (b - a) * sx) * (1 - sy) + (c + (d - c) * sx) * sy;
    }

    /// <summary>Four octaves of <c>VNoise</c>, scaled back to about 0..1.</summary>
    private static double Fractal(double x, double y)
    {
        double s = 0, a = 0.5;
        for (int o = 0; o < 4; o++)
        {
            s += VNoise(x, y) * a;
            x *= 2.03;
            y *= 2.03;
            a *= 0.5;
        }
        return s / 0.9375;
    }

    /// <summary>
    /// Fills a stamp from a function of (u, v) in -1..1, north up; values are clamped to 0..1.
    /// </summary>
    private static float[] Render(Func<double, double, double> f)
    {
        float[] data = new float[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                data[y * Size + x] = (float)Math.Clamp(f(x / (double)(Size - 1) * 2 - 1, 1 - y / (double)(Size - 1) * 2), 0, 1);
            }
        }
        return data;
    }

    /// <summary>The built-in stamps, always offered first.</summary>
    public static readonly Stamp[] BuiltIn =
    [
        new("stamp:mountain", "Mountain", Render((u, v) => { double d = Math.Sqrt(u * u + v * v); return d >= 1 ? 0 : Math.Pow(1 - d, 1.6) * (0.75 + 0.5 * Fractal(u * 4 + 7, v * 4 + 3)); })),
        new("stamp:mesa", "Mesa (flat top)", Render((u, v) => { double d = Math.Sqrt(u * u + v * v) * (1 + 0.12 * (Fractal(u * 3, v * 3) - 0.5)); return d >= 1 ? 0 : d < 0.62 ? 1 : 1 - Math.Pow((d - 0.62) / 0.38, 0.7); })),
        new("stamp:crater", "Crater rim", Render((u, v) => { double d = Math.Sqrt(u * u + v * v); return d >= 1 ? 0 : Math.Pow(Math.Max(0, 1 - Math.Abs(d - 0.72) / 0.28), 1.5); })),
        new("stamp:dunes", "Dunes", Render((u, v) => { double d = Math.Sqrt(u * u + v * v); if (d >= 1) { return 0; } double edge = Math.Min(1, (1 - d) * 3); return edge * Math.Pow(0.5 + 0.5 * Math.Sin(u * 9 + Fractal(u * 2, v * 2) * 3), 2); })),
        new("stamp:rocky", "Rocky ground", Render((u, v) => { double d = Math.Sqrt(u * u + v * v); if (d >= 1) { return 0; } return Math.Min(1, (1 - d) * 2.5) * Math.Max(0, Fractal(u * 6 + 11, v * 6 - 5) * 1.6 - 0.45); })),
    ];

    /// <summary>The weight at (u, v) in -1..1 (north up), between the four nearest pixels; 0 outside.</summary>
    public static float Sample(float[] data, float u, float v)
    {
        if (u <= -1 || u >= 1 || v <= -1 || v >= 1)
        {
            return 0;
        }
        float x = (u + 1) / 2 * (Size - 1), y = (1 - (v + 1) / 2) * (Size - 1);
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float tx = x - x0, ty = y - y0;
        float At(int a, int b) => data[Math.Min(Size - 1, b) * Size + Math.Min(Size - 1, a)];
        return (At(x0, y0) * (1 - tx) + At(x0 + 1, y0) * tx) * (1 - ty) + (At(x0, y0 + 1) * (1 - tx) + At(x0 + 1, y0 + 1) * tx) * ty;
    }

    /// <summary>A picture (any size) to Size × Size weights: brightness times opacity. Null when it cannot be read.</summary>
    /// <param name="bytes">The picture file's contents, in any format Skia decodes.</param>
    public static float[]? FromPicture(byte[] bytes)
    {
        // Not a picture: no codec (Decode would throw).
        using var stream = new SKMemoryStream(bytes);
        using var codec = SKCodec.Create(stream);
        if (codec == null)
        {
            return null;
        }
        using var src = SKBitmap.Decode(codec);
        if (src == null)
        {
            return null;
        }
        using var small = src.Resize(new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Unpremul), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        if (small == null)
        {
            return null;
        }
        float[] data = new float[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                var c = small.GetPixel(x, y);
                data[y * Size + x] = (0.299f * c.Red + 0.587f * c.Green + 0.114f * c.Blue) / 255f * (c.Alpha / 255f);
            }
        }
        return data;
    }

    // ---- Loaded pictures, kept between runs (the weights themselves, 0..255 per pixel).
    /// <summary>A loaded stamp as stamps.json keeps it: the weights as base64, one byte (0..255) per pixel.</summary>
    private sealed record Kept(string Name, string Label, string Weights);
    /// <summary>Where stamps.json is instead of the data folder (the tests' and documentation's --data).</summary>
    internal static string? PathOverride { get; set; }
    /// <summary>The stamps.json file in use.</summary>
    private static string FilePath => PathOverride ?? Path.Combine(AppSettings.DataDir, "stamps.json");

    /// <summary>
    /// The loaded stamps kept from earlier runs; empty when there are none or the file cannot be read. Entries
    /// without weights or of the wrong size are left out.
    /// </summary>
    public static List<Stamp> LoadKept()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }
            // (Entries without their weights, as a file changed by hand can have, are left out.)
            return [.. (JsonSerializer.Deserialize<List<Kept?>>(File.ReadAllText(FilePath)) ?? [])
                .Where(k => k is { Name: not null, Weights: not null }).Select(k => k!)
                .Select(k => new Stamp(k.Name, k.Label, [.. Convert.FromBase64String(k.Weights).Select(b => b / 255f)], Loaded: true))
                .Where(s => s.Data.Length == Size * Size)];
        }
        catch (Exception ex) when (ex is IOException or JsonException or FormatException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "reading stamps.json (starting with none)");
            return [];
        }
    }

    /// <summary>
    /// Writes the loaded stamps to stamps.json, replacing it whole (a write cut short leaves the old file); a failure
    /// to write is logged, and the stamps stay loaded for this run.
    /// </summary>
    public static void SaveKept(IEnumerable<Stamp> loaded)
    {
        try
        {
            var list = loaded.Select(s => new Kept(s.Name, s.Label, Convert.ToBase64String(s.Data.Select(v => (byte)Math.Clamp((int)MathF.Round(v * 255), 0, 255)).ToArray()))).ToList();
            SafeFile.WriteAllText(FilePath, JsonSerializer.Serialize(list));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "writing stamps.json");
        }
    }
}
