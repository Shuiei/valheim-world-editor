using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The heightmap picture Claude names: a file that cannot be read gives no picture and the reason, with the file's own
/// message, so Claude can say what is wrong with it.
/// </summary>
[Collection("DataDir")]
public sealed class ClaudeToolsHeightmapPictureTests
{
    /// <summary>A picture file that is not there gives no picture, and says why it cannot be used.</summary>
    [Fact]
    public void AMissingPictureGivesNoPictureAndTheReason()
    {
        string missing = Path.Combine(Path.GetTempPath(), "vwe-no-heightmap-" + Guid.NewGuid().ToString("N")[..8] + ".png");

        var (picture, problem) = ClaudeTools.PictureOf(missing, 64, 64);

        Assert.Null(picture);
        Assert.NotNull(problem);
        Assert.StartsWith("That picture cannot be used: ", problem);
    }
}
