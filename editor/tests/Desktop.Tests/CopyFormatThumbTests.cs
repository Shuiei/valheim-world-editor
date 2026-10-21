using SkiaSharp;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A copy's thumbnail: each object a dot in its kind's colour (rocks grey, ore orange, bushes green, things picked by
/// hand yellow, trees dark green, the game's structures violet).
/// </summary>
public class CopyFormatThumbTests
{
    [Fact]
    public void EachKindOfObjectIsADotOfItsColour()
    {
        var clip = BlueprintsPanelRun.Clip("Mix", "Rock_4", "MineRock_Copper", "Bush01", "Pickable_Mushroom", "Beech1", "goblin_fence");
        string url = CopyFormat.Thumb(clip);
        Assert.StartsWith("data:image/png;base64,", url);
        using var bmp = SKBitmap.Decode(Convert.FromBase64String(url["data:image/png;base64,".Length..]));
        var pixels = bmp.Pixels;
        foreach (string colour in new[] { "#9a9a9a", "#e0803a", "#7cbf5a", "#e8d24a", "#2f6b2a", "#a08cc8" })
        {
            var c = SKColor.Parse(colour);
            Assert.Contains(pixels, p => Math.Abs(p.Red - c.Red) < 12 && Math.Abs(p.Green - c.Green) < 12 && Math.Abs(p.Blue - c.Blue) < 12);
        }
    }
}
