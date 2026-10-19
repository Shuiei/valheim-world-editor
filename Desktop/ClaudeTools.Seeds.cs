using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>preview_seed: what a seed's world will be like, with a map of its biomes and landmarks.</summary>
    [McpServerTool(Name = "preview_seed", ReadOnly = true, Title = "Preview a seed")]
    [Description("What a world of this seed will be like, as the New world page shows it, without making it: a map of its biomes (north up, 10 km round the middle) with the start (white), traders (yellow) and bosses' altars (red), how much of it is land and in each biome, how far each biome is from the start, and where the start, traders and bosses will be. Use it to pick a seed before create_world; a new world's locations are only laid out by the game when it first loads it, so list_locations has none until then: these predictions stand in for them.")]
    public static async Task<ModelContextProtocol.Protocol.CallToolResult> PreviewSeed(
        [Description("The seed: 1 to 10 characters, as in the game.")] string seed)
    {
        seed = seed.Trim();
        if (seed.Length is 0 or > 10)
        {
            return new() { IsError = true, Content = { new ModelContextProtocol.Protocol.TextContentBlock { Text = "The seed is 1 to 10 characters, as in the game." } } };
        }
        await GameRules();
        var rules = ExpandWorld.ForNewWorld(AppSettings.Load());
        var p = await Task.Run(() => SeedPreview.Make(seed, 160, parallel: true, landmarks: true, rules: rules));
        var st = p.Stats;
        string text = ToJson(new
        {
            seed,
            land = MathF.Round(st.Land, 3),
            biomes = st.Shares.OrderByDescending(b => b.Value).Select(b => new { biome = b.Key.ToString(), share = MathF.Round(b.Value, 3) }),
            start = new { x = MathF.Round(st.Start.X), z = MathF.Round(st.Start.Z), temple = st.TempleStart },
            fromStart = st.Distance.Where(d => float.IsFinite(d.Value)).Select(d => new { biome = d.Key.ToString(), metres = MathF.Round(d.Value), sameLand = st.SameLand.GetValueOrDefault(d.Key) }),
            landmarks = (p.Landmarks ?? []).Select(l => new { name = l.Name, kind = l.Kind, x = MathF.Round(l.X), z = MathF.Round(l.Z), oneOfSeveral = l.OneOf }),
            note = p.Landmarks == null ? "The game's location rules could not be read: no landmarks." : null,
        });
        return new()
        {
            Content =
            {
                new ModelContextProtocol.Protocol.TextContentBlock { Text = text },
                ModelContextProtocol.Protocol.ImageContentBlock.FromBytes(SeedPicture(p), "image/jpeg"),
            },
        };
    }

    /// <summary>A seed preview's biomes as a 640-pixel picture, north up, with its start, traders and bosses as dots.</summary>
    internal static byte[] SeedPicture(SeedPreview.Preview p)
    {
        const int Side = 640;
        using var cells = new SkiaSharp.SKBitmap(p.Size, p.Size);
        for (int j = 0; j < p.Size; j++)
        {
            for (int i = 0; i < p.Size; i++)
            {
                var (r, g, b) = SeedMap.Shade(p.Biome[j * p.Size + i], p.Height[j * p.Size + i], ValheimGen.WorldRules.Vanilla.WaterLevel);
                // Row 0 is the south: drawn at the bottom.
                cells.SetPixel(i, p.Size - 1 - j, new SkiaSharp.SKColor(r, g, b));
            }
        }
        using var sheet = new SkiaSharp.SKBitmap(Side, Side);
        using (var canvas = new SkiaSharp.SKCanvas(sheet))
        {
            using (var image = SkiaSharp.SKImage.FromBitmap(cells))
            {
                canvas.DrawImage(image, SkiaSharp.SKRect.Create(0, 0, Side, Side), new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Nearest));
            }
            float k = Side / (2 * p.Radius);
            using var dot = new SkiaSharp.SKPaint { IsAntialias = true };
            using var rim = new SkiaSharp.SKPaint { IsAntialias = true, Color = SkiaSharp.SKColors.Black, Style = SkiaSharp.SKPaintStyle.Stroke, StrokeWidth = 1 };
            foreach (var l in p.Landmarks ?? [])
            {
                dot.Color = l.Kind switch { "start" => SkiaSharp.SKColors.White, "trader" => SkiaSharp.SKColors.Gold, _ => SkiaSharp.SKColors.Red };
                float x = (l.X + p.Radius) * k, y = Side - (l.Z + p.Radius) * k;
                canvas.DrawCircle(x, y, 4, dot);
                canvas.DrawCircle(x, y, 4, rim);
            }
        }
        using var final = SkiaSharp.SKImage.FromBitmap(sheet);
        return final.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 85).ToArray();
    }
}
