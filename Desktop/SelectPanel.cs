using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

// The Select tool's options, next to the rail: what to do with the selection (delete, select more),
// how moves land, the exact place, and the keys.
public sealed class SelectPanel
{
	public Control Card { get; }
	private readonly SelectTool _tool;
	private readonly TextBlock _info = new() { FontSize = 12, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
	private readonly Control _exact;
	private bool _by;

	// For tests.
	internal Button DeleteButton { get; }
	internal Button BuildingButton { get; }
	internal Button SameButton { get; }
	internal Button InvertButton { get; }
	internal CheckBox GroundBox { get; }
	internal CheckBox SnapBox { get; }
	internal NumericUpDown XBox { get; } = Num(0.1m);
	internal NumericUpDown YBox { get; } = Num(0.1m);
	internal NumericUpDown ZBox { get; } = Num(0.1m);
	internal NumericUpDown TurnBox { get; } = Num(1m);
	internal Button ApplyButton { get; } = new() { Content = "Move there", FontSize = 12 };
	internal Button InspectButton { get; } = new() { Content = "Inspect data (I)", FontSize = 12, IsEnabled = false };
	internal Button ClaimButton { get; } = new() { Content = "Make player built", FontSize = 12, IsEnabled = false };
	// Replace with: the kinds this world can make (filled by the window), and the button.
	internal ComboBox ReplaceBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 400 };
	internal Button ReplaceButton { get; } = new() { Content = "Replace the selection", FontSize = 12 };
	public List<int> ReplaceKinds { get; set; } = new();
	public event Action<int>? ReplaceAsked;
	internal Button ByButton { get; } = new() { Content = "Move by", FontSize = 12 };

	private static NumericUpDown Num(decimal step) => new() { Increment = step, FormatString = "0.##", FontSize = 12, MinWidth = 90 };

	private static Button Act(string text, string tip)
	{
		var b = new Button { Content = text, FontSize = 12, Padding = new Thickness(8, 3) };
		ToolTip.SetTip(b, tip);
		return b;
	}

	public SelectPanel(SelectTool tool)
	{
		_tool = tool;
		DeleteButton = Act("Delete", "Remove the selected objects from the world (Del). Ctrl+Z brings them back.");
		BuildingButton = Act("Whole building", "Add every piece connected to the selected ones (double-click a piece does it too)");
		SameButton = Act("Same kind", "Select every shown object of the selected kinds");
		InvertButton = Act("Invert", "Select the shown objects that are not selected, and only those");
		DeleteButton.Click += (_, _) => tool.Delete();
		ReplaceButton.Click += (_, _) =>
		{
			if (ReplaceBox.SelectedIndex >= 0 && ReplaceBox.SelectedIndex < ReplaceKinds.Count)
			{
				ReplaceAsked?.Invoke(ReplaceKinds[ReplaceBox.SelectedIndex]);
			}
		};
		BuildingButton.Click += (_, _) => tool.WholeBuilding();
		SameButton.Click += (_, _) => tool.SameKind();
		InvertButton.Click += (_, _) => tool.Invert();
		GroundBox = new CheckBox { Content = "Put each object on the ground when moving", IsChecked = tool.OnGround, FontSize = 12 };
		GroundBox.IsCheckedChanged += (_, _) => tool.OnGround = GroundBox.IsChecked == true;
		SnapBox = new CheckBox { Content = "Snap to other pieces when moving", IsChecked = tool.SnapToPieces, FontSize = 12 };
		SnapBox.IsCheckedChanged += (_, _) => tool.SnapToPieces = SnapBox.IsChecked == true;

		Control Cell(string label, Control box) => new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { new TextBlock { Text = label, Width = 34, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, box } };
		var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 6, RowSpacing = 4 };
		void Put(Control c, int row, int col)
		{
			Grid.SetRow(c, row);
			Grid.SetColumn(c, col);
			grid.Children.Add(c);
		}
		Put(Cell("X", XBox), 0, 0);
		Put(Cell("Y", YBox), 0, 1);
		Put(Cell("Z", ZBox), 1, 0);
		Put(Cell("Turn", TurnBox), 1, 1);
		ToolTip.SetTip(ByButton, "Move and turn by the typed amounts instead");
		ByButton.Click += (_, _) => SetBy(!_by);
		ApplyButton.Click += (_, _) => Apply();
		_exact = new StackPanel
		{
			Spacing = 4,
			Children =
			{
				new TextBlock { Text = "EXACT PLACE", FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(0, 6, 0, 0) },
				grid,
				new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { ApplyButton, ByButton } },
				_info,
			},
		};
		Card = new Border
		{
			Background = new SolidColorBrush(Color.FromArgb(235, 24, 28, 34)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(46, 53, 63)),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(10),
			Padding = new Thickness(8),
			VerticalAlignment = VerticalAlignment.Top,
			Child = new StackPanel
			{
				Width = 290,
				Spacing = 6,
				Children =
				{
					new TextBlock { Text = "Select", FontSize = 14, FontWeight = FontWeight.SemiBold },
					new TextBlock { Text = "Click objects (Shift adds), or drag on the ground around them. Drag a selected object to move the selection.", FontSize = 12, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap },
					new WrapPanel { Children = { DeleteButton, BuildingButton, SameButton, InvertButton, InspectButton, ClaimButton }, ItemSpacing = 4, LineSpacing = 4 },
					new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 6, Children = { new TextBlock { Text = "Replace with", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, Col(ReplaceBox, 1) } },
					ReplaceButton,
					GroundBox,
					SnapBox,
					_exact,
					new TextBlock
					{
						Text = ", . turn (Shift: 15°) · PgUp PgDn lift (Shift: 1 m) · End drops onto what is under · Del deletes · Esc cancels a move · Alt + drag draws a zone over objects",
						FontSize = 11, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap,
					},
				},
			},
		};
		Refresh();
	}

	private static Control Col(Control c, int col)
	{
		Grid.SetColumn(c, col);
		return c;
	}

	private void SetBy(bool by)
	{
		_by = by;
		ApplyButton.Content = by ? "Move by these" : "Move there";
		ByButton.Background = by ? new SolidColorBrush(Color.FromRgb(58, 92, 140)) : null;
		if (by)
		{
			XBox.Value = YBox.Value = ZBox.Value = TurnBox.Value = 0;
			_info.Text = "Metres east (X), up (Y) and north (Z), and degrees to turn, from where the selection is now.";
		}
		else
		{
			Refresh();
		}
	}

	private void Apply()
	{
		float V(NumericUpDown b) => (float)(b.Value ?? 0);
		_tool.PlaceAt(V(XBox), V(YBox), V(ZBox), V(TurnBox), _by);
		SetBy(false);
	}

	// The selection changed or moved: the exact place shows where it is now.
	public void Refresh()
	{
		var w = _tool.Where();
		_exact.IsVisible = w != null;
		DeleteButton.IsEnabled = SameButton.IsEnabled = w != null;
		InspectButton.IsEnabled = w is { Count: 1 };
		ClaimButton.IsEnabled = w != null;
		if (w is not { } at || _by)
		{
			return;
		}
		XBox.Value = (decimal)MathF.Round(at.X, 2);
		YBox.Value = (decimal)MathF.Round(at.Y, 2);
		ZBox.Value = (decimal)MathF.Round(at.Z, 2);
		TurnBox.Value = (decimal)MathF.Round(at.Turn, 1);
		_info.Text = at.Count > 1
			? $"The middle of the {at.Count} selected objects; Y is the lowest one, Turn the first one's. Turning turns them all around the middle."
			: "World position (m) and turn (degrees, clockwise seen from above).";
	}
}
