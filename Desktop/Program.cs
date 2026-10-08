using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace TerrainEditor.Desktop;

// The native editor (prototype): an Avalonia window drawing an area of a world with OpenGL.
//   ValheimWorldEditor.Desktop [--world <folder or name>] [--zone x,z] [--size n]
// Without --world it opens the world saved most recently on this computer, around zone 0,0.
// --bench <seconds>, --shot <file.png> and --report <file.txt> are for measuring (see Options).
public static class Program
{
	[STAThread]
	public static void Main(string[] args)
	{
		Options.Parse(args);
		AppDomain.CurrentDomain.UnhandledException += (_, e) => Options.Say($"crash: {e.ExceptionObject}");
		// Windows: ANGLE (OpenGL ES on Direct3D 11) first, the driver's own OpenGL if that fails.
		// VWE_TRACE=1: Avalonia's warnings on the console.
		if (Environment.GetEnvironmentVariable("VWE_TRACE") == "1")
		{
			System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
		}
		AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace(Avalonia.Logging.LogEventLevel.Warning)
			.With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Wgl, Win32RenderingMode.AngleEgl, Win32RenderingMode.Software } })
			.StartWithClassicDesktopLifetime(args);
	}
}

public sealed class App : Application
{
	public override void Initialize()
	{
		Styles.Add(new FluentTheme());
		// Number boxes without the theme's big up/down buttons (they squeezed the numbers out of
		// narrow panels); the arrow keys and the mouse wheel still step them.
		Styles.Add(new Avalonia.Styling.Style(x => x.OfType<Avalonia.Controls.NumericUpDown>())
		{
			Setters =
			{
				new Avalonia.Styling.Setter(Avalonia.Controls.NumericUpDown.ShowButtonSpinnerProperty, false),
				new Avalonia.Styling.Setter(Avalonia.Layout.Layoutable.MinWidthProperty, 64.0),
			},
		});
		RequestedThemeVariant = ThemeVariant.Dark;
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = new MainWindow();
		}
		base.OnFrameworkInitializationCompleted();
	}
}

// Command line options.
public static class Options
{
	public static string? World { get; private set; }
	// --world or --zone given: straight into the 3D editor (scripts), not the start page.
	public static bool Direct { get; private set; }
	// Open this world's map (by name or folder), without the start page (to check the map).
	public static string? MapWorld { get; private set; }
	// With --map: pick this zone (x,z) and open it in the 3D editor.
	public static (int X, int Z)? MapEdit { get; private set; }
	// With --map: look at this point (x,z, metres per pixel); search the world and go to the first result.
	public static (float X, float Z, float Mpp)? MapAt { get; private set; }
	public static string? Search { get; private set; }
	// With --map-edit: back to the map afterwards (its picture then, with --shot).
	public static bool MapBack { get; private set; }
	// A picture of the window's panels (Avalonia draws them; the 3D view stays empty), then quit.
	public static string? UiShot { get; private set; }
	public static int ZoneX { get; private set; }
	public static int ZoneZ { get; private set; }
	public static int Size { get; private set; } = 5;
	// For measuring: turn the camera on its own for this many seconds once loaded, print the frame
	// rates, then close; and/or save a picture of the view once everything is loaded (PNG).
	public static double Bench { get; private set; }
	public static string? Shot { get; private set; }
	// Where those results also go (a Windows window app has no console to print to).
	public static string? Report { get; private set; }
	// Close after this many seconds, saying where the camera ended up (to check input from a script).
	public static double QuitAfter { get; private set; }
	// Click this point of the view once loaded (fractions of its width and height), to check picking.
	public static (double X, double Y)? PickAt { get; private set; }
	// Start with every overlay and the measuring colours on, and/or walking or flying (to check them).
	public static bool AllOverlays { get; private set; }
	public static string? EyeStart { get; private set; }
	// One stroke of this brush (raise, lower, flatten, smooth, natural, restore, paintdirt, …) at the
	// middle of the view once loaded, to check sculpting (nothing is saved).
	public static BrushTool? StrokeTool { get; private set; }
	// Start in this tool (select, or a brush: raise, paintdirt…).
	public static string? StartTool { get; private set; }
	// Measure between these two points of the view (fractions of its width and height) once loaded.
	public static float[]? TapeAt { get; private set; }
	// Path tool: a line through these points of the view (fractions: x1,y1,x2,y2,…), then apply (nothing is saved).
	public static float[]? PathAt { get; private set; }
	// Click this point of the view (fractions) with the tool in use once loaded (--tool place…).
	public static (double X, double Y)? ClickAt { get; private set; }
	// With --click: only move the mouse there (to see what the tool would do).
	public static bool HoverOnly { get; private set; }
	// Move what --pick selected by this much (metres east, north), to check moving (nothing is saved).
	public static (float X, float Z)? MoveBy { get; private set; }

	public static void Say(string line)
	{
		Console.WriteLine(line);
		if (Report != null)
		{
			File.AppendAllText(Report, line + Environment.NewLine);
		}
	}

	public static void ClearShot() => Shot = null;
	public static void ClearBench() => Bench = 0;
	public static void QuitAfterDone() => QuitAfter = 0;

	public static void Parse(string[] args)
	{
		for (int i = 0; i < args.Length - 1; i++)
		{
			switch (args[i])
			{
				case "--world":
					World = args[++i];
					Direct = true;
					break;
				case "--map":
					MapWorld = args[++i];
					break;
				case "--map-at":
					var ma = args[++i].Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
					MapAt = (ma[0], ma[1], ma[2]);
					break;
				case "--ui-shot":
					UiShot = args[++i];
					break;
				case "--map-back":
					MapBack = true;
					break;
				case "--search":
					Search = args[++i];
					break;
				case "--map-edit":
					var me = args[++i].Split(',');
					MapEdit = (int.Parse(me[0]), int.Parse(me[1]));
					break;
				case "--zone":
					Direct = true;
					var p = args[++i].Split(',');
					ZoneX = int.Parse(p[0]);
					ZoneZ = int.Parse(p[1]);
					break;
				case "--size":
					Size = Math.Clamp(int.Parse(args[++i]), 1, 9);
					break;
				case "--bench":
					Bench = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
					break;
				case "--shot":
					Shot = Path.GetFullPath(args[++i]);
					break;
				case "--pick":
					var f = args[++i].Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
					PickAt = (f[0], f[1]);
					break;
				case "--overlays":
					AllOverlays = args[++i] != "off";
					break;
				case "--eye":
					EyeStart = args[++i];
					break;
				case "--quit-after":
					QuitAfter = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
					break;
				case "--stroke":
					StrokeTool = Enum.TryParse<BrushTool>(args[++i], ignoreCase: true, out var t) ? t : null;
					break;
				case "--tool":
					StartTool = args[++i];
					break;
				case "--tape":
					TapeAt = args[++i].Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
					break;
				case "--path":
					PathAt = args[++i].Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
					break;
				case "--hover-only":
					HoverOnly = true;
					break;
				case "--click":
					var cf = args[++i].Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
					ClickAt = (cf[0], cf[1]);
					break;
				case "--move":
					var m = args[++i].Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
					MoveBy = (m[0], m[1]);
					break;
				case "--report":
					Report = Path.GetFullPath(args[++i]);
					break;
			}
		}
	}
}
