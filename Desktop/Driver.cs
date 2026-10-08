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
//   bench <seconds>                the 3D camera turns on its own; the frame rates come back
//   mouse <down|move|up> <x> <z> [left|right|middle] [shift|ctrl|alt ...]
//                                  the mouse at world x, z over the 3D view (real pointer events)
//   wheel <x> <z> <steps>          the wheel there (positive: zoom in)
//   key <name> [shift|ctrl|alt ...] a key pressed in the window (Avalonia key names: E, Escape, Enter, D1...)
//   mapview <x> <z> <m per pixel>  the map looking there
//   search <text>                  the map's search (objects by kind)
//   zones                          the map's zone filter: show the matching zones
//   state                          what is shown, the objects, what is pending, frames drawn
//   quit
public static class Driver
{
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
		}) { IsBackground = true, Name = "Driver" };
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
			case "bench":
			{
				string result = await w.View.Benchmark(F(0)).WaitAsync(TimeSpan.FromSeconds(F(0) + 60));
				return JsonSerializer.Serialize(new { bench = result });
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
