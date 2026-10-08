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
	private readonly TextBlock _info = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
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
	// Eyedropper: the "Replace with" kind picked from the world.
	internal Button ReplacePickButton { get; } = new() { Content = "pick", FontSize = 11, Padding = new Thickness(6, 2) };
	public event Action<string, Action<int>>? PickKindAsked;
	public event Action<string>? Message;
	internal Button ByButton { get; } = new() { Content = "Move by", FontSize = 12 };

	// Saved selections (per world): keep the selection under a name, select it again (or add it), forget it.
	internal ComboBox SavedBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 300 };
	internal Button KeepButton { get; } = new() { Content = "Keep…", FontSize = 12 };
	internal Button LoadButton { get; } = new() { Content = "Select", FontSize = 12 };
	internal Button AddSavedButton { get; } = new() { Content = "Add", FontSize = 12 };
	internal Button ForgetButton { get; } = new() { Content = "Forget", FontSize = 12 };
	private readonly StackPanel _savedRow;
	private List<SavedSelections.Saved> _saved = new();
	// The window's: the open world's name, its things, the selection, selecting, and the questions.
	internal Func<string?> WorldName { get; set; } = () => null;
	internal Func<IReadOnlyList<WorldScene.Thing>?> Things { get; set; } = () => null;
	internal Func<IReadOnlyCollection<int>> SelectedIds { get; set; } = Array.Empty<int>;
	internal Action<IEnumerable<int>> SelectIds { get; set; } = _ => { };
	internal Func<string, Task<string?>> AskName { get; set; } = n => Task.FromResult<string?>(n);
	internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);

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
		DeleteButton = Act("Delete", Tips.Of("select.delete"));
		BuildingButton = Act("Whole building", Tips.Of("select.building"));
		SameButton = Act("Same kind", Tips.Of("select.same"));
		InvertButton = Act("Invert", Tips.Of("select.invert"));
		DeleteButton.Click += (_, _) => tool.Delete();
		ReplaceButton.Click += (_, _) =>
		{
			if (ReplaceBox.SelectedIndex >= 0 && ReplaceBox.SelectedIndex < ReplaceKinds.Count)
			{
				ReplaceAsked?.Invoke(ReplaceKinds[ReplaceBox.SelectedIndex]);
			}
		};
		ReplacePickButton.Tip("select.pick");
		ReplaceBox.Tip("select.to");
		ReplaceButton.Tip("select.replace");
		ApplyButton.Tip("select.apply");
		InspectButton.Tip("select.inspect");
		ClaimButton.Tip("select.claim");
		SavedBox.Tip("select.saved");
		ForgetButton.Tip("select.forget");
		XBox.Tip("select.x");
		YBox.Tip("select.y");
		ZBox.Tip("select.z");
		TurnBox.Tip("select.turn");
		ReplacePickButton.Click += (_, _) => PickKindAsked?.Invoke("Replace", UsePicked);
		BuildingButton.Click += (_, _) => tool.WholeBuilding();
		SameButton.Click += (_, _) => tool.SameKind();
		InvertButton.Click += (_, _) => tool.Invert();
		GroundBox = new CheckBox { Content = "Put each object on the ground when moving", IsChecked = tool.OnGround, FontSize = 12 }.Tip("select.ground");
		GroundBox.IsCheckedChanged += (_, _) => tool.OnGround = GroundBox.IsChecked == true;
		SnapBox = new CheckBox { Content = "Snap to other pieces when moving", IsChecked = tool.SnapToPieces, FontSize = 12 }.Tip("select.snap");
		SnapBox.IsCheckedChanged += (_, _) => tool.SnapToPieces = SnapBox.IsChecked == true;

		KeepButton.Tip("select.keep");
		LoadButton.Tip("select.load");
		AddSavedButton.Tip("select.add");
		KeepButton.Click += async (_, _) => await Keep();
		LoadButton.Click += (_, _) => UseSaved(false);
		AddSavedButton.Click += (_, _) => UseSaved(true);
		ForgetButton.Click += async (_, _) => await Forget();
		SavedBox.SelectionChanged += (_, _) => _savedRow!.IsVisible = SavedBox.SelectedIndex >= 0;
		_savedRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, IsVisible = false, Children = { LoadButton, AddSavedButton, ForgetButton } };

		Control Cell(string label, Control box)
		{
			var l = new TextBlock { Text = label, Width = 34, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
			Tips.Label(l, box);
			return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { l, box } };
		}
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
		ByButton.Tip("select.by");
		ByButton.Click += (_, _) => SetBy(!_by);
		ApplyButton.Click += (_, _) => Apply();
		_exact = new StackPanel
		{
			Spacing = 4,
			Children =
			{
				new TextBlock { Text = "EXACT PLACE", FontSize = 10, Foreground = Ui.Muted, Margin = new Thickness(0, 6, 0, 0) },
				grid,
				new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { ApplyButton, ByButton } },
				_info,
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
				Width = 290,
				Spacing = 6,
				Children =
				{
					new TextBlock { Text = "Select", FontSize = 14, FontWeight = FontWeight.SemiBold },
					new TextBlock { Text = "Click objects (Shift adds), or drag on the ground around them. Drag a selected object to move the selection.", FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap },
					new WrapPanel { Children = { DeleteButton, BuildingButton, SameButton, InvertButton, InspectButton, ClaimButton }, ItemSpacing = 4, LineSpacing = 4 },
					new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 6, Children = { new TextBlock { Text = "Replace with", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, Col(ReplaceBox, 1), Col(ReplacePickButton, 2) } },
					ReplaceButton,
					GroundBox,
					SnapBox,
					new TextBlock { Text = "SAVED SELECTIONS", FontSize = 10, Foreground = Ui.Muted, Margin = new Thickness(0, 6, 0, 0) },
					new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6, Children = { SavedBox, Col(KeepButton, 1) } },
					_savedRow,
					_exact,
					new TextBlock
					{
						Text = ", . turn (Shift: 15°) · PgUp PgDn lift (Shift: 1 m) · End drops onto what is under · Del deletes · Esc cancels a move · Alt + drag draws a zone over objects",
						FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
					},
				},
			},
		};
		Refresh();
	}

	// The eyedropper's kind into "Replace with", when the game can make it.
	internal void UsePicked(int prefab)
	{
		int i = ReplaceKinds.IndexOf(prefab);
		string name = ReplaceBox.ItemsSource is IList<string> names && i >= 0 && i < names.Count ? names[i] : NameOf?.Invoke(prefab) ?? prefab.ToString();
		if (i < 0)
		{
			Message?.Invoke($"{name} cannot be placed: the game has no such kind to copy.");
			return;
		}
		ReplaceBox.SelectedIndex = i;
		Message?.Invoke($"Replace with {name}.");
	}

	// The open world's saved selections into the list (the chosen one kept when it is still there).
	internal void FillSaved()
	{
		string? world = WorldName();
		int keep = SavedBox.SelectedIndex;
		_saved = world == null ? new() : SavedSelections.For(world);
		SavedBox.ItemsSource = _saved.Select(s => $"{s.Name} ({s.Items.Count})").ToList();
		SavedBox.PlaceholderText = _saved.Count > 0 ? "Saved selections…" : "No saved selections";
		SavedBox.SelectedIndex = keep >= 0 && keep < _saved.Count ? keep : -1;
		_savedRow.IsVisible = SavedBox.SelectedIndex >= 0;
	}

	internal async Task Keep()
	{
		string? world = WorldName();
		var things = Things();
		var items = things == null ? new List<SavedSelections.Item>() : SelectedIds().Where(i => i >= 0 && i < things.Count && !things[i].Gone)
			.Select(i => new SavedSelections.Item(things[i].Prefab, things[i].Position.X, things[i].Position.Y, things[i].Position.Z)).ToList();
		if (world == null || items.Count == 0)
		{
			Message?.Invoke("Select something first.");
			return;
		}
		string? name = (await AskName($"Selection {SavedSelections.For(world).Count + 1}"))?.Trim();
		if (string.IsNullOrEmpty(name))
		{
			return;
		}
		if (!SavedSelections.Keep(world, name, items))
		{
			Message?.Invoke("Could not keep the selection: the editor's data folder cannot be written.");
			return;
		}
		FillSaved();
		SavedBox.SelectedIndex = _saved.FindIndex(s => s.Name == name);
		Message?.Invoke($"Kept the selection “{name}” ({items.Count} object(s)). It can be picked again here, also after saving the world.");
	}

	internal void UseSaved(bool add)
	{
		int i = SavedBox.SelectedIndex;
		var things = Things();
		if (i < 0 || i >= _saved.Count || things == null)
		{
			return;
		}
		var s = _saved[i];
		var (ids, missing) = SavedSelections.Find(s, things);
		SelectIds(add ? SelectedIds().Concat(ids).Distinct().ToList() : ids);
		Message?.Invoke($"Selected {ids.Count} object(s) of “{s.Name}”{(missing > 0 ? $"; {missing} are not in this area or no longer where they were" : "")}.");
	}

	internal async Task Forget()
	{
		int i = SavedBox.SelectedIndex;
		string? world = WorldName();
		if (world == null || i < 0 || i >= _saved.Count || !await Confirm($"Forget the selection “{_saved[i].Name}”?"))
		{
			return;
		}
		if (!SavedSelections.Forget(world, _saved[i].Name))
		{
			Message?.Invoke("Could not forget the selection: the editor's data folder cannot be written.");
			return;
		}
		SavedBox.SelectedIndex = -1;
		FillSaved();
	}

	// The name of a kind (the window's models), for messages.
	public Func<int, string?>? NameOf { get; set; }

	private static Control Col(Control c, int col)
	{
		Grid.SetColumn(c, col);
		return c;
	}

	private void SetBy(bool by)
	{
		_by = by;
		ApplyButton.Content = by ? "Move by these" : "Move there";
		ByButton.Classes.Set("on", by);
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
