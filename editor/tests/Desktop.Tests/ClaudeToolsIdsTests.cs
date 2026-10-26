using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's answers about the ids and pieces given (ClaudeTools.Ids): when nothing could be done, why for each one
/// given, or that none was.
/// </summary>
public class ClaudeToolsIdsTests
{
    [Fact]
    public void NothingDoneSaysWhyForEachOrThatNoneWasGiven()
    {
        Assert.Equal("Nothing moved: 3: not an object of the area; 9: not an object of the area",
            ClaudeTools.NothingDone("Nothing moved", ["3: not an object of the area", "9: not an object of the area"]));
        // An empty list: not an answer ending on its colon.
        Assert.Equal("Nothing put down: none was given.", ClaudeTools.NothingDone("Nothing put down", []));
    }
}
