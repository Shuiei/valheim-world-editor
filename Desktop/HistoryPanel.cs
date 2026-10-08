using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

// The history of this session, like the web editor's: every change, the newest first (undone ones
// greyed), with Back to here (undo every change after it), Redo to here, and Remove (take out only that
// change and keep everything done after it).
public sealed class HistoryPanel
{
	public Control Card { get; }
	internal StackPanel Rows { get; } = new() { Spacing = 4 };
	internal TextBlock Count { get; } = new() { FontSize = 11, Foreground = Ui.Muted };
	private readonly Func<EditSession?> _session;
	public event Action<string>? Message;
	public event Action? Closed;

	public HistoryPanel(Func<EditSession?> session)
	{
		_session = session;
		var close = new Button { Content = Icons.Make("close", 14), Padding = new Thickness(5), HorizontalAlignment = HorizontalAlignment.Right }.Classed("ghost");
		close.Click += (_, _) => { Card!.IsVisible = false; Closed?.Invoke(); };
		Card = new Border
		{
			Background = Ui.Panel,
			BorderBrush = Ui.Line,
			BoxShadow = BoxShadows.Parse("0 6 24 0 #59000000"),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(10),
			Padding = Ui.Pad,
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
						Children = { new TextBlock { Text = "History", FontSize = 14, FontWeight = FontWeight.SemiBold }, Col(Count, 1, new Thickness(8, 3, 0, 0)), Col(close, 2) },
					},
					new ScrollViewer { MaxHeight = 520, Content = Rows },
				},
			},
		};
	}

	private static Control Col(Control c, int col, Thickness? margin = null)
	{
		Grid.SetColumn(c, col);
		if (margin is { } m)
		{
			c.Margin = m;
		}
		return c;
	}

	public void Toggle()
	{
		Card.IsVisible = !Card.IsVisible;
		Refresh();
	}

	public void Refresh()
	{
		if (!Card.IsVisible)
		{
			return;
		}
		Rows.Children.Clear();
		var s = _session();
		var undo = s?.UndoList ?? Array.Empty<EditSession.Change>();
		var redo = s?.RedoList ?? Array.Empty<EditSession.Change>();
		Count.Text = undo.Count > 0 ? $"{undo.Count} change(s)" : "";
		if (s == null || undo.Count + redo.Count == 0)
		{
			Rows.Children.Add(new TextBlock { Text = "No changes yet in this session.", FontSize = 12, Foreground = Ui.Muted });
			return;
		}
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

	private Control Row(EditSession s, EditSession.Change c, bool undone, bool current)
	{
		var acts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right };
		Button Act(string text, string tip, Action a)
		{
			var b = new Button { Content = text, FontSize = 11, Padding = new Thickness(6, 1) };
			ToolTip.SetTip(b, tip);
			b.Click += (_, _) => { a(); Refresh(); };
			acts.Children.Add(b);
			return b;
		}
		if (undone)
		{
			Act("Redo to here", "Redo up to this change", () => s.ForwardTo(c));
		}
		else
		{
			if (!current)
			{
				Act("Back to here", "Undo every change after this one", () => s.BackTo(c));
			}
			if (!c.Removed && c.RevertOf == null)
			{
				Act("Remove", "Take out only this change", () => Message?.Invoke(s.RemoveChange(c)));
			}
		}
		string meta = c.Time.ToString("T") + (c.Describe() is { Length: > 0 } d ? " · " + d : "");
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
				CornerRadius = new CornerRadius(999), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.Parse("#2a6a4a")), Padding = new Thickness(5, 0), VerticalAlignment = VerticalAlignment.Center,
				Child = new TextBlock { Text = "applied", FontSize = 10, Foreground = Ui.Live },
			});
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

	private static Control RowOf(Control c, int row)
	{
		Grid.SetRow(c, row);
		Grid.SetColumnSpan(c, 2);
		return c;
	}
}
