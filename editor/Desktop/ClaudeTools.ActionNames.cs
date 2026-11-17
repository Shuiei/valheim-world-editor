using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>An Area tool action as Claude's history step and status bar name it ("smooth", "paint dirt").</summary>
    /// <param name="act">The action.</param>
    /// <param name="paint">The paint brush, for Paint.</param>
    internal static Said GroundStep(AreaTool.GroundAction act, BrushTool paint) => act switch
    {
        AreaTool.GroundAction.Flatten => Said.Of("claude.undo.flatten"),
        AreaTool.GroundAction.Raise => Said.Of("claude.undo.raise"),
        AreaTool.GroundAction.Lower => Said.Of("claude.undo.lower"),
        AreaTool.GroundAction.Smooth => Said.Of("claude.undo.smooth"),
        AreaTool.GroundAction.Natural => Said.Of("claude.undo.naturalize"),
        AreaTool.GroundAction.Restore => Said.Of("claude.undo.restore"),
        AreaTool.GroundAction.Erode => Said.Of("claude.undo.erode"),
        _ => PaintStep(paint),
    };

    /// <summary>A Path tool action along Claude's line, as its history step names it, in the user's language.</summary>
    /// <param name="act">The action (one of PathActions).</param>
    internal static string PathStep(PathTool.Action act) => act switch
    {
        PathTool.Action.River => Lang.T("claude.undo.riverLine"),
        PathTool.Action.Ramp => Lang.T("claude.undo.rampLine"),
        PathTool.Action.Flatten => Lang.T("claude.undo.flattenLine"),
        PathTool.Action.Raise => Lang.T("claude.undo.raiseLine"),
        PathTool.Action.Lower => Lang.T("claude.undo.lowerLine"),
        PathTool.Action.Naturalize => Lang.T("claude.undo.naturalizeLine"),
        PathTool.Action.Erode => Lang.T("claude.undo.erodeLine"),
        PathTool.Action.Restore => Lang.T("claude.undo.restoreLine"),
        _ => Lang.T("claude.undo.smoothLine"),
    };
}
