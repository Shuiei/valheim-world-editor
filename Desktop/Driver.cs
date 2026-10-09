using System.Text.Json;
using Avalonia.Threading;

namespace TerrainEditor.Desktop;

// --driver: the editor driven by another program (the visual tests), one command per line on its
// input, one answer per line on its output ("@@ ok <json>" or "@@ error <message>"), so a single
// editor serves many tests. Commands:
//   world <folder>                 open a world (its map)
//   area <zx> <zz> <size>          open an area in the 3D editor
//   map                            back to the map
//   stroke <tool> <x> <z> <steps>  a brush stroke at world x, z (tool: raise, lower, smooth...)
//   picture <file.png>             a picture of the view shown (3D or map), once it is drawn
//   shot <file.png>                a picture of the whole window, panels and view (documentation)
//   look <game|seethrough> <on|off>, look res <sharp|balanced|fast>
//                                  the View panel's Look switches
//   camera <x> <z> <yaw°> <pitch°> <distance> [y]  the 3D view's camera on world x, z (at height y:
//                                              inside a dungeon)
//   click <text>                   the visible button, switch or box labelled so, or whose words
//                                  begin so (windows and dialogs)
//   choose <text>                  the entry so named in whichever visible list has it
//   type <hint>|<text>             the visible text box whose placeholder contains hint gets text
//   set <label>|<value>            the slider, text box(es: a,b) or list beside the label so worded
//   panel <view|history|help|none> the right-hand panel shown
//   message [text]                 the status bar's message (none: cleared)
//   wait <ms>                      a pause (a brush stroke works frame by frame while held)
//   bench <seconds>                the 3D camera turns on its own; the frame rates come back
//   mouse <down|move|up> <x> <z> [left|right|middle] [shift|ctrl|alt ...]
//                                  the mouse at world x, z over the 3D view (real pointer events)
//   wheel <x> <z> <steps>          the wheel there (positive: zoom in)
//   key <name> [shift|ctrl|alt ...] a key pressed in the window (Avalonia key names: E, Escape, Enter, D1...)
//   mapview <x> <z> <m per pixel>  the map looking there
//   mappick <zone x> <zone z>      the map's zone picked (as a click on it)
//   search <text>                  the map's search (objects by kind)
//   zones                          the map's zone filter: show the matching zones
//   workshop [file.blueprint]      the Workshop opened (on that blueprint); how long it took
//   state                          what is shown, the objects, what is pending, frames drawn
//   quit
public static class Driver
{
	// The View panel's 3D resolution choices, in their list's order.
	private static readonly string[] Resolutions = { "sharp", "balanced", "fast" };

	public static void Start(MainWindow w)
	{
		var thread = new Thread(() =>
		{
			string? line;
			while ((line = Console.ReadLine()) != null)
			{
				string cmd = line.Trim();
				if (cmd.Length == 0)
				{
					continue;
				}
				string answer;
				try
				{
					answer = "@@ ok " + Dispatcher.UIThread.InvokeAsync(() => Run(w, cmd)).GetAwaiter().GetResult();
				}
				catch (Exception ex)
				{
					answer = "@@ error " + (ex.InnerException ?? ex).Message.Replace('\n', ' ');
				}
				Console.WriteLine(answer);
				Console.Out.Flush();
				if (cmd == "quit")
				{
					break;
				}
			}
			Dispatcher.UIThread.Post(w.CloseWithoutAsking);
		});
		thread.IsBackground = true;
		thread.Name = "Driver";
		thread.Start();
		Console.WriteLine("@@ ready");
		Console.Out.Flush();
	}

