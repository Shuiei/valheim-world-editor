using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// The Area tool's panel, like the web editor's (editor/area.js): the selection's shape and soft edge,
// one action at a time with only its own settings, and Apply (Enter). Ground actions change the ground
// inside; object actions remove, select or replace the objects of the chosen kinds inside; Reset zones
// hands the zones under the selection back to the world generator when saving.
public sealed class AreaPanel
{
	public enum Act { Flatten, Raise, Lower, Smooth, Natural, Restore, Paint, Remove, Select, Replace, Reset }

	private static readonly (Act Act, string Label)[] Actions =
	{
		(Act.Flatten, "Flatten"), (Act.Raise, "Raise"), (Act.Lower, "Lower"), (Act.Smooth, "Smooth"), (Act.Natural, "Naturalize"),
		(Act.Restore, "Restore the ground"), (Act.Paint, "Paint"),
		(Act.Remove, "Remove objects"), (Act.Select, "Select objects"), (Act.Replace, "Replace objects"),
		(Act.Reset, "Reset zones"),
	};

	private static readonly (string Label, BrushTool Tool)[] Paints =
	{
		("Dirt", BrushTool.PaintDirt), ("Cultivated", BrushTool.PaintCultivated), ("Paved", BrushTool.PaintPaved), ("Clear paint", BrushTool.PaintClear),
	};

