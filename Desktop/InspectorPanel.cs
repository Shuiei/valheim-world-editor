using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// The object inspector, like the web editor's (editor/inspector.js; MCEdit's NBT editor): everything the
// selected object holds in the save, by name, and editing it: sign text, portal tag, chest contents,
// ward permissions, crop timers… Applying replaces the object by a copy with the new data, one undo step.
public sealed class InspectorPanel
{
	private static readonly Dictionary<string, string> Labels = new()
	{
		["text"] = "Text (sign)", ["tag"] = "Tag (portal)", ["creator"] = "Builder (player id)", ["creatorName"] = "Builder name", ["author"] = "Author",
		["health"] = "Health", ["plantTime"] = "Planted at (game time)", ["items"] = "Contents", ["permitted"] = "Permitted players", ["enabled"] = "Enabled (1 = on)",
		["item"] = "Item", ["variant"] = "Variant", ["quality"] = "Quality", ["ownerName"] = "Owner name", ["owner"] = "Owner (player id)", ["fuel"] = "Fuel", ["StartTime"] = "Started at",
		["level"] = "Level (stars)", ["tamed"] = "Tamed (1 = yes)", ["support"] = "Support", ["scale"] = "Scale", ["picked"] = "Picked (1 = yes)", ["picked_time"] = "Picked at",
		["spawntime"] = "Spawned at", ["lastTime"] = "Last updated (game time)", ["accTime"] = "Time accumulated", ["queued"] = "Queued items", ["state"] = "State",
	};

	private static readonly (string Section, string Name)[] Sections =
	{
		("strings", "text"), ("ints", "whole number"), ("floats", "number"), ("longs", "large number"), ("vec3", "3 numbers (x y z)"), ("quats", "4 numbers (x y z w)"),
	};

