using Avalonia.Headless.XUnit;
using ModelContextProtocol.Protocol;
using SkiaSharp;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's pictures (ClaudeTools): the area's map drawn from its data, and screenshots of the 3D view and the world's
/// map, made no wider than 1280 pixels; four views in one. Headless there are no OpenGL frames: the view's and the
/// map's pictures here are plain PNGs written in their place.
/// </summary>
[Collection("World files")]
public class ClaudeToolsPicturesTests
{
    /// <summary>A plain PNG of the given size and colour, written to <paramref name="path"/>.</summary>
    private static Task Png(string path, int w, int h, SKColor colour)
    {
        using var bitmap = new SKBitmap(w, h);
        bitmap.Erase(colour);
        using var image = SKImage.FromBitmap(bitmap);
        File.WriteAllBytes(path, image.Encode(SKEncodedImageFormat.Png, 100).ToArray());
        return Task.CompletedTask;
    }

    /// <summary>The one picture of a tool's answer, decoded.</summary>
    private static SKBitmap Picture(CallToolResult result)
    {
        Assert.NotEqual(true, result.IsError);
        var block = Assert.Single(result.Content.OfType<ImageContentBlock>());
        Assert.Equal("image/jpeg", block.MimeType);
        return SKBitmap.Decode(block.DecodedData.ToArray());
    }

    // 40 x 30 points, 12 pixels each, with a 40 pixel margin: water blue under its level, painted ground grey,
    // and a JPEG to show.
    [Fact]
    public void TheAreasMapIsDrawnFromItsData()
    {
        const int W = 40, H = 30;
        float[] heights = new float[W * H];
        float[] paint = new float[W * H * 3];
        for (int j = 0; j < H; j++)
        {
            for (int i = 0; i < W; i++)
            {
                // The west half under water, the east half dry; the north-east corner paved.
                heights[j * W + i] = i < W / 2 ? 20 : 40 + j;
                if (i >= 30 && j >= 20)
                {
                    paint[(j * W + i) * 3 + 2] = 1;
                }
            }
        }
        List<(float X, float Z, string Kind)> things = [(5, 5, "Trees"), (6, 6, "Rocks"), (7, 7, "Ore"), (8, 8, "Buildings"), (9, 9, "Pickables")];
        byte[] jpeg = ClaudeTools.DrawMap(W, H, 0, 0, 30, heights, paint, things, 10);
        using var map = SKBitmap.Decode(jpeg);
        Assert.Equal((W * 12 + 80, H * 12 + 80), (map.Width, map.Height));
        // North up: row j is drawn at 40 + (H - 1 - j) * 12. Water (west) is blue; paved ground (north-east) grey.
        var water = map.GetPixel(40 + 5 * 12 + 6, 40 + (H - 1 - 15) * 12 + 6);
        Assert.True(water.Blue > water.Red + 60, $"water {water}");
        var paved = map.GetPixel(40 + 35 * 12 + 6, 40 + (H - 1 - 25) * 12 + 6);
        Assert.True(Math.Abs(paved.Red - paved.Blue) < 40 && paved.Red > 100, $"paved {paved}");
        // Without paint and objects too.
        using var bare = SKBitmap.Decode(ClaudeTools.DrawMap(W, H, 100, -50, 30, heights, null, [], 4));
        Assert.Equal(map.Width, bare.Width);
    }

    // Wider than 1280 pixels: made 1280 wide, the same shape; smaller: kept as it is.
    [Fact]
    public void PicturesAreShrunkTo1280Wide()
    {
        static byte[] Make(int w, int h)
        {
            using var bitmap = new SKBitmap(w, h);
            bitmap.Erase(SKColors.Green);
            using var image = SKImage.FromBitmap(bitmap);
            return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        }
        using var big = SKBitmap.Decode(ClaudeTools.Shrink(Make(2560, 640)));
        Assert.Equal((1280, 320), (big.Width, big.Height));
        using var small = SKBitmap.Decode(ClaudeTools.Shrink(Make(400, 300)));
        Assert.Equal((400, 300), (small.Width, small.Height));
    }

    // area_map on the open area: its bounds and heights in words, and the map; the grid no finer than 4 m.
    [AvaloniaFact]
    public async Task TheOpenAreasMapComesWithItsBounds()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var result = await r.T.AreaMap(grid: 1, objects: false);
        string text = Assert.IsType<TextContentBlock>(result.Content[0]).Text;
        Assert.StartsWith("x -32 to 32 (left to right), z -32 to 32 (bottom to top); heights ", text);
        Assert.Contains("water at", text);
        using var map = Picture(result);
        Assert.True(map.Width > 700);
    }

    // The 3D view from a camera given, and four views in one (each 640 wide, two by two); the world's map while the map
    // is shown, where four views are refused. The pictures' files are deleted.
    [AvaloniaFact]
    public async Task ScreenshotsOfTheViewAndTheMap()
    {
        using var r = new ClaudeRun();
        var written = new List<string>();
        r.T.ViewPicture = path =>
        {
            written.Add(path);
            return Png(path, 1600, 900, SKColors.SkyBlue);
        };
        r.T.MapPicture = path =>
        {
            written.Add(path);
            return Png(path, 800, 600, SKColors.SandyBrown);
        };
        Assert.Equal("map", ClaudeRun.J(await r.T.OpenWorld(r.Dir)).GetProperty("page").GetString());
        using (var map = Picture(await r.T.Screenshot()))
        {
            Assert.Equal((800, 600), (map.Width, map.Height));
        }
        var refused = await r.T.Screenshot(views: 4);
        Assert.True(refused.IsError);
        Assert.StartsWith("Four views need", Assert.IsType<TextContentBlock>(Assert.Single(refused.Content)).Text);

        await r.T.OpenArea(0, 0, 1);
        using (var view = Picture(await r.T.Screenshot(x: 10, z: -10, yaw: 90, pitch: 30, distance: 50, height: 40)))
        {
            Assert.Equal((1280, 720), (view.Width, view.Height));
        }
        using (var four = Picture(await r.T.Screenshot(views: 4)))
        {
            Assert.Equal((1280, 720), (four.Width, four.Height));
            // Each view under its name's dark band: sky blue below it.
            var c = four.GetPixel(320, 200);
            Assert.True(c.Blue > 180 && c.Green > 150, $"view {c}");
        }
        Assert.Equal(1 + 1 + 4, written.Count);
        Assert.All(written, p => Assert.False(File.Exists(p)));
    }

    // A look at the 3D view moves the camera for its picture only: the user's camera is as it was after the picture.
    [AvaloniaFact]
    public async Task ALookAtTheViewPutsTheUsersCameraBack()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var kept = r.W.View.WorldCamera;
        object? during = null;
        string path = Path.Combine(Path.GetTempPath(), $"vwe-claude-{Guid.NewGuid():N}.png");
        r.T.ViewPicture = p =>
        {
            during = r.W.View.WorldCamera;
            return Png(p, 16, 9, SKColors.SkyBlue);
        };
        try
        {
            await r.T.ShootView(path, () => r.W.View.Orbit(0, 0, 271, 20, 50));
            Assert.NotEqual(kept, during);
            Assert.Equal(kept, r.W.View.WorldCamera);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