	public Control Card { get; }
	public AreaTool Area { get; }
	private readonly GlView _view;
	private readonly Func<EditSession?> _session;
	private readonly Func<int, string?> _nameOf;
	internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);
	public event Action<string>? Message;
	// Select objects: the window switches to the Select tool.
	public event Action? SwitchToSelect;

	internal Button BoxButton { get; } = new() { Content = "Box", FontSize = 12 };
	internal Button PolyButton { get; } = new() { Content = "Polygon", FontSize = 12 };
	internal Button ClearButton { get; } = new() { Content = "Clear (Esc)", FontSize = 12 };
	internal TextBlock Info { get; } = new() { FontSize = 12, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
	internal Slider SoftSlider { get; }
	internal ComboBox ActionBox { get; }
	internal NumericUpDown HeightBox { get; } = new() { Value = 35, Increment = 0.1m, FormatString = "0.0#", FontSize = 12 };
	internal Button AverageButton { get; } = new() { Content = "avg", FontSize = 11, Padding = new Thickness(6, 2) };
	internal NumericUpDown AmountBox { get; } = new() { Value = 2, Increment = 0.1m, FormatString = "0.0#", FontSize = 12 };
	internal ComboBox PaintBox { get; } = new() { ItemsSource = Paints.Select(p => p.Label).ToArray(), SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
	internal TextBlock VolumeText { get; } = new() { FontSize = 11, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap };
	internal Dictionary<ObjectKind, ToggleButton> KindButtons { get; } = new();
	internal ComboBox FromBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
	internal ComboBox ToBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 400 };
	internal CheckBox KeepBuildingsBox { get; } = new() { Content = "Keep my buildings", IsChecked = true, FontSize = 12 };
	internal CheckBox ResetGroundBox { get; } = new() { Content = "Reset ground edits too", IsChecked = true, FontSize = 12 };
	internal Button UnresetButton { get; } = new() { Content = "Cancel reset", FontSize = 12 };
	internal Button ApplyButton { get; } = new() { FontSize = 12 };
	private readonly Dictionary<Control, Act[]> _rows = new();
	private List<int> _creatable = new();

	public Act Current => Actions[Math.Max(0, ActionBox.SelectedIndex)].Act;

	public AreaPanel(GlView view, Func<EditSession?> session, Func<int, string?> nameOf)
	{
		_view = view;
		_session = session;
		_nameOf = nameOf;
		Area = view.Area;
		ActionBox = new ComboBox { ItemsSource = Actions.Select(a => a.Label).ToArray(), SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 500 };
		var softV = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
		SoftSlider = new Slider { Minimum = 0, Maximum = 20, SmallChange = 0.5, TickFrequency = 0.5, IsSnapToTickEnabled = true, Value = Area.Soft };
		softV.Text = $"{Area.Soft:0.#} m";
		SoftSlider.ValueChanged += (_, e) => { Area.Soft = (float)e.NewValue; softV.Text = $"{e.NewValue:0.#} m"; Refresh(); };
		BoxButton.Click += (_, _) => { Area.Box = true; Area.Clear(); ShowShape(); };
		PolyButton.Click += (_, _) => { Area.Box = false; Area.Clear(); ShowShape(); };
		ClearButton.Click += (_, _) => { Area.Clear(); Message?.Invoke("Selection cleared."); };
		ActionBox.SelectionChanged += (_, _) => ShowRows();
		HeightBox.ValueChanged += (_, _) => Refresh();
		AmountBox.ValueChanged += (_, _) => Refresh();
		AverageButton.Click += (_, _) =>
		{
			if (_session()?.Ground is { } g && Area.Average(g) is float avg)
			{
				HeightBox.Value = (decimal)MathF.Round(avg, 1);
			}
		};
		ToolTip.SetTip(AverageButton, "Average height inside the selection");
		var kinds = new WrapPanel { ItemSpacing = 4, LineSpacing = 4 };
		foreach (var k in ObjectKinds.All)
		{
			var b = new ToggleButton { Content = ObjectKinds.Label(k), IsChecked = Area.Kinds.Contains(k), FontSize = 11, Padding = new Thickness(6, 2) };
			b.IsCheckedChanged += (_, _) => { if (b.IsChecked == true) Area.Kinds.Add(k); else Area.Kinds.Remove(k); };
			KindButtons[k] = b;
			kinds.Children.Add(b);
		}
		ApplyButton.Click += async (_, _) => await Apply();
		UnresetButton.Click += (_, _) => CancelReset();
		Area.Changed += Refresh;

		Control Row(string label, Control input, Control? after = null)
		{
			var g = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*,Auto") };
			g.Children.Add(new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
			Grid.SetColumn(input, 1);
			g.Children.Add(input);
			if (after != null)
			{
				Grid.SetColumn(after, 2);
				g.Children.Add(after);
			}
			return g;
		}
		Control For(Control c, params Act[] acts)
		{
			_rows[c] = acts;
			return c;
		}
		TextBlock Help(string t) => new() { Text = t, FontSize = 11, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap };
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
				Width = 310,
				Spacing = 6,
				Children =
				{
					new TextBlock { Text = "Area", FontSize = 14, FontWeight = FontWeight.SemiBold },
					new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { BoxButton, PolyButton, ClearButton } },
					Info,
					Row("Soft edge", SoftSlider, softV),
					Row("Action", ActionBox),
					For(Row("Height", HeightBox, AverageButton), Act.Flatten),
					For(Row("Amount", AmountBox, new TextBlock { Text = " m", FontSize = 12, VerticalAlignment = VerticalAlignment.Center }), Act.Raise, Act.Lower),
					For(Row("Paint", PaintBox), Act.Paint),
					For(VolumeText, Act.Flatten, Act.Raise, Act.Lower),
					For(Help("Evens out bumps inside the selection."), Act.Smooth),
					For(Help("Natural-looking bumps, with the Bumps and Size of the Naturalize brush."), Act.Natural),
					For(Help("Back to the ground the game generated, paint removed."), Act.Restore),
					For(kinds, Act.Remove, Act.Select, Act.Replace),
					For(Row("Replace", FromBox), Act.Replace),
					For(Row("with", ToBox), Act.Replace),
					For(KeepBuildingsBox, Act.Reset),
					For(ResetGroundBox, Act.Reset),
					For(Help("On save, the zones under the selection lose their trees, rocks, ruins and dungeon entrances, and the game generates them again the next time someone goes there."), Act.Reset),
					For(UnresetButton, Act.Reset),
					ApplyButton,
				},
			},
		};
		ShowShape();
		ShowRows();
	}

	private static readonly IBrush On = new SolidColorBrush(Color.FromRgb(58, 92, 140));

	private void ShowShape()
	{
		BoxButton.Background = Area.Box ? On : null;
		PolyButton.Background = Area.Box ? null : On;
		Refresh();
	}

	private void ShowRows()
	{
		var act = Current;
		foreach (var (row, acts) in _rows)
		{
			row.IsVisible = acts.Contains(act);
		}
		ApplyButton.Content = act switch
		{
			Act.Remove => "Remove the objects (Enter)",
			Act.Select => "Select the objects (Enter)",
			Act.Replace => "Replace (Enter)",
			Act.Reset => "Reset zones… (Enter)",
			_ => $"{Actions.First(a => a.Act == act).Label} (Enter)",
		};
		Refresh();
	}

	private float Value(NumericUpDown b) => (float)(b.Value ?? 0);

	// The things inside the selection (indices), not deleted, of shown kinds (or added in this session).
	internal List<int> ThingsInside(List<Vector2> poly, bool chosenKindsOnly)
	{
		var out_ = new List<int>();
		if (_view.Scene is not { } s)
		{
			return out_;
		}
		float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
		lock (s.Things)
		{
			for (int i = 0; i < s.Things.Count; i++)
			{
				var t = s.Things[i];
				if (t.Gone || !AreaTool.Inside(poly, t.Position.X - ox, t.Position.Z - oz))
				{
					continue;
				}
				var k = KindOf(t);
				if ((t.Id < 0 || _view.IsShown(k)) && (!chosenKindsOnly || Area.Kinds.Contains(k)))
				{
					out_.Add(i);
				}
			}
		}
		return out_;
	}

	private ObjectKind KindOf(WorldScene.Thing t) => ObjectKinds.Of(_nameOf(t.Prefab), t.Piece);
	private string NameOf(int prefab) => _nameOf(prefab) ?? prefab.ToString();

	// The selection changed: its size, what is inside, the lists and the volumes.
	public void Refresh()
	{
		var poly = Area.Polygon();
		var g = _session()?.Ground;
		VolumeText.Text = g != null ? Area.Volume(g, Ground(Current) ?? AreaTool.GroundAction.Flatten, Value(HeightBox), Value(AmountBox)) : "";
		if (poly == null)
		{
			Info.Text = Area.Box ? "Drag on the ground to select a box." : "Click points around the area; double-click or Enter closes it. Backspace removes a point, Esc clears.";
			FromBox.ItemsSource = Array.Empty<string>();
			foreach (var (k, b) in KindButtons)
			{
				b.Content = ObjectKinds.Label(k);
			}
			_view.LassoChanged();
			return;
		}
		var inside = ThingsInside(poly, chosenKindsOnly: false);
		var s = _view.Scene!;
		Info.Text = $"{AreaTool.Area(poly):0} m² selected · {inside.Count} shown object(s) inside. Click outside to start a new selection.";
		var byKind = inside.GroupBy(i => KindOf(s.Things[i])).ToDictionary(x => x.Key, x => x.Count());
		foreach (var (k, b) in KindButtons)
		{
			b.Content = $"{ObjectKinds.Label(k)} {(_view.IsShown(k) ? byKind.GetValueOrDefault(k).ToString() : "hidden")}";
		}
		var keep = FromBox.SelectedItem as string;
		var names = inside.GroupBy(i => NameOf(s.Things[i].Prefab)).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} ({x.Count()})").ToList();
		FromBox.ItemsSource = names;
		FromBox.SelectedIndex = keep != null && names.Contains(keep) ? names.IndexOf(keep) : names.Count > 0 ? 0 : -1;
		if (_creatable.Count == 0 && s.World != null)
		{
			_creatable = s.World.Creatable.Where(p => _nameOf(p) != null).OrderBy(p => _nameOf(p), StringComparer.OrdinalIgnoreCase).ToList();
			ToBox.ItemsSource = _creatable.Select(p => _nameOf(p)!).ToList();
		}
		_view.LassoChanged();
	}

	private static AreaTool.GroundAction? Ground(Act a) => a switch
	{
		Act.Flatten => AreaTool.GroundAction.Flatten,
		Act.Raise => AreaTool.GroundAction.Raise,
		Act.Lower => AreaTool.GroundAction.Lower,
		Act.Smooth => AreaTool.GroundAction.Smooth,
		Act.Natural => AreaTool.GroundAction.Natural,
		Act.Restore => AreaTool.GroundAction.Restore,
		Act.Paint => AreaTool.GroundAction.Paint,
		_ => null,
	};

	// The action (Apply, Enter).
	public async Task Apply()
	{
		if (_session() is not { } session)
		{
			return;
		}
		var poly = Area.Polygon();
		if (poly == null)
		{
			Message?.Invoke("Select an area first.");
			return;
		}
		var act = Current;
		if (Ground(act) is { } ga)
		{
			var paint = Brush.PaintOf(Paints[Math.Max(0, PaintBox.SelectedIndex)].Tool)!;
			string name = act == Act.Paint ? $"Paint {Paints[Math.Max(0, PaintBox.SelectedIndex)].Label.ToLowerInvariant()}" : AreaTool.Label(ga);
			var touched = session.EditGround($"Area: {name}", g => Area.Apply(g, session.Brush, ga, Value(HeightBox), Value(AmountBox), paint));
			Message?.Invoke($"{name} applied to {touched.Count} point(s).");
			Refresh();
			return;
		}
		switch (act)
		{
			case Act.Remove:
			{
				var list = ThingsInside(poly, chosenKindsOnly: true);
				if (list.Count == 0)
				{
					Message?.Invoke("No objects of the chosen kinds inside the selection.");
					return;
				}
				session.Commit($"Area: removed {list.Count} object(s)", null, list, Array.Empty<(NewObject, bool)>());
				Message?.Invoke($"Removed {list.Count} object(s). Ctrl+Z brings them back.");
				break;
			}
			case Act.Select:
				_view.Select(ThingsInside(poly, chosenKindsOnly: true));
				SwitchToSelect?.Invoke();
				break;
			case Act.Replace:
			{
				var s = _view.Scene!;
				string? from = (FromBox.SelectedItem as string)?.Split(" (")[0];
				int to = ToBox.SelectedIndex >= 0 && ToBox.SelectedIndex < _creatable.Count ? _creatable[ToBox.SelectedIndex] : 0;
				if (from == null || to == 0)
				{
					Message?.Invoke("Choose what to replace, and with what.");
					return;
				}
				var list = ThingsInside(poly, chosenKindsOnly: false).Where(i => NameOf(s.Things[i].Prefab) == from).ToList();
				Replace(session, list, to);
				break;
			}
			case Act.Reset:
				await ResetZones(session);
				break;
		}
		Refresh();
	}

	// Replaces things with objects of another kind, where they are and turned as they are.
	public void Replace(EditSession session, IReadOnlyList<int> things, int prefab)
	{
		var s = session.Scene;
		bool piece = PieceCatalog.Get(prefab)?.Tool != null;
		var adds = things.Select(i => s.Things[i]).Select(t => (new NewObject(0, prefab, t.Position, t.Rotation, 0), piece)).ToList();
		session.Commit($"Replaced {things.Count} with {NameOf(prefab)}", null, things, adds);
		Message?.Invoke($"Replaced {things.Count} object(s) with {NameOf(prefab)}.");
	}

	private async Task ResetZones(EditSession session)
	{
		var zones = Area.ZonesUnder(session.Ground);
		if (zones.Count == 0)
		{
			Message?.Invoke("Select an area first.");
			return;
		}
		bool keep = KeepBuildingsBox.IsChecked == true, ground = ResetGroundBox.IsChecked == true;
		if (!await Confirm($"Reset {zones.Count} zone(s) ({string.Join(" · ", zones.Select(z => $"{z.X}, {z.Z}"))}) when you save?\n\n"
			+ $"• Every tree, rock, ruin and dungeon entrance in them is removed{(keep ? " (your buildings stay)" : ", your buildings too")}.\n"
			+ (ground ? "• Their ground edits are undone.\n" : "") + "• Valheim generates the zones again the next time a player goes there."))
		{
			return;
		}
		var marked = session.Resets.Select(r => (r.X, r.Z)).ToHashSet();
		var resets = zones.Select(z => (new ZoneReset(z.X, z.Z, keep, ground), marked.Contains(z), true)).ToArray();
		Func<Ground, (List<int>, (int, int, int, int))>? clearGround = !ground ? null : g =>
		{
			var touched = new List<int>();
			foreach (var (zx, zz) in zones)
			{
				int gx0 = (zx - g.X0) * 64, gz0 = (zz - g.Z0) * 64;
				for (int k = 0; k <= 64; k++)
				{
					for (int l = 0; l <= 64; l++)
					{
						int gx = gx0 + l, gz = gz0 + k, p = gz * g.W + gx;
						if (gx < g.W && gz < g.H && (g.Mod[p] != 0 || g.PMod[p] != 0))
						{
							g.Mod[p] = g.PMod[p] = 0;
							g.Level[p] = g.Smooth[p] = 0;
							touched.Add(p);
						}
					}
				}
			}
			return (touched, (0, 0, g.W - 1, g.H - 1));
		};
		session.Commit($"Reset {zones.Count} zone(s)", clearGround, Array.Empty<int>(), Array.Empty<(NewObject, bool)>(), resets);
		Message?.Invoke($"{zones.Count} zone(s) marked for reset (red outline). Save to apply; Ctrl+Z cancels.");
	}

	private void CancelReset()
	{
		if (_session() is not { } session)
		{
			return;
		}
		var marked = session.Resets.ToDictionary(r => (r.X, r.Z));
		var zones = Area.ZonesUnder(session.Ground).Where(marked.ContainsKey).ToList();
		if (zones.Count == 0)
		{
			Message?.Invoke("No zone marked for reset under the selection.");
			return;
		}
		session.Commit($"Cancelled reset of {zones.Count} zone(s)", null, Array.Empty<int>(), Array.Empty<(NewObject, bool)>(),
			zones.Select(z => (marked[z], true, false)).ToArray());
		Message?.Invoke($"Reset cancelled for {zones.Count} zone(s).");
	}
}
