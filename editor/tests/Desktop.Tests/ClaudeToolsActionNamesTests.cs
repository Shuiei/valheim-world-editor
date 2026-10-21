using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// How Claude's ground and path actions are named in the history and the status bar (ClaudeTools.GroundStep, PathStep):
/// each action its own catalogue text, a paint action named after the paint, and the path's smoothing for anything else.
/// </summary>
public sealed class ClaudeToolsActionNamesTests
{
    [Theory]
    [InlineData(AreaTool.GroundAction.Flatten, "claude.undo.flatten")]
    [InlineData(AreaTool.GroundAction.Raise, "claude.undo.raise")]
    [InlineData(AreaTool.GroundAction.Lower, "claude.undo.lower")]
    [InlineData(AreaTool.GroundAction.Smooth, "claude.undo.smooth")]
    [InlineData(AreaTool.GroundAction.Natural, "claude.undo.naturalize")]
    [InlineData(AreaTool.GroundAction.Restore, "claude.undo.restore")]
    [InlineData(AreaTool.GroundAction.Erode, "claude.undo.erode")]
    public void EachGroundActionIsNamedAsItsHistoryStep(AreaTool.GroundAction act, string key)
    {
        Assert.Equal(Said.Of(key), ClaudeTools.GroundStep(act, BrushTool.PaintDirt));
    }

    [Theory]
    [InlineData(BrushTool.PaintDirt, "claude.undo.paintDirt")]
    [InlineData(BrushTool.PaintCultivated, "claude.undo.paintCultivated")]
    [InlineData(BrushTool.PaintPaved, "claude.undo.paintPaved")]
    [InlineData(BrushTool.Raise, "claude.undo.paintClear")]
    public void APaintActionIsNamedAfterThePaint(BrushTool paint, string key)
    {
        Assert.Equal(Said.Of(key), ClaudeTools.GroundStep(AreaTool.GroundAction.Paint, paint));
    }

    [Theory]
    [InlineData(PathTool.Action.River, "claude.undo.riverLine")]
    [InlineData(PathTool.Action.Cave, "claude.undo.caveLine")]
    [InlineData(PathTool.Action.Ramp, "claude.undo.rampLine")]
    [InlineData(PathTool.Action.Flatten, "claude.undo.flattenLine")]
    [InlineData(PathTool.Action.Raise, "claude.undo.raiseLine")]
    [InlineData(PathTool.Action.Lower, "claude.undo.lowerLine")]
    [InlineData(PathTool.Action.Smooth, "claude.undo.smoothLine")]
    public void EachPathActionIsNamedAsItsHistoryStep(PathTool.Action act, string key)
    {
        Assert.Equal(Lang.T(key), ClaudeTools.PathStep(act));
    }
}
