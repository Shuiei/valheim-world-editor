using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class HistoryPanel
{
    /// <summary>Rebuilds the rows from the session's undo and redo lists (only while the card is shown).</summary>
    public void Refresh()
    {
        if (!Card.IsVisible)
        {
            return;
        }
        Rows.Children.Clear();
        var s = _session();
        var undo = s?.UndoList ?? [];
        var redo = s?.RedoList ?? [];
        Count.Text = undo.Count > 0 ? Lang.T("history.count", undo.Count) : "";
        TakeBackClaude.IsVisible = s != null && ClaudesChanges(s).Count > 0;
        if (s == null || undo.Count + redo.Count == 0)
        {
            Rows.Children.Add(new TextBlock { Text = Lang.T("history.none"), FontSize = 12, Foreground = Ui.Muted });
        }
        else
        {
            // Undone changes on top (the next redo just above the current change).
            foreach (var c in redo)
            {
                Rows.Children.Add(Row(s, c, undone: true, current: false));
            }
            for (int i = undo.Count - 1; i >= 0; i--)
            {
                Rows.Children.Add(Row(s, undo[i], undone: false, current: i == undo.Count - 1));
            }
        }
        // Steps made in other areas: not undoable here, kept for where they were made.
        if (s is { Shelved: > 0 and var aside })
        {
            Rows.Children.Add(new TextBlock
            {
                Text = Lang.T("history.shelved", aside),
                FontSize = 11,
                Foreground = Ui.Muted,
                TextWrapping = TextWrapping.Wrap,
            });
        }
    }
}
