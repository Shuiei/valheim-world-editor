using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

// The Dungeon tool's panel: which dungeon of the area, going inside and cutting its roofs away, the
// rooms it can take, and the changes (turn, delete, close the open ends).
public sealed class DungeonPanel
{
	public Control Card { get; }
	private readonly GlView _view;
	private DungeonTool Tool => _view.Dungeon;
	internal ComboBox DungeonBox { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 12 };
	internal Button GoInside { get; } = new() { Content = "Go inside", FontSize = 12 };
	internal Button BuildHere { get; } = new() { Content = "Build here", FontSize = 12 };
	public event Action? BuildAsked;
	internal Slider CutSlider { get; } = new() { Minimum = 0, Maximum = 30, Value = 0, SmallChange = 1, TickFrequency = 1, IsSnapToTickEnabled = true };
	private readonly TextBlock _cutText = new() { FontSize = 12 };
	internal TextBox Filter { get; } = new() { PlaceholderText = "Find a room…", FontSize = 12 };
	internal ListBox Rooms { get; } = new() { MaxHeight = 260, FontSize = 12 };
	internal Button TurnButton { get; } = new() { Content = "Turn (R)", FontSize = 12 };
	internal Button CloseEnds { get; } = new() { Content = "Close open ends", FontSize = 12 };
	internal Button DeleteButton { get; } = new() { Content = "Delete room (Del)", FontSize = 12 };
	internal TextBlock Info { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
	internal CheckBox Contents { get; } = new() { Content = "With what the game puts in them", IsChecked = true, FontSize = 12 };
	internal CheckBox Doors { get; } = new() { Content = "Doors where the game might put them", IsChecked = true, FontSize = 12 };
	private readonly TextBlock _empty = new()
	{
		FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
		Text = "No dungeon in this area. Open the area over a Frost Cave, crypt or other dungeon entrance: the game keeps its rooms 5000 m above it."
	};
	private readonly StackPanel _body;
	private List<DungeonRooms.Dungeon> _shown = new();
	private bool _filling;

	public DungeonPanel(GlView view)
	{
		_view = view;
		DungeonBox.Tip("dungeon.which");
		GoInside.Tip("dungeon.inside");
		BuildHere.Tip("dungeon.build");
		BuildHere.Click += (_, _) => BuildAsked?.Invoke();
		CutSlider.Tip("dungeon.cut");
		Filter.Tip("dungeon.find");
		Rooms.Tip("dungeon.rooms");
		TurnButton.Tip("dungeon.turn");
		CloseEnds.Tip("dungeon.close");
		DeleteButton.Tip("dungeon.delete");
		Contents.Tip("dungeon.contents");
		Contents.IsCheckedChanged += (_, _) => Tool.WithContents = Contents.IsChecked == true;
		Doors.Tip("dungeon.doors");
		Doors.IsCheckedChanged += (_, _) => Tool.WithDoors = Doors.IsChecked == true;
		DungeonBox.SelectionChanged += (_, _) =>
		{
			if (!_filling && DungeonBox.SelectedIndex >= 0 && DungeonBox.SelectedIndex < _shown.Count)
			{
				Tool.Open(_shown[DungeonBox.SelectedIndex]);
				FillRooms();
			}
		};
		GoInside.Click += (_, _) => Inside();
		CutSlider.ValueChanged += (_, e) => SetCut(e.NewValue);
		Filter.TextChanged += (_, _) => FillRooms();
		Rooms.SelectionChanged += (_, _) =>
		{
			if (!_filling)
			{
				Tool.Choose((Rooms.SelectedItem as ListBoxItem)?.Tag as string);
			}
		};
		TurnButton.Click += (_, _) => Tool.Turn();
		CloseEnds.Click += (_, _) => Tool.CloseOpenEnds();
		DeleteButton.Click += (_, _) => Tool.Delete();
		Tool.Changed += Refresh;
		_body = new StackPanel
		{
			Spacing = 6,
			Children =
			{
				DungeonBox,
				new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { GoInside, BuildHere } },
				new StackPanel { Spacing = 2, Children = { _cutText, CutSlider } },
				new TextBlock { Text = "ROOMS", FontSize = 11, Foreground = Ui.Muted, FontWeight = FontWeight.SemiBold },
				Filter, Rooms, Contents, Doors,
				new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { TurnButton, DeleteButton } },
				CloseEnds, Info,
			},
		};
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
				Width = 280,
				Spacing = 6,
				Children =
				{
					new TextBlock { Text = "Dungeon", FontSize = 14, FontWeight = FontWeight.SemiBold },
					new TextBlock { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
						Text = "Pick a room below, then click a green opening to add it there. Click a room to select it, an orange or blue square (where rooms meet) to put a door there or take it away." },
					_empty, _body,
				},
			},
		};
		SetCut(0);
	}

	// The area's dungeons again (a new area, or an edit).
	public void Reload()
	{
		_filling = true;
		_shown = Tool.All.Where(d => d.Kind.Interior || d.Rooms.Count > 0).ToList();
		DungeonBox.ItemsSource = _shown.Select(DungeonTool.Describe).ToList();
		int i = Tool.Current is { } at ? _shown.FindIndex(d => System.Numerics.Vector3.DistanceSquared(d.Thing.Position, at) < 0.01f) : -1;
		if (i < 0 && _shown.Count > 0)
		{
			i = 0;
			Tool.Open(_shown[0]);
		}
		DungeonBox.SelectedIndex = i;
		_filling = false;
		_empty.IsVisible = _shown.Count == 0;
		_body.IsVisible = _shown.Count > 0;
		FillRooms();
		Refresh();
	}

	private void FillRooms()
	{
		_filling = true;
		string f = Filter.Text?.Trim() ?? "";
		var items = Tool.Palette.Where(r => f.Length == 0 || r.Name.Contains(f, StringComparison.OrdinalIgnoreCase))
			.Select(r => new ListBoxItem { Content = r.Name + (r.EndCap ? "  (end cap)" : r.Entrance ? "  (entrance)" : ""), Tag = r.Name }).ToList();
		Rooms.ItemsSource = items;
		Rooms.SelectedItem = items.FirstOrDefault(i => (string?)i.Tag == Tool.Chosen);
		_filling = false;
	}

	private void Refresh()
	{
		if (Tool.Dungeon is not { } d)
		{
			Info.Text = "";
			return;
		}
		int open = Tool.FreeEnds.Count;
		int overlaps = d.Rooms.Select((r, i) => Dungeons.Overlaps(d.Rooms, r, i).Count > 0 ? 1 : 0).Sum() / 2;
		string text = $"{d.Rooms.Count} rooms, {open} open end(s).";
		if (overlaps > 0)
		{
			text += $" {overlaps} overlapping pair(s).";
		}
		if (d.Problem != null)
		{
			text += " " + d.Problem;
		}
		if (Tool.Preview is { } p && Tool.Problem(p) is string problem)
		{
			text += " This one: " + problem;
		}
		Info.Text = text;
		DeleteButton.IsEnabled = Tool.Selected != null;
		CloseEnds.IsEnabled = open > 0;
	}

	// The camera to the dungeon (its rooms 5000 m above the entrance), looking down into it.
	internal void Inside()
	{
		if (Tool.Dungeon is not { } d)
		{
			return;
		}
		var p = d.Rooms.Count > 0 ? d.Rooms.Aggregate(System.Numerics.Vector3.Zero, (a, r) => a + r.Position) / d.Rooms.Count : d.Thing.Position;
		_view.Orbit(p.X, p.Z, 30, 55, 70, p.Y);
		if (CutSlider.Value == 0)
		{
			CutSlider.Value = 4;
		}
		SetCut(CutSlider.Value);
	}

	private void SetCut(double metres)
	{
		// From about the floor of the dungeon's entrance (3 m under the dungeon object, as in the game's caves).
		float? y = metres > 0 && Tool.Dungeon is { } d ? d.Thing.Position.Y - 3 + (float)metres : null;
		_view.CutY = y;
		_cutText.Text = metres > 0 ? $"Cut {metres:0} m above the floor" : "Cut: off (roofs shown)";
	}

	// Leaving the tool: the cut off.
	public void Leave() => _view.CutY = null;
}
