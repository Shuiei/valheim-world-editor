using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The history of this session, like the web editor's: every change, the newest first (undone ones
/// greyed), with Back to here (undo every change after it), Redo to here, and Remove (take out only that
/// change and keep everything done after it). Claude's changes (labelled "Claude: …") carry a tag, and
/// Take back Claude's changes removes them all, keeping the user's.
/// </summary>
public sealed partial class HistoryPanel
{
    /// <summary>The floating card, hidden until shown (<c>Toggle</c>); the window places it over the view.</summary>
    public Control Card { get; }
    /// <summary>One row per change, rebuilt by <c>Refresh</c>.</summary>
    internal StackPanel Rows { get; } = new() { Spacing = 4 };
    /// <summary>How many changes are on the undo list (undone ones not counted); empty when none.</summary>
    internal TextBlock Count { get; } = new() { FontSize = 11, Foreground = Ui.Muted };
    /// <summary>Take back Claude's changes; shown only while some of Claude's changes are in.</summary>
    internal Button TakeBackClaude { get; } = new Button { Content = Lang.T("history.label.takeBackClaude"), FontSize = 11, Padding = new Thickness(6, 1), IsVisible = false }.Tip("history.claude");
    /// <summary>The current edit session, null when no world is open.</summary>
    private readonly Func<EditSession?> _session;
    /// <summary>A status line to show (what an action did), in both languages.</summary>
    public event Action<Said>? Message;
    /// <summary>Raised when the card is closed with its close button.</summary>
    public event Action? Closed;
    /// <summary>Whether these steps may be changed (the window asks about steps from an earlier session).</summary>
    internal Func<IReadOnlyList<EditSession.Change>, Task<bool>> Allow { get; set; } = _ => Task.FromResult(true);

    /// <summary>The card, empty until shown.</summary>
    /// <param name="session">Gives the current edit session (null when no world is open).</param>
    public HistoryPanel(Func<EditSession?> session)
    {
        _session = session;
        var close = new Button { Content = Icons.Make("close", 14), Padding = new Thickness(5), HorizontalAlignment = HorizontalAlignment.Right }.Classed("ghost").Tip("card.close");
        close.Click += (_, _) => { Card!.IsVisible = false; Closed?.Invoke(); };
        TakeBackClaude.Click += async (_, _) => await TakeBackClaudes();
        Card = Ui.AsCard(new Border
        {
            // Just above the save bar.
            Margin = new Thickness(10, 10, 10, 70),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsVisible = false,
            Child = new StackPanel
            {
                Width = 380,
                Spacing = 6,
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                        Children = { Ui.Title(Lang.T("history.title"), HelpTopics.History), Col(Count, 1, new Thickness(8, 3, 0, 0)), Col(close, 2) },
                    },
                    TakeBackClaude,
                    new ScrollViewer { MaxHeight = 520, Content = Rows },
                },
            },
        });
    }

    /// <summary>Puts a control in a grid column, with an optional margin.</summary>
    private static Control Col(Control c, int col, Thickness? margin = null)
    {
        Grid.SetColumn(c, col);
        if (margin is { } m)
        {
            c.Margin = m;
        }
        return c;
    }

    /// <summary>A change Claude made (ClaudeTools labels them so).</summary>
    internal static bool ByClaude(EditSession.Change c) => c.Label.StartsWith("Claude:", StringComparison.Ordinal);

    /// <summary>Claude's changes still in, newest first.</summary>
    private static List<EditSession.Change> ClaudesChanges(EditSession s) =>
        [.. s.UndoList.Where(c => ByClaude(c) && !c.Removed && c.RevertOf == null).Reverse()];

    /// <summary>Puts a control in a grid row, across both columns.</summary>
    private static Control RowOf(Control c, int row)
    {
        Grid.SetRow(c, row);
        Grid.SetColumnSpan(c, 2);
        return c;
    }
}
