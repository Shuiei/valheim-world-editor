using TerrainEditor.App;
namespace TerrainEditor.Desktop;

/// <summary>
/// The building bar's settings as the dock uses them: Auto (as the game's hammer), Manual (only the piece's snap point
/// chosen with Q and E, reaching further) or Free (no snapping), the grid and the steps; kept between runs. Holding
/// Alt places by a quarter metre without snapping, whatever the mode.
/// </summary>
public sealed partial class BuildPanel
{
    /// <summary>The snapping chosen (the Place tool takes it when building starts).</summary>
    internal PlaceTool.SnapModes SnapMode => Bar.SnapMode;

    /// <summary>Snaps the piece this way from now on (and the next time), its button lit.</summary>
    /// <param name="mode">Auto, Manual or Free.</param>
    internal void SetSnapMode(PlaceTool.SnapModes mode) => Bar.SetSnapMode(mode);

    /// <summary>
    /// The building bar's settings given to the Place tool: only while building (the Place tool is the worlds' too, and
    /// the bar Select's and Paste's).
    /// </summary>
    private void TakeBar()
    {
        if (Tool.Building)
        {
            Tool.SnapMode = Bar.SnapMode;
            Tool.SnapTo = Bar.Snaps;
            Tool.GridStep = Bar.GridStep;
            _input.TurnStep = Bar.TurnStep;
            _input.LiftStep = Bar.LiftStep;
            _input.Refresh();
            Tool.Notify();
        }
        ShowSnapPoint();
    }

    /// <summary>
    /// Snapping by hand: which of the piece's snap points meets the others ("Point 2 of 8 · Q / E"), or the keys alone
    /// for a piece without snap points; nothing in the other modes, nor while laying a line.
    /// </summary>
    private void ShowSnapPoint()
    {
        if (!(Tool.Hammering && Tool.SnapMode == PlaceTool.SnapModes.Manual))
        {
            // Not the dock's to hide while another tool has the bar.
            if (Bar.Use is BuildingBar.Uses.Build or BuildingBar.Uses.None)
            {
                Bar.ShowSnapPoint(null);
            }
            return;
        }
        int count = Tool.Chosen.Count == 1 ? Editing.Hammer.SnapCount(Tool.Chosen[0]) : 0;
        Bar.ShowSnapPoint(count > 0 ? Lang.T("build.snapPoint", (Tool.SnapPoint % count + count) % count + 1, count) : Lang.T("build.snapPointNone"));
    }
}