	public Control Card { get; }
	private readonly Func<EditSession?> _session;
	private readonly TextBlock _title = new() { FontSize = 14, FontWeight = FontWeight.SemiBold };
	private readonly TextBlock _where = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
	internal StackPanel ItemsBox { get; } = new() { Spacing = 3 };
	internal StackPanel Fields { get; } = new() { Spacing = 2 };
	internal ComboBox AddSection { get; } = new() { ItemsSource = Sections.Select(s => s.Name).ToList(), SelectedIndex = 0, FontSize = 11, Width = 100 };
	internal TextBox AddKey { get; } = new() { PlaceholderText = "name (text, tag…)", FontSize = 11 };
	internal TextBox AddValue { get; } = new() { PlaceholderText = "value", FontSize = 11 };
	internal Button AddButton { get; } = new() { Content = "Add", FontSize = 11 };
	internal Button ApplyButton { get; } = new() { Content = "Apply changes", FontSize = 12, IsEnabled = false };
	internal Button RevertButton { get; } = new() { Content = "Revert", FontSize = 12, IsEnabled = false };
	public event Action<string>? Message;
	// After applying: the copy that replaced the object (to select it).
	public event Action<int>? Replaced;
	internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);

	// What is shown: the thing (index), its data, and what has been changed.
	public int? Index { get; private set; }
	private ZdoData? _z;
	private byte[]? _bytes;
	internal sealed class Field
	{
		public required string Section;
		public required int Key;
		public required string? Name;
		public required string Value;
		public string Now = "";
		public string? Note;
		public bool Removed;
	}
	internal List<Field> FieldList { get; private set; } = new();
	internal List<(string Section, string Key, string Value)> Added { get; } = new();
	internal sealed class ItemRow
	{
		public string Name = "Wood";
		public int Stack = 1, Quality = 1, X, Y, Variant, WorldLevel;
		public float Durability = 100;
		public string CrafterId = "0", CrafterName = "";
		public bool Equipped, PickedUp, Cheated;
		public Dictionary<string, string> CustomData = new();
		public ItemUpload Upload() => new(Name, null, Stack, Quality, Durability, X, Y, Variant, WorldLevel, CrafterId, CrafterName, Equipped, PickedUp, Cheated, CustomData);
	}
	internal List<ItemRow>? Items { get; private set; }

	// A stand's items: an item stand's one item, or an armour stand's slots (one row per kind of slot,
	// and any slot that holds something); the item stand's orientation or the armour stand's pose.
	internal sealed class StandRow
	{
		public int Slot;
		public int[] Types = Array.Empty<int>();
		public string Name = "";
		public int Quality = 1, Variant;
	}
	internal List<StandRow>? Stand { get; private set; }
	internal int StandWay { get; set; }
	private bool _armourStand;
	private string _standAtOpen = "";
	private string? _standError;
	private string _itemsAtOpen = "";
	private (int W, int H) _grid;
	private (int X, int Y)? _slot; // the slot picked in the grid
	private bool _moving; // the next slot clicked takes the picked slot's item
	private string? _inventoryError;

	public InspectorPanel(Func<EditSession?> session)
	{
		_session = session;
		var close = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(6, 0) }.Tip("card.close");
		close.Click += (_, _) => Close();
		AddSection.Tip("inspect.addSection");
		AddKey.Tip("inspect.addKey");
		AddValue.Tip("inspect.addValue");
		AddButton.Tip("inspect.add");
		ApplyButton.Tip("inspect.apply");
		RevertButton.Tip("inspect.revert");
		AddButton.Click += (_, _) =>
		{
			string key = AddKey.Text?.Trim() ?? "";
			if (key == "")
			{
				Message?.Invoke("Type the name of the value to add (for example text for a sign).");
				return;
			}
			Added.Add((Sections[Math.Max(0, AddSection.SelectedIndex)].Section, key, AddValue.Text ?? ""));
			AddKey.Text = AddValue.Text = "";
			Render();
		};
		RevertButton.Click += (_, _) => Reset();
		ApplyButton.Click += async (_, _) => await Apply();
		Card = new Border
		{
			Background = Ui.Panel,
			BorderBrush = Ui.Line,
			BoxShadow = BoxShadows.Parse("0 6 24 0 #59000000"),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(10),
			Padding = Ui.Pad,
			Margin = new Thickness(10),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			IsVisible = false,
			Child = new ScrollViewer
			{
				MaxHeight = 820,
				Content = new StackPanel
				{
					Width = 380,
					Spacing = 6,
					Children =
					{
						new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { _title, Col(close, 1) } },
						_where,
						ItemsBox,
						new TextBlock { Text = "DATA", FontSize = 10, Foreground = Ui.Muted, Margin = new Thickness(0, 6, 0, 0) },
						Fields,
						new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,*,Auto"), ColumnSpacing = 4, Children = { AddSection, Col(AddKey, 1), Col(AddValue, 2), Col(AddButton, 3) } },
						new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { ApplyButton, RevertButton } },
						new TextBlock
						{
							Text = "Applying replaces the object by a copy with the new data (Ctrl+Z puts the old one back), like a move does; Save writes it. Change values only if you know what they do: the game may reset or ignore wrong ones.",
							FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap,
						},
					},
				},
			},
		};
	}

	private static Control Col(Control c, int col)
	{
		Grid.SetColumn(c, col);
		return c;
	}

	public bool IsOpen => Card.IsVisible;

	public void Close()
	{
		Card.IsVisible = false;
		Index = null;
		Closed?.Invoke();
	}
	public event Action? Closed;

	// Opens on a thing of the scene; false when its data cannot be read.
	public bool Open(int index)
	{
		var s = _session();
		if (s == null || s.Scene.World == null)
		{
			return false;
		}
		var t = s.Scene.Things[index];
		byte[]? bytes;
		try
		{
			bytes = ObjectData.Bytes(s.Scene.World, s.Edits, t.Id);
		}
		catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
		{
			bytes = null;
		}
		if (bytes == null)
		{
			Message?.Invoke("That object cannot be read.");
			return false;
		}
		Index = index;
		_bytes = bytes;
		_z = ZdoData.Parse(bytes);
		Reset();
		Card.IsVisible = true;
		return true;
	}

	private static string S(float v) => v.ToString("R", CultureInfo.InvariantCulture);

	private void Reset()
	{
		var z = _z!;
		var list = new List<Field>();
		void Add(string section, int key, string value, string? note = null) => list.Add(new Field { Section = section, Key = key, Name = ZdoKeys.NameOf(key), Value = value, Now = value, Note = note });
		foreach (var (k, v) in z.FloatList) Add("floats", k, S(v));
		foreach (var (k, v) in z.Vec3List) Add("vec3", k, $"{S(v.X)} {S(v.Y)} {S(v.Z)}");
		foreach (var (k, v) in z.QuatList) Add("quats", k, $"{S(v.X)} {S(v.Y)} {S(v.Z)} {S(v.W)}");
		// Ints that are prefab or item hashes (item stands, cooking stations…) get the name beside them.
		foreach (var (k, v) in z.IntList) Add("ints", k, v.ToString(CultureInfo.InvariantCulture), PrefabCatalog.NameOf(v));
		foreach (var (k, v) in z.LongList) Add("longs", k, v.ToString(CultureInfo.InvariantCulture));
		foreach (var (k, v) in z.StringList) Add("strings", k, v);
		foreach (var (k, v) in z.ByteList) Add("bytes", k, Convert.ToBase64String(v), $"{v.Length} bytes");
		FieldList = list;
		Added.Clear();
		var details = PrefabCatalog.Details(z.Prefab);
		byte[]? items = z.GetBytes(ObjectData.ItemsKey);
		Items = null;
		_inventoryError = null;
		_grid = (details?.ContainerW ?? 0, details?.ContainerH ?? 0);
		if (items != null || details?.ContainerW > 0)
		{
			try
			{
				var inv = items != null ? InventoryData.Read(items) : new InventoryData();
				Items = inv.Items.Select(i => new ItemRow
				{
					Name = PrefabCatalog.NameOf(i.Prefab) ?? i.Prefab.ToString(CultureInfo.InvariantCulture), Stack = i.Stack, Quality = i.Quality, Durability = i.Durability,
					X = i.X, Y = i.Y, Variant = i.Variant, WorldLevel = i.WorldLevel, CrafterId = i.CrafterId.ToString(CultureInfo.InvariantCulture), CrafterName = i.CrafterName,
					Equipped = i.Equipped, PickedUp = i.PickedUp, Cheated = i.Cheated, CustomData = new(i.CustomData),
				}).ToList();
			}
			catch (Exception ex) when (ex is NotSupportedException or EndOfStreamException or IOException)
			{
				_inventoryError = ex.Message;
				Items = new();
			}
		}
		Stand = null;
		_armourStand = false;
		_standError = null;
		if (StandData.IsItemStand(z.Prefab))
		{
			var (held, way) = StandData.ReadItemStand(z);
			Stand = new() { new StandRow { Name = held?.Item ?? "", Quality = held?.Quality ?? 1, Variant = held?.Variant ?? 0 } };
			StandWay = way;
		}
		else if (PrefabCatalog.ArmourSlotsOf(z.Prefab) is { } slots)
		{
			_armourStand = true;
			var (held, pose) = StandData.ReadArmourStand(z, slots.Length);
			Stand = new();
			for (int i = 0; i < slots.Length; i++)
			{
				// ArmorStand has each of its slots twice: one row per kind, and any slot that holds something.
				int slot = i;
				if (held[i] == null && Enumerable.Range(0, i).Any(j => slots[j].SequenceEqual(slots[slot])))
				{
					continue;
				}
				Stand.Add(new StandRow { Slot = i, Types = slots[i], Name = held[i]?.Item ?? "", Quality = held[i]?.Quality ?? 1, Variant = held[i]?.Variant ?? 0 });
			}
			StandWay = pose;
		}
		_standAtOpen = StandKey();
		_itemsAtOpen = ItemsKey();
		_slot = null;
		_moving = false;
		Render();
	}

	private string ItemsKey() => Items == null ? "" : string.Join(";", Items.Select(i => $"{i.Name},{i.Stack},{i.Quality},{i.Durability},{i.X},{i.Y}"));

	private string StandKey() => Stand == null ? "" : string.Join(";", Stand.Select(r => $"{r.Name},{r.Quality},{r.Variant}")) + "|" + StandWay.ToString(CultureInfo.InvariantCulture);

	private static string Label(Field f) => f.Name is string n ? Labels.TryGetValue(n, out var l) ? l : n : $"#{f.Key}";

	private void Render()
	{
		var z = _z!;
		var s = _session();
		string? name = PrefabCatalog.DisplayName(z.Prefab);
		_title.Text = name ?? $"Prefab {z.Prefab}";
		int id = Index is int ix && s != null ? s.Scene.Things[ix].Id : 0;
		_where.Text = $"Object {id} · x {z.Position.X:0.00}, y {z.Position.Y:0.00}, z {z.Position.Z:0.00} · turned {z.Rotation.Y:0.0}°{(z.Connection != null ? " · has a connection (kept)" : "")}";
		RenderItems();
		RenderStand();
		Fields.Children.Clear();
		if (FieldList.Count == 0 && Added.Count == 0)
		{
			Fields.Children.Add(new TextBlock { Text = "This object holds no data: the game uses its defaults.", FontSize = 11, Foreground = Ui.Muted });
		}
		foreach (var f in FieldList)
		{
			bool isItems = f.Section == "bytes" && f.Key == ObjectData.ItemsKey && Items != null && _inventoryError == null;
			bool ro = f.Section == "bytes";
			var box = new TextBox { Text = isItems ? "see Contents above" : ro ? f.Note : f.Now, IsReadOnly = ro, FontSize = 11, Opacity = f.Removed ? 0.5 : 1 }.Tip("inspect.value");
			box.PropertyChanged += (_, e) =>
			{
				if (e.Property == TextBox.TextProperty && !ro)
				{
					f.Now = box.Text ?? "";
					Dirty();
				}
			};
			var rm = new Button { Content = f.Removed ? "↺" : "✕", FontSize = 11, Padding = new Thickness(4, 0) };
			rm.Tip(f.Removed ? "inspect.keepValue" : "inspect.removeValue");
			rm.Click += (_, _) => { f.Removed = !f.Removed; Render(); };
			var label = new TextBlock { Text = Label(f), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, TextDecorations = f.Removed ? TextDecorations.Strikethrough : null };
			ToolTip.SetTip(label, $"{f.Name} · {Sections.FirstOrDefault(x => x.Section == f.Section).Name ?? "data"} · key {f.Key}");
			var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,170,Auto"), ColumnSpacing = 4, Children = { label, Col(box, 1), Col(rm, 2) } };
			Fields.Children.Add(f.Note != null && !ro ? new StackPanel { Children = { row, new TextBlock { Text = f.Note, FontSize = 10, Foreground = Ui.Muted } } } : row);
		}
		foreach (var a in Added.ToList())
		{
			var x = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(4, 0) }.Tip("inspect.unadd");
			x.Click += (_, _) => { Added.Remove(a); Render(); };
			Fields.Children.Add(new Grid
			{
				ColumnDefinitions = new ColumnDefinitions("*,170,Auto"),
				ColumnSpacing = 4,
				Children = { new TextBlock { Text = $"{(Labels.TryGetValue(a.Key, out var l) ? l : a.Key)} (new)", FontSize = 11 }, Col(new TextBlock { Text = a.Value, FontSize = 11 }, 1), Col(x, 2) },
			});
		}
		Dirty();
	}

	private void RenderItems()
	{
		ItemsBox.Children.Clear();
		if (Items == null)
		{
			return;
		}
		var (w, h) = _grid;
		ItemsBox.Children.Add(new TextBlock { Text = $"CONTENTS · {(w > 0 ? $"{w} × {h} slots" : "size unknown")} · {Items.Count} item(s)", FontSize = 10, Foreground = Ui.Muted });
		if (_inventoryError != null)
		{
			ItemsBox.Children.Add(new TextBlock { Text = $"The contents cannot be read: {_inventoryError}", FontSize = 11, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap });
			return;
		}
		if (w > 0 && h > 0)
		{
			SlotGrid(w, h);
		}
		var rows = new StackPanel { Spacing = 3 };
		var details = new Expander
		{
			Header = new TextBlock { Text = "Every item's details", FontSize = 11 }, Content = rows, IsExpanded = w <= 0 || h <= 0 || Items.Any(i => i.X >= w || i.Y >= h),
			HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0),
		}.Tip("inspect.details");
		ItemsBox.Children.Add(details);
		rows.Children.Add(new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("*,54,44,50,40,40,26"),
			ColumnSpacing = 3,
			Children = { Head("Item", 0), Head("Stack", 1), Head("Qual.", 2), Head("Dur. %", 3), Head("X", 4), Head("Y", 5) },
		});
		foreach (var it in Items.ToList())
		{
			var name = new AutoCompleteBox { Text = it.Name, ItemsSource = PrefabCatalog.Items, FilterMode = AutoCompleteFilterMode.ContainsOrdinal, FontSize = 11, MinimumPrefixLength = 2 }.Tip("inspect.item");
			name.PropertyChanged += (_, e) => { if (e.Property == AutoCompleteBox.TextProperty) { it.Name = name.Text?.Trim() ?? ""; Dirty(); } };
			NumericUpDown Num(int v, int min, int max, Action<int> set, string tip)
			{
				var n = new NumericUpDown { Value = v, Minimum = min, Maximum = max, Increment = 1, FormatString = "0", FontSize = 11, ShowButtonSpinner = false }.Tip(tip);
				n.ValueChanged += (_, e) => { set((int)(e.NewValue ?? min)); Dirty(); };
				return n;
			}
			var x = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(4, 0) };
			x.Tip("inspect.removeItem");
			x.Click += (_, _) => { Items.Remove(it); RenderItems(); Dirty(); };
			bool bad = w > 0 && (it.X >= w || it.Y >= h);
			rows.Children.Add(new Grid
			{
				ColumnDefinitions = new ColumnDefinitions("*,54,44,50,40,40,26"),
				ColumnSpacing = 3,
				Children =
				{
					name,
					Col(Num(it.Stack, 1, 65535, v => it.Stack = v, "inspect.count"), 1),
					Col(Num(it.Quality, 1, 10, v => it.Quality = v, "inspect.quality"), 2),
					Col(Num((int)MathF.Round(it.Durability), 0, 100000, v => it.Durability = v, "inspect.durability"), 3),
					Col(WithBorder(Num(it.X, 0, 255, v => it.X = v, "inspect.slotX"), bad), 4),
					Col(WithBorder(Num(it.Y, 0, 255, v => it.Y = v, "inspect.slotY"), bad), 5),
					Col(x, 6),
				},
			});
		}
		var add = new Button { Content = "Add item", FontSize = 11 }.Tip("inspect.addItem");
		add.Click += (_, _) =>
		{
			if (FreeSlot() is not var (fx, fy))
			{
				Message?.Invoke("No free slot left in this container.");
				return;
			}
			Items.Add(new ItemRow { X = fx, Y = fy });
			RenderItems();
			Dirty();
		};
		var tidy = new Button { Content = "Tidy slots", FontSize = 11 };
		tidy.Tip("inspect.tidy");
		tidy.Click += (_, _) =>
		{
			int width = w > 0 ? w : 4;
			for (int i = 0; i < Items.Count; i++)
			{
				Items[i].X = i % width;
				Items[i].Y = i / width;
			}
			RenderItems();
			Dirty();
		};
		rows.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { add, tidy } });
	}

	// The container's slots as in game, w × h: each shows its item and how many. Click one to choose
	// what it holds (or empty it); Move, then another slot, moves the item there (or swaps the two).
	private void SlotGrid(int w, int h)
	{
		var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
		for (int x = 0; x < w; x++)
		{
			grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Math.Min(46, 376.0 / w))));
		}
		for (int y = 0; y < h; y++)
		{
			grid.RowDefinitions.Add(new RowDefinition(new GridLength(46)));
		}
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				var at = (x, y);
				var it = Items!.FirstOrDefault(i => i.X == x && i.Y == y);
				var cell = new Button
				{
					Margin = new Thickness(1), Padding = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
					HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
					Background = it != null ? Ui.Panel2 : Ui.Bg, BorderThickness = new Thickness(_slot == at ? 2 : 1),
					BorderBrush = _slot == at ? new SolidColorBrush(Ui.AccentColor) : Ui.Line,
					Content = it == null ? null : new Grid
					{
						Children =
						{
							new TextBlock { Text = it.Name, FontSize = 9, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2 },
							new TextBlock { Text = it.Stack > 1 ? it.Stack.ToString(CultureInfo.InvariantCulture) : "", FontSize = 11, FontWeight = FontWeight.SemiBold,
								HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom },
						},
					},
				}.Tip("inspect.slot");
				if (it != null)
				{
					ToolTip.SetTip(cell, $"{it.Name} × {it.Stack} (slot {x + 1}, {y + 1})");
				}
				cell.Click += (_, _) =>
				{
					if (_moving && _slot is var (fx, fy) && _slot != at)
					{
						var from = Items!.FirstOrDefault(i => i.X == fx && i.Y == fy);
						var to = Items!.FirstOrDefault(i => i.X == at.x && i.Y == at.y);
						if (from != null)
						{
							(from.X, from.Y) = at;
						}
						if (to != null)
						{
							(to.X, to.Y) = (fx, fy);
						}
						Dirty();
					}
					_moving = false;
					_slot = at;
					RenderItems();
				};
				Grid.SetColumn(cell, x);
				Grid.SetRow(cell, y);
				grid.Children.Add(cell);
			}
		}
		ItemsBox.Children.Add(grid);
		if (_slot is not var (sx, sy) || sx >= w || sy >= h)
		{
			ItemsBox.Children.Add(new TextBlock { Text = "Click a slot to choose what it holds.", FontSize = 11, Foreground = Ui.Muted });
			return;
		}
		// The picked slot: its item (any of the game's), how many, how good.
		var items = Items!;
		var item = items.FirstOrDefault(i => i.X == sx && i.Y == sy);
		var pick = new AutoCompleteBox
		{
			Text = item?.Name ?? "", ItemsSource = PrefabCatalog.Items, FilterMode = AutoCompleteFilterMode.ContainsOrdinal, FontSize = 11, MinimumPrefixLength = 1,
			PlaceholderText = "Find an item…",
		}.Tip("inspect.slotItem");
		void Chosen()
		{
			string name = pick.Text?.Trim() ?? "";
			var known = PrefabCatalog.Items.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
			// What the slot holds now: a pick and the box then losing focus (once the grid is drawn
			// again) both come here, and must not put the item in twice.
			var now = items.FirstOrDefault(i => i.X == sx && i.Y == sy);
			if (known == null || known == now?.Name)
			{
				return;
			}
			if (now == null)
			{
				items.Add(new ItemRow { Name = known, X = sx, Y = sy });
			}
			else
			{
				now.Name = known;
			}
			Dirty();
			RenderItems();
		}
		pick.SelectionChanged += (_, _) => Chosen();
		pick.LostFocus += (_, _) => Chosen();
		var count = new NumericUpDown { Value = item?.Stack ?? 1, Minimum = 1, Maximum = 9999, Increment = 1, FormatString = "0", FontSize = 11, Width = 90, IsEnabled = item != null }.Tip("inspect.count");
		count.ValueChanged += (_, e) =>
		{
			if (item != null)
			{
				item.Stack = (int)(e.NewValue ?? 1);
				Dirty();
			}
		};
		var quality = new NumericUpDown { Value = item?.Quality ?? 1, Minimum = 1, Maximum = 10, Increment = 1, FormatString = "0", FontSize = 11, Width = 70, IsEnabled = item != null }.Tip("inspect.quality");
		quality.ValueChanged += (_, e) =>
		{
			if (item != null)
			{
				item.Quality = (int)(e.NewValue ?? 1);
				Dirty();
			}
		};
		var empty = new Button { Content = "Empty", FontSize = 11, IsEnabled = item != null }.Tip("inspect.emptySlot");
		empty.Click += (_, _) =>
		{
			items.Remove(item!);
			Dirty();
			RenderItems();
		};
		var move = new Button { Content = _moving ? "Click a slot…" : "Move", FontSize = 11, IsEnabled = item != null }.Tip("inspect.moveSlot");
		move.Click += (_, _) =>
		{
			_moving = !_moving;
			RenderItems();
		};
		ItemsBox.Children.Add(new TextBlock { Text = $"SLOT {sx + 1}, {sy + 1}", FontSize = 10, Foreground = Ui.Muted });
		ItemsBox.Children.Add(pick);
		ItemsBox.Children.Add(new StackPanel
		{
			Orientation = Orientation.Horizontal, Spacing = 6,
			Children =
			{
				new TextBlock { Text = "How many", FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, count,
				new TextBlock { Text = "Quality", FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, quality, empty, move,
			},
		});
	}

	// An item stand's item and orientation, or an armour stand's slots and pose; each list offers only
	// what the game lets that stand or slot take.
	private void RenderStand()
	{
		if (Stand == null || _z == null)
		{
			return;
		}
		int prefab = _z.Prefab;
		ItemsBox.Children.Add(new TextBlock { Text = _armourStand ? "WEARS" : "HOLDS", FontSize = 10, Foreground = Ui.Muted });
		ItemsBox.Children.Add(new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("*,44,44,26"),
			ColumnSpacing = 3,
			Children = { Head("Item", 0), Head("Qual.", 1), Head("Style", 2) },
		});
		foreach (var row in Stand)
		{
			var choices = PrefabCatalog.Items.Where(n => _armourStand
				? PrefabCatalog.ItemKindOf(StableHash.Of(n)) is { } k && StandData.FitsArmourSlot(row.Types, k)
				: StandData.ItemStandRefusal(prefab, n) == null).ToList();
			string slotName = row.Types.Length == 0 ? "Anything" : string.Join(", ", row.Types.Select(StandData.TypeName).Distinct());
			var name = new AutoCompleteBox
			{
				Text = row.Name, ItemsSource = choices, FilterMode = AutoCompleteFilterMode.ContainsOrdinal, FontSize = 11, MinimumPrefixLength = 1,
				PlaceholderText = _armourStand ? slotName : "Find an item…",
			}.Tip("inspect.standItem");
			name.PropertyChanged += (_, e) =>
			{
				if (e.Property == AutoCompleteBox.TextProperty)
				{
					row.Name = name.Text?.Trim() ?? "";
					Dirty();
				}
			};
			var empty = new Button { Content = "✕", FontSize = 11, Padding = new Thickness(4, 0), IsEnabled = row.Name != "" }.Tip("inspect.emptyStand");
			empty.Click += (_, _) =>
			{
				row.Name = "";
				Dirty();
				RenderItems();
				RenderStand();
			};
			if (_armourStand)
			{
				ItemsBox.Children.Add(new TextBlock { Text = slotName, FontSize = 10, Foreground = Ui.Muted });
			}
			ItemsBox.Children.Add(new Grid
			{
				ColumnDefinitions = new ColumnDefinitions("*,44,44,26"),
				ColumnSpacing = 3,
				Children =
				{
					name,
					Col(StandNum(row.Quality, 1, 10, v => row.Quality = v, "inspect.quality"), 1),
					Col(StandNum(row.Variant, 0, 99, v => row.Variant = v, "inspect.variant"), 2),
					Col(empty, 3),
				},
			});
		}
		ItemsBox.Children.Add(new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 6,
			Children =
			{
				new TextBlock { Text = _armourStand ? "Pose" : "Hangs", FontSize = 11, VerticalAlignment = VerticalAlignment.Center },
				StandNum(StandWay, 0, 99, v => StandWay = v, _armourStand ? "inspect.pose" : "inspect.orientation"),
			},
		});
		if (_standError != null)
		{
			ItemsBox.Children.Add(new TextBlock { Text = _standError, FontSize = 11, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap });
		}
	}

	private NumericUpDown StandNum(int v, int min, int max, Action<int> set, string tip)
	{
		var n = new NumericUpDown { Value = v, Minimum = min, Maximum = max, Increment = 1, FormatString = "0", FontSize = 11, ShowButtonSpinner = false, Width = 60 }.Tip(tip);
		n.ValueChanged += (_, e) =>
		{
			set((int)(e.NewValue ?? min));
			Dirty();
		};
		return n;
	}

	private static Control Head(string t, int col) => Col(new TextBlock { Text = t, FontSize = 10, Foreground = Ui.Muted }, col);

	private static NumericUpDown WithBorder(NumericUpDown n, bool bad)
	{
		if (bad)
		{
			n.BorderBrush = Brushes.Orange;
		}
		return n;
	}

	// The first free slot, row by row (size unknown: 4 wide, 2 rows: the smallest containers).
	internal (int X, int Y)? FreeSlot()
	{
		var (w, h) = _grid;
		int gw = w > 0 ? w : 4, gh = h > 0 ? h : Math.Max(2, Items!.Select(i => i.Y + 1).DefaultIfEmpty(0).Max());
		for (int y = 0; y < gh; y++)
		{
			for (int x = 0; x < gw; x++)
			{
				if (!Items!.Any(i => i.X == x && i.Y == y))
				{
					return (x, y);
				}
			}
		}
		return null;
	}

	// The changes made: values set (null: removed), and the contents when they changed.
	internal (List<FieldChange> Set, List<ItemUpload>? Inventory) Changes()
	{
		var set = new List<FieldChange>();
		foreach (var f in FieldList)
		{
			if (f.Removed)
			{
				set.Add(new FieldChange(f.Section, f.Key.ToString(CultureInfo.InvariantCulture), null));
			}
			else if (f.Now != f.Value && f.Section != "bytes")
			{
				set.Add(new FieldChange(f.Section, f.Key.ToString(CultureInfo.InvariantCulture), f.Now));
			}
		}
		foreach (var (section, key, value) in Added)
		{
			set.Add(new FieldChange(section, key, value));
		}
		var inv = Items != null && _inventoryError == null && ItemsKey() != _itemsAtOpen ? Items.Select(i => i.Upload()).ToList() : null;
		_standError = null;
		if (Stand != null && _z != null && StandKey() != _standAtOpen)
		{
			try
			{
				var held = Stand.Where(r => r.Name != "").Select(r => new StandData.Held(r.Name, r.Quality, r.Variant)).ToList();
				set.AddRange(_armourStand ? StandData.ForArmourStand(_z.Prefab, held, StandWay) : StandData.ForItemStand(_z.Prefab, held.FirstOrDefault(), StandWay));
			}
			catch (ArgumentException ex)
			{
				_standError = ex.Message;
			}
		}
		return (set, inv);
	}

	private void Dirty()
	{
		var (set, inv) = Changes();
		ApplyButton.IsEnabled = RevertButton.IsEnabled = set.Count > 0 || inv != null;
	}

	internal async Task Apply()
	{
		if (_session() is not { } s || Index is not int index || _bytes == null)
		{
			return;
		}
		var (set, inv) = Changes();
		if (_standError != null)
		{
			Message?.Invoke(_standError);
			return;
		}
		var (w, h) = _grid;
		if (inv != null && w > 0 && inv.Any(i => i.X >= w || i.Y >= h)
			&& !await Confirm($"Some items are outside the {w} × {h} slots of this container: the game would not show them. Apply anyway?"))
		{
			return;
		}
		ZdoData z;
		try
		{
			z = ObjectData.Edited(_bytes, set, inv);
		}
		catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
		{
			Message?.Invoke($"Could not change the object: {ex.Message}");
			return;
		}
		var t = s.Scene.Things[index];
		// Live: changed or removed in the game since it was opened (a player used the chest): the copy
		// would bring back its old data. Shown as it is now, to change again.
		if (t.Gone || !(ObjectData.Bytes(s.Scene.World, s.Edits, t.Id)?.AsSpan().SequenceEqual(_bytes) ?? false))
		{
			Message?.Invoke(t.Gone ? "This object is gone (removed in the game): nothing changed." : "This object changed in the game since you opened it: here it is as it is now, make your change again.");
			if (t.Gone)
			{
				Close();
			}
			else
			{
				Open(index);
			}
			return;
		}
		string name = PrefabCatalog.DisplayName(z.Prefab) ?? "object";
		var copies = s.Commit($"Edited {name}", null, new[] { index }, new[] { (new NewObject(0, z.Prefab, z.Position, z.Rotation, 0, null, false, z.Serialize()), t.Piece) });
		Message?.Invoke($"Changed {name}. Ctrl+Z puts the old one back; Save writes it.");
		Open(copies[0]);
		Replaced?.Invoke(copies[0]);
	}
}
