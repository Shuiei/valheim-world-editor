using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class PathTool
{
    /// <summary>
    /// The action as the panel lists it, in both languages: the panel, the status line and the history show it in the
    /// language in use, Claude is told it in English. An action the brushes and the Area tool have too is named as
    /// they name it.
    /// </summary>
    public static Said Label(Action a) => a switch
    {
        Action.Flatten => Said.Of("brush.label.flatten"),
        Action.Ramp => Said.Of("path.action.ramp"),
        Action.Raise => Said.Of("brush.label.raise"),
        Action.Lower => Said.Of("brush.label.lower"),
        Action.Smooth => Said.Of("brush.label.smooth"),
        Action.Naturalize => Said.Of("brush.label.natural"),
        Action.Erode => Said.Of("brush.label.erode"),
        Action.Restore => Said.Of("brush.label.restore"),
        Action.River => Said.Of("path.action.river"),
        Action.PaintDirt => Said.Of("path.action.paintDirt"),
        Action.PaintPaved => Said.Of("path.action.paintPaved"),
        Action.PaintCultivated => Said.Of("path.action.paintCultivated"),
        _ => Said.Of("brush.label.clear"),
    };
}