	private static async Task<string> Run(MainWindow w, string cmd)
	{
		string[] a = cmd.Split(' ', 2);
		string[] args = a.Length > 1 ? a[1].Split(' ') : Array.Empty<string>();
		float F(int i) => float.Parse(args[i], System.Globalization.CultureInfo.InvariantCulture);
		switch (a[0])
		{
			case "spirv":
				// SPIRV-Cross (the terrain shader from Valheim for Windows) loads in this build.
				TerrainEditor.App.TerrainShader.SelfTest();
				return "{\"spirvCross\":\"ok\"}";
			case "world":
				await w.OpenWorld(() => Task.Run(() => WorldSession.Open(a[1])), "Opening the world…");
				return State(w);
			case "area":
				await w.EditArea((int)F(0), (int)F(1), (int)F(2));
				return State(w);
			case "map":
				w.ShowMap();
				return State(w);
			case "stroke":
			{
				var s = w.Session ?? throw new InvalidOperationException("no area open");
				var tool = Enum.Parse<BrushTool>(args[0], ignoreCase: true);
				float gx = F(1) - (s.Scene.X0 * 64 - 32), gz = F(2) - (s.Scene.Z0 * 64 - 32);
				s.BeginStroke(tool, gx, gz);
				for (int i = 0; i < (int)F(3); i++)
				{
					s.StrokeStep(gx, gz, 0.05f);
				}
				s.EndStroke();
				return State(w);
			}
			case "mouse":
			{
				var p = ScreenAt(w, F(1), F(2));
				var button = args.Length > 3 ? args[3] : "left";
				Mouse(w, args[0], p, button, Mods(args.Skip(4)));
				return State(w);
			}
			case "mouse3":
			{
				// A point in the air: world x, height above the ground, z (pieces, for the Workshop).
				var sc = w.View.Scene ?? throw new InvalidOperationException("no area open");
				var g = new System.Numerics.Vector2(F(1) - (sc.X0 * 64 - 32), F(3) - (sc.Z0 * 64 - 32));
				var p = w.View.ScreenOfGrid(g, F(2)) ?? throw new InvalidOperationException("not in view");
				Mouse(w, args[0], p, args.Length > 4 ? args[4] : "left", Mods(args.Skip(5)));
				return State(w);
			}
			case "wheel":
			{
				var p = ScreenAt(w, F(0), F(1));
				var pointer = new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), Avalonia.Input.PointerType.Mouse, true);
				w.Surface.RaiseEvent(new Avalonia.Input.PointerWheelEventArgs(w.Surface, pointer, w.Surface, p, 0, new Avalonia.Input.PointerPointProperties(), Avalonia.Input.KeyModifiers.None, new Avalonia.Vector(0, F(2))));
				return State(w);
			}
			case "key":
			{
				var key = Enum.Parse<Avalonia.Input.Key>(args[0], ignoreCase: true);
				w.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = key, KeyModifiers = Mods(args.Skip(1)), Source = w });
				w.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyUpEvent, Key = key, KeyModifiers = Mods(args.Skip(1)), Source = w });
				return State(w);
			}
			case "mapview":
				w.MapPage!.Map.LookAt(F(0), F(1), F(2));
				return State(w);
			case "mappick":
				w.MapPage!.Pick((int)F(0), (int)F(1));
				return State(w);
			case "search":
				w.MapPage!.SearchBox.Text = a[1];
				await w.MapPage.Search();
				return State(w);
			case "zones":
				w.MapPage!.NoBuildBox.IsChecked = w.MapPage.NoEditBox.IsChecked = w.MapPage.OnlyGenBox.IsChecked = false;
				await w.MapPage.ShowMatching();
				return State(w);
			case "picture":
				await (w.MapShown ? w.MapPage!.Map.Picture(a[1]) : w.View.Picture(a[1])).WaitAsync(TimeSpan.FromSeconds(60));
				return State(w);
			case "shot":
			{
				// Once the view shown has its area and models in (its own picture waits for that).
				string glPicture = Path.Combine(Path.GetTempPath(), $"vwe-shot-{Guid.NewGuid():N}.png");
				try
				{
					if (w.MapShown || w.Session != null)
					{
						await (w.MapShown ? w.MapPage!.Map.Picture(glPicture) : w.View.Picture(glPicture)).WaitAsync(TimeSpan.FromSeconds(60));
					}
					await WindowShot.Save(w, a[1]);
				}
				finally
				{
					File.Delete(glPicture);
				}
				return State(w);
			}
			case "look":
				switch (args[0])
				{
					case "game": w.GameLookBox.IsChecked = args[1] == "on"; break;
					case "seethrough": w.SeeThroughBox.IsChecked = args[1] == "on"; break;
					case "res": w.ResolutionBox.SelectedIndex = Array.IndexOf(Resolutions, args[1]); break;
					default: throw new InvalidOperationException("unknown look " + args[0]);
				}
				return State(w);
			case "type":
			{
				int bar = a[1].IndexOf('|');
				string hint = a[1][..bar], text = a[1][(bar + 1)..];
				var box = Find<Avalonia.Controls.TextBox>(w, b => (b.PlaceholderText ?? "").Contains(hint, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"no text box \"{hint}\"");
				box.Text = text;
				return State(w);
			}
			case "set":
			{
				int bar = a[1].IndexOf('|');
				string label = a[1][..bar], value = a[1][(bar + 1)..];
				var tb = Find<Avalonia.Controls.TextBlock>(w, t => string.Equals(t.Text?.Trim(), label, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"no label \"{label}\"");
				// The row holding the label: the first control after it.
				var row = (Avalonia.Visual?)Avalonia.VisualTree.VisualExtensions.GetVisualParent(tb);
				for (int up = 0; up < 3 && row != null; up++, row = Avalonia.VisualTree.VisualExtensions.GetVisualParent(row))
				{
					var c = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(row).OfType<Avalonia.Controls.Control>()
						.FirstOrDefault(c => c is Avalonia.Controls.Slider or Avalonia.Controls.TextBox or Avalonia.Controls.ComboBox or Avalonia.Controls.NumericUpDown && c.IsEffectivelyVisible);
					switch (c)
					{
						case Avalonia.Controls.Slider sl:
							sl.Value = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
							return State(w);
						case Avalonia.Controls.NumericUpDown:
						{
							// Several boxes in the row (Height min, max): in order, comma separated; empty for none.
							var boxes = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(row).OfType<Avalonia.Controls.NumericUpDown>().Where(b => b.IsEffectivelyVisible).ToList();
							var values = value.Split(',');
							for (int k = 0; k < Math.Min(values.Length, boxes.Count); k++)
							{
								boxes[k].Value = values[k].Trim().Length == 0 ? null : decimal.Parse(values[k], System.Globalization.CultureInfo.InvariantCulture);
							}
							return State(w);
						}
						case Avalonia.Controls.TextBox:
						{
							// Several boxes in the row (Size % from, to): the values in order, comma separated.
							var boxes = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(row).OfType<Avalonia.Controls.TextBox>().Where(b => b.IsEffectivelyVisible).ToList();
							var values = value.Split(',');
							for (int k = 0; k < Math.Min(values.Length, boxes.Count); k++)
							{
								boxes[k].Text = values[k].Trim();
							}
							return State(w);
						}
						case Avalonia.Controls.ComboBox cb when IndexOf(cb, value) >= 0:
							cb.SelectedIndex = IndexOf(cb, value);
							return State(w);
					}
				}
				throw new InvalidOperationException($"nothing to set beside \"{label}\"");
			}
			case "panel":
				w.ShowRightPanel(args[0]);
				return State(w);
			case "message":
				w.StatusMessage = a.Length > 1 ? a[1] : "";
				return State(w);
			case "wait":
				await Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp(F(0), 0, 60000)));
				return State(w);
			case "camera":
				w.View.Orbit(F(0), F(1), F(2), F(3), F(4), args.Length > 5 ? F(5) : null);
				return State(w);
			case "click":
			{
				var c = Find<Avalonia.Controls.Button>(w, b => string.Equals(TextOf(b), a[1], StringComparison.OrdinalIgnoreCase))
					?? Find<Avalonia.Controls.Button>(w, b => TextOf(b).StartsWith(a[1] + " ", StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"no button \"{a[1]}\"");
				if (c is Avalonia.Controls.Primitives.ToggleButton t)
				{
					t.IsChecked = t.IsChecked != true;
				}
				c.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
				return State(w);
			}
			case "choose":
			{
				var box = Find<Avalonia.Controls.ComboBox>(w, b => IndexOf(b, a[1]) >= 0) ?? throw new InvalidOperationException($"no list with \"{a[1]}\"");
				box.SelectedIndex = IndexOf(box, a[1]);
				return State(w);
			}
			case "bench":
			{
				string result = await w.View.Benchmark(F(0)).WaitAsync(TimeSpan.FromSeconds(F(0) + 60));
				return JsonSerializer.Serialize(new { bench = result });
			}
			case "workshop":
			{
				var sw = System.Diagnostics.Stopwatch.StartNew();
				await w.OpenWorkshop(a.Length > 1 ? a[1] : null);
				return JsonSerializer.Serialize(new { opened = sw.ElapsedMilliseconds, objects = w.View.Scene?.Things.Count(t => !t.Gone) ?? 0 });
			}
			case "state":
			case "quit":
				return State(w);
			default:
				throw new InvalidOperationException("unknown command " + a[0]);
		}
	}

	private static Avalonia.Input.KeyModifiers Mods(IEnumerable<string> names)
	{
		var m = Avalonia.Input.KeyModifiers.None;
		foreach (var n in names)
		{
			m |= n switch { "shift" => Avalonia.Input.KeyModifiers.Shift, "ctrl" => Avalonia.Input.KeyModifiers.Control, "alt" => Avalonia.Input.KeyModifiers.Alt, _ => Avalonia.Input.KeyModifiers.None };
		}
		return m;
	}

	// Where world x, z (on the ground) is over the 3D view.
	// The first visible, enabled control of that type in the window or its open dialogs.
	private static T? Find<T>(MainWindow w, Func<T, bool> match) where T : Avalonia.Controls.Control
	{
		IEnumerable<Avalonia.Controls.Window> windows = new Avalonia.Controls.Window[] { w }.Concat(w.OwnedWindows);
		foreach (var win in windows.Reverse())
		{
			foreach (var c in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(win).OfType<T>())
			{
				if (c.IsEffectivelyVisible && c.IsEffectivelyEnabled && match(c))
				{
					return c;
				}
			}
		}
		return null;
	}

	// A control's own words: its text content, or every text inside it.
	private static string TextOf(Avalonia.Controls.ContentControl c) => c.Content is string s ? s.Trim()
		: string.Join(" ", Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(c).OfType<Avalonia.Controls.TextBlock>().Select(t => t.Text?.Trim()).Where(t => !string.IsNullOrEmpty(t))).Trim();

	private static int IndexOf(Avalonia.Controls.ComboBox box, string text)
	{
		int i = 0;
		foreach (var item in box.Items)
		{
			string? name = item is Avalonia.Controls.ContentControl cc ? cc.Content?.ToString() : item?.ToString();
			if (string.Equals(name?.Trim(), text, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
			i++;
		}
		return -1;
	}

	private static Avalonia.Point ScreenAt(MainWindow w, float x, float z)
	{
		var s = w.View.Scene ?? throw new InvalidOperationException("no area open");
		var g = new System.Numerics.Vector2(x - (s.X0 * 64 - 32), z - (s.Z0 * 64 - 32));
		return w.View.ScreenOfGrid(g, 0) ?? throw new InvalidOperationException($"{x}, {z} is not in view");
	}

	private static readonly Avalonia.Input.Pointer TheMouse = new(Avalonia.Input.Pointer.GetNextFreeId(), Avalonia.Input.PointerType.Mouse, true);

	private static void Mouse(MainWindow w, string what, Avalonia.Point p, string button, Avalonia.Input.KeyModifiers mods)
	{
		var raw = (Avalonia.Input.RawInputModifiers)mods;
		var (kind, down) = button switch
		{
			"right" => (what == "up" ? Avalonia.Input.PointerUpdateKind.RightButtonReleased : Avalonia.Input.PointerUpdateKind.RightButtonPressed, Avalonia.Input.RawInputModifiers.RightMouseButton),
			"middle" => (what == "up" ? Avalonia.Input.PointerUpdateKind.MiddleButtonReleased : Avalonia.Input.PointerUpdateKind.MiddleButtonPressed, Avalonia.Input.RawInputModifiers.MiddleMouseButton),
			_ => (what == "up" ? Avalonia.Input.PointerUpdateKind.LeftButtonReleased : Avalonia.Input.PointerUpdateKind.LeftButtonPressed, Avalonia.Input.RawInputModifiers.LeftMouseButton),
		};
		var surface = w.Surface;
		switch (what)
		{
			case "down":
				_held = down;
				surface.RaiseEvent(new Avalonia.Input.PointerPressedEventArgs(surface, TheMouse, surface, p, 0, new Avalonia.Input.PointerPointProperties(raw | down, kind), mods, 1));
				break;
			case "move":
				surface.RaiseEvent(new Avalonia.Input.PointerEventArgs(Avalonia.Input.InputElement.PointerMovedEvent, surface, TheMouse, surface, p, 0, new Avalonia.Input.PointerPointProperties(raw | _held, Avalonia.Input.PointerUpdateKind.Other), mods));
				break;
			default:
				_held = Avalonia.Input.RawInputModifiers.None;
				surface.RaiseEvent(new Avalonia.Input.PointerReleasedEventArgs(surface, TheMouse, surface, p, 0, new Avalonia.Input.PointerPointProperties(raw, kind), mods, kind == Avalonia.Input.PointerUpdateKind.RightButtonReleased ? Avalonia.Input.MouseButton.Right : kind == Avalonia.Input.PointerUpdateKind.MiddleButtonReleased ? Avalonia.Input.MouseButton.Middle : Avalonia.Input.MouseButton.Left));
				break;
		}
	}

	private static Avalonia.Input.RawInputModifiers _held;

	private static string State(MainWindow w)
	{
		var s = w.Session;
		return JsonSerializer.Serialize(new
		{
			page = w.MapShown ? "map" : s != null ? "editor" : "start",
			objects = s?.Scene.Things.Count(t => !t.Gone) ?? 0,
			pending = w.World?.Pending.Zones ?? 0,
			frames = w.MapShown ? w.MapPage!.Map.FramesDrawn : w.View.FramesDrawn,
			glErrors = w.View.GlErrors + (w.MapPage?.Map.GlErrors ?? 0),
			selected = w.View.Selected.Count,
			mode = w.Tools.Mode.ToString(),
			message = w.MessageText.Text ?? "",
			added = w.World?.Pending.Added ?? 0,
			yaw = w.View.Camera.Yaw,
			distance = w.View.Camera.Distance,
			targetX = w.View.Camera.Target.X,
			mapX = w.MapPage?.Map.Center.X ?? 0,
			mapScale = w.MapPage?.Map.MetersPerPixel ?? 0,
			pins = w.MapPage?.Map.Pins.Count ?? 0,
			matches = w.MapPage?.Matches.Count ?? 0,
			detail = w.MapPage?.Map.DetailShown ?? false,
		});
	}
}
