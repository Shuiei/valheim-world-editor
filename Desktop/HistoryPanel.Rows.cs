using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class HistoryPanel
{
    /// <summary>One change's row: its label and tags, its time and description, and the actions that fit it.</summary>
    /// <param name="s">The session the change belongs to.</param>
    /// <param name="c">The change.</param>
    /// <param name="undone">On the redo list (shown faded, with Redo to here).</param>
    /// <param name="current">The newest change still in (outlined, no Back to here).</param>
    private Border Row(EditSession s, EditSession.Change c, bool undone, bool current)
    {
        var acts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right };
        // The steps an action changes, asked about first.
        Button Act(string text, string tip, Func<IReadOnlyList<EditSession.Change>> steps, Action a)
        {
            var b = new Button { Content = text, FontSize = 11, Padding = new Thickness(6, 1) };
            ToolTip.SetTip(b, tip);
            b.Click += async (_, _) =>
            {
                if (await Allow(steps()))
                {
                    a();
                }
                Refresh();
            };
            acts.Children.Add(b);
            return b;
        }
        if (undone)
        {
            Act(Lang.T("history.label.redoTo"), Lang.T("history.tip.redoTo"), () => [.. s.RedoList.SkipWhile(r => r != c)], () => s.ForwardTo(c));
        }
        else
        {
            if (!current)
            {
                Act(Lang.T("history.label.backTo"), Lang.T("history.tip.backTo"), () => [.. s.UndoList.SkipWhile(u => u != c).Skip(1)], () => s.BackTo(c));
            }
            if (!c.Removed && c.RevertOf == null)
            {
                Act(Lang.T("history.label.remove"), Lang.T("history.tip.remove"), () => [c], () => Message?.Invoke(s.RemoveChange(c)));
            }
        }
        // Steps from an earlier session of the editor show their day too.
        string meta = c.Time.ToString(c.Time.Date == DateTime.Today ? "T" : "g") + (c.Describe() is { Length: > 0 } d ? " · " + d : "");
        var label = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { new TextBlock { Text = c.Label, FontSize = 12.5, FontWeight = current ? FontWeight.SemiBold : FontWeight.Normal, TextDecorations = c.Removed ? TextDecorations.Strikethrough : null, Foreground = c.Removed ? Ui.Muted : Ui.Text } },
        };
        if (c.Applied)
        {
            // Live: sent to the game already (the web editor's tag).
            label.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(999),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.Parse("#2a6a4a")),
                Padding = new Thickness(5, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = Lang.T("history.tag.applied"), FontSize = 10, Foreground = Ui.Live },
            });
        }
        if (ByClaude(c))
        {
            label.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(999),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.Parse("#7a5a2a")),
                Padding = new Thickness(5, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = Lang.T("settings.claude"), FontSize = 10, Foreground = Ui.Accent },
            }.Tip("history.byClaude"));
        }
        if (c.Earlier)
        {
            // Kept from an earlier session of the editor (HistoryFile).
            label.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(999),
                BorderThickness = new Thickness(1),
                BorderBrush = Ui.Line,
                Padding = new Thickness(5, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = Lang.T("history.tag.earlier"), FontSize = 10, Foreground = Ui.Muted },
            }.Tip("history.earlier"));
        }
        // The current change outlined in amber, like the web editor's.
        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = current ? Ui.Accent : Brushes.Transparent,
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(6),
            Opacity = undone ? 0.5 : 1,
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                Children =
                {
                    label,
                    Col(acts, 1),
                    RowOf(new TextBlock { Text = meta, FontSize = 11, Foreground = Ui.Muted }, 1),
                },
            },
        };
    }
}
