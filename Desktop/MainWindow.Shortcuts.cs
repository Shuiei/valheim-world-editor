using Avalonia.Controls;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Ctrl + wheel: pasting, the paste is lowered (into a mountain: Clear the site digs it out) or raised; building
    /// (the Workshop), the piece is lifted; otherwise (false) the wheel is not taken.
    /// </summary>
    /// <param name="dir">The wheel's direction: 1 up, -1 down.</param>
    /// <param name="shift">Shift held: finer steps.</param>
    /// <returns>Whether a tool took it.</returns>
    private bool CtrlWheel(int dir, bool shift)
    {
        // Pasting: the paste lowered (into a mountain: Clear the site digs it out) or raised.
        if (Tools.Mode == ToolMode.Paste)
        {
            PastePanel.OffsetBox.Value = Math.Round((PastePanel.OffsetBox.Value ?? 0) + dir * (shift ? 0.1m : 0.5m), 2);
            Msg("view.msg.pasteHeight", PastePanel.OffsetBox.Value);
            return true;
        }
        if (Tools.Mode != ToolMode.Place || !PlaceTool.Building)
        {
            return false;
        }
        PlaceInput.Lift(dir * (shift ? 0.1f : 0.5f));
        BuildPanel.ShowLift();
        return true;
    }

    /// <summary>
    /// The status bar's cursor readout follows the mouse over the 3D view (and is read again after a stroke).
    /// </summary>
    /// <param name="surface">The control that takes the mouse for the 3D view.</param>
    private void TrackCursor(Control surface)
    {
        surface.PointerMoved += (_, e) => ShowCursor(e.GetPosition(surface));
        surface.PointerExited += (_, _) => ShowCursor(null);
        View.StrokeEnded += _ => ShowCursor(_cursorAt);
    }

    /// <summary>
    /// Shows the 3D view's report in the help card's frame rate line, in the language in use.
    /// </summary>
    /// <param name="s">The view's report: frames, work, objects, draws and the kinds still loading.</param>
    internal void ShowStats(GlView.Stats s) => _fps.Text = Lang.T("help.fps", s.Fps, s.WorkMs, s.Objects, s.Instances, s.Batches)
        + (s.PendingModels > 0 ? " " + Lang.T("help.fpsLoading", s.PendingModels) : "");

    /// <summary>
    /// What the 3D view reports, shown: frame rates, the selection (the Select panel, the Inspector and the status
    /// line follow it), walking or flying, and its status lines.
    /// </summary>
    private void WireViewReports()
    {
        View.StatsChanged += ShowStats;
        View.SelectionChanged += _ => FollowSelection();
        View.SelectionChanged += things => _selection.Text = things.Count == 0 ? "" : things.Count == 1
            ? Lang.T("view.selected.one", ThingName(things[0]), things[0].Position.X, things[0].Position.Z, things[0].Position.Y)
            : Lang.T("view.selected.many", things.Count, string.Join(", ", things.GroupBy(ThingName).OrderByDescending(g => g.Count()).Take(4).Select(g => $"{g.Key} ×{g.Count()}")));
        View.EyeChanged += e => _eye.Text = e switch
        {
            GlView.EyeMode.Walk => Lang.T("view.eye.walk"),
            GlView.EyeMode.Fly => Lang.T("view.eye.fly"),
            _ => "",
        };
        View.Status += t => { Options.Say(t.English); Dispatcher.UIThread.Post(() => LoadedInfo.Text = t.Shown + "\n" + LoadedInfo.Text); };
    }

    /// <summary>
    /// The selection changed: the Select panel shows it, and an open Inspector follows a single selected object.
    /// </summary>
    private void FollowSelection()
    {
        SelectPanel.Refresh();
        // Open: it follows the selection.
        var sel = View.Selected;
        if (Inspector.IsOpen && sel.Count == 1 && sel.First() != Inspector.Index)
        {
            Inspect();
        }
    }
}
