using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

// The Paste tool's panel, like the web editor's: what is pasted, the height, repeats, turn and mirror.
public sealed class PastePanel
{
	public Control Card { get; }
	private readonly PasteTool _paste;
	internal TextBlock Info { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
	internal CheckBox GroundBox { get; } = new() { Content = "Ground shape and paint", IsChecked = true, FontSize = 12 };
	internal CheckBox ObjectsBox { get; } = new() { Content = "Objects", IsChecked = true, FontSize = 12 };
	internal NumericUpDown OffsetBox { get; } = new() { Value = 0, Increment = 0.5m, FormatString = "0.0#", FontSize = 12 };
	internal NumericUpDown CopiesBox { get; } = new() { Value = 1, Minimum = 1, Maximum = 50, Increment = 1, FormatString = "0", FontSize = 12 };
	internal ComboBox AlongBox { get; } = new() { ItemsSource = new[] { "its width", "its depth", "upwards" }, SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
	internal NumericUpDown GapBox { get; } = new() { Value = 0, Increment = 0.25m, FormatString = "0.0#", FontSize = 12 };
	internal Button TurnButton { get; } = new() { Content = "Turn 90° (R)", FontSize = 12 };
	internal Button MirrorButton { get; } = new() { Content = "Mirror (F)", FontSize = 12 };
	internal Button DoneButton { get; } = new() { Content = "Done (Esc)", FontSize = 12 };
	public event Action? Done;

	public PastePanel(PasteTool paste)
	{
		_paste = paste;
		GroundBox.IsCheckedChanged += (_, _) => paste.Ground = GroundBox.IsChecked == true;
		ObjectsBox.IsCheckedChanged += (_, _) => paste.Objects = ObjectsBox.IsChecked == true;
		OffsetBox.ValueChanged += (_, e) => { paste.Offset = (float)(e.NewValue ?? 0); paste.Notify(); };
		CopiesBox.ValueChanged += (_, e) => { paste.Copies = (int)(e.NewValue ?? 1); paste.Notify(); };
		AlongBox.SelectionChanged += (_, _) => { paste.Direction = (PasteTool.Along)Math.Max(0, AlongBox.SelectedIndex); paste.Notify(); };
		GapBox.ValueChanged += (_, e) => { paste.Gap = (float)(e.NewValue ?? 0); paste.Notify(); };
		TurnButton.Click += (_, _) => paste.TurnBy(90);
		MirrorButton.Click += (_, _) => { paste.Mirror = !paste.Mirror; paste.Notify(); };
		DoneButton.Click += (_, _) => Done?.Invoke();
		ToolTip.SetTip(TurnButton, "Quarter turn; , . turn by 1° (Shift: 15°)");
		paste.Changed += Refresh;
		Control Row(string label, Control input, string? unit = null)
		{
			var g = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*,Auto") };
			g.Children.Add(new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
			Grid.SetColumn(input, 1);
			g.Children.Add(input);
			if (unit != null)
			{
				var u = new TextBlock { Text = unit, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
				Grid.SetColumn(u, 2);
				g.Children.Add(u);
			}
			return g;
		}
		Card = new Border
		{
			Background = Ui.Panel,
			BorderBrush = Ui.Line,
			BoxShadow = BoxShadows.Parse("0 6 24 0 #59000000"),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(10),
			Padding = Ui.Pad,
			VerticalAlignment = VerticalAlignment.Top,
			Child = new StackPanel
			{
				Width = 290,
				Spacing = 6,
				Children =
				{
					new TextBlock { Text = "Paste", FontSize = 14, FontWeight = FontWeight.SemiBold },
					Info, GroundBox, ObjectsBox,
					Row("Height", OffsetBox, " m"),
					new TextBlock { Text = "REPEAT (STACK)", FontSize = 10, Foreground = Ui.Muted, Margin = new Thickness(0, 4, 0, 0) },
					Row("Copies", CopiesBox),
					Row("Along", AlongBox),
					Row("Gap", GapBox, " m"),
					new WrapPanel { ItemSpacing = 4, LineSpacing = 4, Children = { TurnButton, MirrorButton, DoneButton } },
					new TextBlock
					{
						Text = "Click to place; the copied ground keeps its shape relative to the point you click. Height moves the paste up or down.",
						FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
					},
				},
			},
		};
		Refresh();
	}

	public void Refresh()
	{
		var c = _paste.Clip;
		Info.Text = c == null ? "Nothing copied yet: copy in the Area tool (Ctrl+C) or the Select tool (Ctrl+C)."
			: $"{(c.Name != null ? c.Name + ": " : "")}{c.W} × {c.H} m, {c.Objects.Count} object(s){(_paste.Count > 1 ? $", {_paste.Count} copies" : "")} · turned {_paste.Turn:0.#}°{(_paste.Mirror ? ", mirrored" : "")}.";
	}
}
