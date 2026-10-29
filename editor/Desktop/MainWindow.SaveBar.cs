using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>The save bar's pill: amber while something waits, grey when all is saved.</summary>
    private void SetPendingPill(bool waiting)
    {
        _pendingPill.Background = waiting ? PillWaiting : Brushes.Transparent;
        _pendingPill.BorderBrush = waiting ? PillWaitingBorder : Ui.Line;
        PendingText.Foreground = waiting ? PillWaitingText : Ui.Muted;
    }

    /// <summary>
    /// The save bar and the editor's world card as the session now stands (in the Workshop: its own save bar).
    /// </summary>
    private void UpdateSaveBar()
    {
        var s = Session;
        if (s == null)
        {
            return;
        }
        if (InWorkshop)
        {
            WorkshopSaveBar(s);
            return;
        }
        PendingText.Text = s.PendingText;
        var (z, d, a, r) = s.Pending;
        bool dirty = z + d + a + r > 0;
        SaveButton.IsEnabled = dirty;
        SaveButton.Content = s.IsLive ? Lang.T("save.label.apply") : Lang.T("save.label.save");
        SaveButton.Tip(s.IsLive ? "top.apply" : "top.save");
        SetPendingPill(dirty);
        _liveBadge.IsVisible = s.IsLive;
        UndoButton.IsEnabled = s.CanUndo;
        RedoButton.IsEnabled = s.CanRedo;
        ToolTip.SetTip(UndoButton, s.UndoLabel is string u ? Lang.T("top.undoNamed", u) : Lang.T("top.undoNone"));
        ToolTip.SetTip(RedoButton, s.RedoLabel is string rl ? Lang.T("top.redoNamed", rl) : Lang.T("top.redoNone"));
        UpdateEditorWorld(s, dirty);
    }

    /// <summary>
    /// Ctrl+Z: takes the last step back (asking first for a step from an earlier session).
    /// </summary>
    internal async Task Undo()
    {
        View.SelectTool.Commit();
        if (Session is { } s && s.UndoList.Count > 0 && await AllowEarlier([s.UndoList[^1]]))
        {
            s.Undo();
        }
        UpdateSaveBar();
    }
}
