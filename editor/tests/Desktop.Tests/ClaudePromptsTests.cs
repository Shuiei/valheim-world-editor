using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The prompts Claude's client offers (ClaudePrompts): what to make, the guide for it, and the steps.
/// </summary>
public class ClaudePromptsTests
{
    [Fact]
    public void BuildingInTheWorkshop()
    {
        string prompt = ClaudePrompts.BuildInWorkshop("a longhouse");
        Assert.StartsWith("In Valheim World Editor, build this in the Workshop: a longhouse", prompt);
        Assert.Contains(ClaudeGuide.Building, prompt);
        Assert.Contains("support_check", prompt);
        Assert.Contains("save_blueprint only when I ask", prompt);
    }

    [Fact]
    public void ShapingAnArea()
    {
        string prompt = ClaudePrompts.ShapeArea("a river valley");
        Assert.StartsWith("In Valheim World Editor, change the area of the open world: a river valley", prompt);
        Assert.Contains(ClaudeGuide.Shaping, prompt);
        Assert.Contains("dryRun first", prompt);
        Assert.Contains("I save it", prompt);
    }
}
