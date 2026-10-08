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
			case "picture":
				await (w.MapShown ? w.MapPage!.Map.Picture(a[1]) : w.View.Picture(a[1])).WaitAsync(TimeSpan.FromSeconds(60));
				return State(w);
			case "state":
			case "quit":
				return State(w);
			default:
				throw new InvalidOperationException("unknown command " + a[0]);
		}
	}

	private static string State(MainWindow w)
	{
		var s = w.Session;
		return JsonSerializer.Serialize(new
		{
			page = w.MapShown ? "map" : s != null ? "editor" : "start",
			objects = s?.Scene.Things.Count(t => !t.Gone) ?? 0,
			pending = w.World?.Pending.Zones ?? 0,
			frames = w.MapShown ? w.MapPage!.Map.FramesDrawn : w.View.FramesDrawn,
		});
	}
}
