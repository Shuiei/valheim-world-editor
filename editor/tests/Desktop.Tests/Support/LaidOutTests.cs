using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The layout helpers, tested once here so the tests using them can rely on them.
/// </summary>
public sealed class LaidOutTests
{
    [AvaloniaFact]
    public void TheNaturalWidthGrowsWithTheText()
    {
        double shortWidth = LaidOut.NaturalWidth(new TextBlock { Text = "ab" });
        double longWidth = LaidOut.NaturalWidth(new TextBlock { Text = "ab ab ab ab ab ab ab ab ab ab" });
        Assert.True(shortWidth > 0);
        Assert.True(longWidth > shortWidth * 5);
    }

    [AvaloniaFact]
    public void InLaysTheContentOutInTheWidthGivenAndClosesTheWindow()
    {
        var box = new Border();
        Window? shown = null;
        LaidOut.In(box, 300, () =>
        {
            shown = Assert.IsType<Window>(box.Parent);
            Assert.Equal(300, box.Bounds.Width);
        });
        Assert.False(shown!.IsVisible);
    }
}
