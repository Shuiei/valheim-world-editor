using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The point a Claude tool turns or copies things about: a pivot given is used as it is, on both axes.
/// </summary>
public class ClaudeToolsPivotTests
{
    [Fact]
    public void AGivenPivotIsUsedAsItIs()
    {
        Assert.Equal((3f, 4f), ClaudeTools.PivotOf(null!, [], 3f, 4f));
    }
}
