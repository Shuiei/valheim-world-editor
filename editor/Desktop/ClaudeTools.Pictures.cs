using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's pictures of the 3D view (screenshot): from a camera given or as it is, no wider than 1280 pixels.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>
    /// screenshot: the 3D view (from the camera given, else as it is) or the map, as a JPEG no wider than 1280
    /// pixels; four views in one with <c>views</c> 4 or more.
    /// </summary>
    [McpServerTool(Name = "screenshot", ReadOnly = true, Title = "See the view")]
    [Description("A picture of what the editor shows: the 3D view of the area or the Workshop (optionally from a camera: looking at x, z from yaw degrees round, pitch degrees down, distance metres away), or the world's map when the map is shown. Use it to see the result of a change.")]
    public async Task<ModelContextProtocol.Protocol.CallToolResult> Screenshot(
        [Description("World x the camera looks at (optional: the view as it is).")] float? x = null,
        [Description("World z the camera looks at.")] float? z = null,
        [Description("Round the point, in degrees (0: looking north).")] float yaw = 45,
        [Description("Down from level, in degrees (90: from straight above).")] float pitch = 55,
        [Description("Metres from the point.")] float distance = 120,
        [Description("Height of the point looked at (optional: the ground's).")] float? height = null,
        [Description("4: four pictures in one (from above, and from the south-west, east and north-west, all at the point, or the area's middle), to see a building or a place from every side. 1: one picture (default).")] int views = 1)
    {
        if (LoneCoordinate(x, z) is string lone)
        {
            return new() { IsError = true, Content = { new ModelContextProtocol.Protocol.TextContentBlock { Text = lone } } };
        }
        if (views >= 4)
        {
            return await FourViews(x, z, distance, height);
        }
        string path = Path.Combine(Path.GetTempPath(), $"vwe-claude-{Guid.NewGuid():N}.png");
        try
        {
            string? problem = await WithCamera(async () =>
            {
                if (_w.MapShown)
                {
                    await MapPicture(path).WaitAsync(TimeSpan.FromSeconds(60));
                    return null;
                }
                if (_w.Session == null)
                {
                    return "Nothing to see: open a world's area (open_area) or the Workshop.";
                }
                // A read-only look: the user's camera is put back once the picture is taken (ShootView).
                Action? orbit = x is float cx && z is float cz ? () => _w.View.Orbit(cx, cz, yaw, pitch, distance, height) : null;
                await ShootView(path, orbit);
                return null;
            });
            if (problem != null)
            {
                return new() { IsError = true, Content = { new ModelContextProtocol.Protocol.TextContentBlock { Text = problem } } };
            }
            return new() { Content = { ModelContextProtocol.Protocol.ImageContentBlock.FromBytes(Shrink(await File.ReadAllBytesAsync(path)), "image/jpeg") } };
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Saves a picture of the 3D view to <paramref name="path"/>, from the camera <paramref name="orbit"/> points (none: the
    /// view as it is), and puts the user's camera back afterwards, so that a look does not move it. On the window's thread,
    /// within the camera's turn (WithCamera).
    /// </summary>
    /// <param name="path">The PNG file to write.</param>
    /// <param name="orbit">Points the camera, or null to keep it as it is.</param>
    internal async Task ShootView(string path, Action? orbit)
    {
        var kept = _w.View.WorldCamera;
        try
        {
            orbit?.Invoke();
            await ViewPicture(path).WaitAsync(TimeSpan.FromSeconds(60));
        }
        finally
        {
            _w.View.WorldCamera = kept;
        }
    }

    /// <summary>A picture no wider than 1280 pixels, as a JPEG (smaller to send and to look at).</summary>
    internal static byte[] Shrink(byte[] png)
    {
        using var bitmap = SkiaSharp.SKBitmap.Decode(png);
        float k = Math.Min(1f, 1280f / bitmap.Width);
        using var scaled = k < 1 ? bitmap.Resize(new SkiaSharp.SKImageInfo((int)(bitmap.Width * k), (int)(bitmap.Height * k)), new SkiaSharp.SKSamplingOptions(SkiaSharp.SKFilterMode.Linear)) : bitmap.Copy();
        using var image = SkiaSharp.SKImage.FromBitmap(scaled);
        return image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 82).ToArray();
    }
}
