using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Stamps, like the web editor's (editor/stamps.js; WorldPainter's custom brushes, Axiom's stamp tool):
/// a picture as the brush shape. White works fully, black not at all. A few are built in (mountain,
/// mesa, crater rim…); any picture can be loaded (kept in stamps.json for next time).
/// </summary>
public static partial class Stamps
{
    /// <summary>A stamp's width and height in pixels: every stamp is Size × Size weights.</summary>
    public const int Size = 128;

    /// <summary>
    /// The most memory a picture may take once decoded, in bytes (8192 × 8192 colours with opacity): a larger one is
    /// not read, rather than asking for more memory than there may be. Also the largest picture file loaded.
    /// </summary>
    internal const long MostPictureBytes = 256L * 1024 * 1024;

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

    /// <summary>The built-in stamps, always offered first.</summary>
    public static readonly Stamp[] BuiltIn =
    [
        new("stamp:mountain", "Mountain", Render((u, v) => { double d = Math.Sqrt(u * u + v * v); return d >= 1 ? 0 : Math.Pow(1 - d, 1.6) * (0.75 + 0.5 * Fractal(u * 4 + 7, v * 4 + 3)); })),
        new("stamp:mesa", "Mesa (flat top)", Render((u, v) => { double d = Math.Sqrt(u * u + v * v) * (1 + 0.12 * (Fractal(u * 3, v * 3) - 0.5)); return d >= 1 ? 0 : d < 0.62 ? 1 : 1 - Math.Pow((d - 0.62) / 0.38, 0.7); })),
        new("stamp:crater", "Crater rim", Render((u, v) => { double d = Math.Sqrt(u * u + v * v); return d >= 1 ? 0 : Math.Pow(Math.Max(0, 1 - Math.Abs(d - 0.72) / 0.28), 1.5); })),
        new("stamp:dunes", "Dunes", Render((u, v) => { double d = Math.Sqrt(u * u + v * v); if (d >= 1) { return 0; } double edge = Math.Min(1, (1 - d) * 3); return edge * Math.Pow(0.5 + 0.5 * Math.Sin(u * 9 + Fractal(u * 2, v * 2) * 3), 2); })),
        new("stamp:rocky", "Rocky ground", Render((u, v) => { double d = Math.Sqrt(u * u + v * v); if (d >= 1) { return 0; } return Math.Min(1, (1 - d) * 2.5) * Math.Max(0, Fractal(u * 6 + 11, v * 6 - 5) * 1.6 - 0.45); })),
    ];

    // ---- Loaded pictures, kept between runs (the weights themselves, 0..255 per pixel).
    /// <summary>A loaded stamp as stamps.json keeps it: the weights as base64, one byte (0..255) per pixel.</summary>
    private sealed record Kept(string Name, string Label, string Weights);
    /// <summary>Where stamps.json is instead of the data folder (the tests' and documentation's --data).</summary>
    internal static string? PathOverride { get; set; }
    /// <summary>The stamps.json file in use.</summary>
    private static string FilePath => PathOverride ?? Path.Combine(AppSettings.DataDir, "stamps.json");
}
