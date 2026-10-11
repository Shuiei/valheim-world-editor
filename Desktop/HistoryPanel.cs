using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

// The history of this session, like the web editor's: every change, the newest first (undone ones
// greyed), with Back to here (undo every change after it), Redo to here, and Remove (take out only that
// change and keep everything done after it). Claude's changes (labelled "Claude: …") carry a tag, and
// Take back Claude's changes removes them all, keeping the user's.
public sealed class HistoryPanel
{
	public Control Card { get; }
	internal StackPanel Rows { get; } = new() { Spacing = 4 };
	internal TextBlock Count { get; } = new() { FontSize = 11, Foreground = Ui.Muted };
	internal Button TakeBackClaude { get; } = new Button { Content = "Take back Claude's changes", FontSize = 11, Padding = new Thickness(6, 1), IsVisible = false }.Tip("history.claude");
	private readonly Func<EditSession?> _session;
	public event Action<string>? Message;
	public event Action? Closed;
	// Whether these steps may be changed (the window asks about steps from an earlier session).
	internal Func<IReadOnlyList<EditSession.Change>, Task<bool>> Allow { get; set; } = _ => Task.FromResult(true);

	public HistoryPanel(Func<EditSession?> session)
	{
		_session = session;
		var close = new Button { Content = Icons.Make("close", 14), Padding = new Thickness(5), HorizontalAlignment = HorizontalAlignment.Right }.Classed("ghost").Tip("card.close");
		close.Click += (_, _) => { Card!.IsVisible = false; Closed?.Invoke(); };
		TakeBackClaude.Click += async (_, _) => await TakeBackClaudes();
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
					TakeBackClaude,
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

	// A change Claude made (ClaudeTools labels them so).
	internal static bool ByClaude(EditSession.Change c) => c.Label.StartsWith("Claude:", StringComparison.Ordinal);

	// Claude's changes still in, newest first.
	private static List<EditSession.Change> ClaudesChanges(EditSession s) =>
		s.UndoList.Where(c => ByClaude(c) && !c.Removed && c.RevertOf == null).Reverse().ToList();

	// Every change of Claude's taken out (newest first), the user's kept.
	internal async Task TakeBackClaudes()
	{
		if (_session() is not { } s || ClaudesChanges(s) is not { Count: > 0 } steps || !await Allow(steps))
		{
			return;
		}
		foreach (var c in steps)
		{
			s.RemoveChange(c);
		}
		Message?.Invoke($"Took back {steps.Count} change(s) Claude made; yours are kept.");
		Refresh();
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
		TakeBackClaude.IsVisible = s != null && ClaudesChanges(s).Count > 0;
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
			Act("Redo to here", "Redo up to this change", () => s.RedoList.SkipWhile(r => r != c).ToList(), () => s.ForwardTo(c));
		}
		else
		{
			if (!current)
			{
				Act("Back to here", "Undo every change after this one", () => s.UndoList.SkipWhile(u => u != c).Skip(1).ToList(), () => s.BackTo(c));
			}
			if (!c.Removed && c.RevertOf == null)
			{
				Act("Remove", "Take out only this change", () => new[] { c }, () => Message?.Invoke(s.RemoveChange(c)));
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
				CornerRadius = new CornerRadius(999), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.Parse("#2a6a4a")), Padding = new Thickness(5, 0), VerticalAlignment = VerticalAlignment.Center,
				Child = new TextBlock { Text = "applied", FontSize = 10, Foreground = Ui.Live },
			});
		}
		if (ByClaude(c))
		{
			label.Children.Add(new Border
			{
				CornerRadius = new CornerRadius(999), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.Parse("#7a5a2a")), Padding = new Thickness(5, 0), VerticalAlignment = VerticalAlignment.Center,
				Child = new TextBlock { Text = "Claude", FontSize = 10, Foreground = Ui.Accent },
			}.Tip("history.byClaude"));
		}
		if (c.Earlier)
		{
			// Kept from an earlier session of the editor (HistoryFile).
			label.Children.Add(new Border
			{
				CornerRadius = new CornerRadius(999), BorderThickness = new Thickness(1), BorderBrush = Ui.Line, Padding = new Thickness(5, 0), VerticalAlignment = VerticalAlignment.Center,
				Child = new TextBlock { Text = "earlier session", FontSize = 10, Foreground = Ui.Muted },
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

	private static Control RowOf(Control c, int row)
	{
		Grid.SetRow(c, row);
		Grid.SetColumnSpan(c, 2);
		return c;
	}
}
