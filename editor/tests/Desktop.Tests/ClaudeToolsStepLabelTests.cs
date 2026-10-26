using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The label a script step gets when Claude runs it: prefixed with "Claude: " so it reads as Claude's in the history.
/// </summary>
public class ClaudeToolsStepLabelTests
{
    [Fact]
    public void AStepLabelIsPrefixedWithClaude()
    {
        Assert.Equal("Claude: Plant trees", ClaudeTools.ClaudesStep("Plant trees"));
    }
}
