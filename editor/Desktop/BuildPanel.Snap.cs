using TerrainEditor.App;
namespace TerrainEditor.Desktop;

/// <summary>
/// The building bar's snapping: Auto (as the game's hammer), Manual (only the piece's snap point chosen with Q and E,
/// reaching further) or Free (no snapping); kept between runs. Holding Alt places by a quarter metre without snapping,
/// whatever the mode.
/// </summary>
public sealed partial class BuildPanel
{
    /// <summary>The snapping chosen (the Place tool takes it when building starts).</summary>
    internal PlaceTool.SnapModes SnapMode { get; private set; }

    /// <summary>Snaps the piece this way from now on (and the next time), its button lit.</summary>
    /// <param name="mode">Auto, Manual or Free.</param>
    internal void SetSnapMode(PlaceTool.SnapModes mode)
    {
        SnapMode = mode;
        _prefs.Set("build.snapMode", mode.ToString());
        foreach (var b in SnapModes.Children.OfType<Avalonia.Controls.Button>())
        {
            b.Classes.Set("on", (PlaceTool.SnapModes)b.Tag! == mode);
        }
        // The Place tool is the worlds' too: it takes the mode only while building (Start gives it then).
        if (Tool.Building)
        {
            Tool.SnapMode = mode;
            Tool.SnapTo = mode != PlaceTool.SnapModes.Free;
            _input.Refresh();
            Tool.Notify();
        }
        ShowSnapPoint();
    }

    /// <summary>
    /// Snapping by hand: which of the piece's snap points meets the others ("Point 2 of 8 · Q / E"), or the keys alone
    /// for a piece without snap points; nothing in the other modes.
    /// </summary>
    private void ShowSnapPoint()
    {
        SnapPointText.IsVisible = Tool.Building && Tool.SnapMode == PlaceTool.SnapModes.Manual;
        if (SnapPointText.IsVisible)
        {
            int count = Tool.Chosen.Count == 1 ? Editing.Hammer.SnapCount(Tool.Chosen[0]) : 0;
            SnapPointText.Text = count > 0 ? Lang.T("build.snapPoint", (Tool.SnapPoint % count + count) % count + 1, count) : Lang.T("build.snapPointNone");
        }
    }
}
