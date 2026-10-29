using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class HistoryPanel
{
    /// <summary>How many rows the card shows at first, and how many more each Show older adds.</summary>
    internal const int Page = 200;

    /// <summary>How many rows the card shows (the newest; Show older adds a page, Toggle goes back to one).</summary>
    private int _shown = Page;

    /// <summary>The Show older button, with how many changes are not shown.</summary>
    private Button Older(int hidden)
    {
        var b = new Button { Content = Lang.T("history.older", hidden), FontSize = 11, Padding = new Thickness(6, 1) }.Tip("history.older");
        b.Click += (_, _) =>
        {
            _shown += Page;
            Refresh();
        };
        return b;
    }

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
            // Steps made in other areas this session (said below): none here, not none at all.
            string none = s is { Shelved: > 0 } ? Lang.T("history.noneHere") : Lang.T("history.none");
            Rows.Children.Add(new TextBlock { Text = none, FontSize = 12, Foreground = Ui.Muted });
        }
        else
        {
            // Undone changes on top (the next redo just above the current change), then the newest _shown in all.
            var rows = redo.Select(c => (Change: c, Undone: true)).Concat(Enumerable.Reverse(undo).Select(c => (Change: c, Undone: false))).ToList();
            // The current change is the first after the undone ones.
            foreach (var ((c, undone), i) in rows.Take(_shown).Select((r, i) => (r, i)))
            {
                Rows.Children.Add(Row(s, c, undone, current: i == redo.Count));
            }
            if (rows.Count > _shown)
            {
                Rows.Children.Add(Older(rows.Count - _shown));
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
