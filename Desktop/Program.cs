using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace TerrainEditor.Desktop;

// The native editor: an Avalonia window, the 3D view and the map drawn with OpenGL. It opens on the
// start page; the options (see Options) open an area directly or let a program drive it.
public static class Program
{
	[STAThread]
	public static void Main(string[] args)
	{
		Options.Parse(args);
		// Everything said also goes to ValheimWorldEditor.log in the data folder (a window app has no console on Windows).
		TerrainEditor.App.Log.Start(BuildInfo.Version);
		AppDomain.CurrentDomain.UnhandledException += (_, e) => Options.Say($"crash: {e.ExceptionObject}");
		TaskScheduler.UnobservedTaskException += (_, e) => Options.Say($"error in a background task: {e.Exception}");
		// Logging out, shutting down or `kill` (SIGTERM, SIGHUP): the tunnel and the game-look copy stop,
		// then the app ends (changes not saved are lost, as when the computer turns off).
		using var term = System.Runtime.InteropServices.PosixSignalRegistration.Create(System.Runtime.InteropServices.PosixSignal.SIGTERM, Quit);
		using var hup = System.Runtime.InteropServices.PosixSignalRegistration.Create(System.Runtime.InteropServices.PosixSignal.SIGHUP, Quit);
		// Windows: the graphics driver's own OpenGL (WGL) first, ANGLE (OpenGL ES on Direct3D 11) when
		// that fails, drawing in software last.
		// VWE_TRACE=1: Avalonia's warnings on the console.
		if (Environment.GetEnvironmentVariable("VWE_TRACE") == "1")
		{
			System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
		}
		AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace(Avalonia.Logging.LogEventLevel.Warning)
			.With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Wgl, Win32RenderingMode.AngleEgl, Win32RenderingMode.Software } })
			// Linux: Avalonia turns OpenGL off for software renderers (Mesa's llvmpipe, VMware's
			// SVGA3D), where its own drawing is faster without it; the 3D view and the map need
			// OpenGL, so they were blank in virtual machines and without GPU drivers.
			.With(new X11PlatformOptions { GlxRendererBlacklist = new List<string>() })
			.StartWithClassicDesktopLifetime(args);
	}

	private static void Quit(System.Runtime.InteropServices.PosixSignalContext c)
	{
		c.Cancel = true;
		Options.Say($"quit: {c.Signal}");
		TerrainEditor.App.Tunnel.Close();
		TerrainEditor.App.GameLook.StopExport();
		Avalonia.Threading.Dispatcher.UIThread.Post(() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown());
	}
}

public sealed class App : Application
{
	public override void Initialize()
	{
		Styles.Add(new FluentTheme());
		Ui.Apply(this);
		RequestedThemeVariant = ThemeVariant.Dark;
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			var window = new MainWindow();
			desktop.MainWindow = window;
			// An error in a button's or a key's handler would close the app and lose what is not saved:
			// it is logged and shown instead.
			Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
			{
				Options.Say($"error: {e.Exception}");
				e.Handled = true;
				window.ShowError(e.Exception);
			};
		}
		base.OnFrameworkInitializationCompleted();
	}
}

// Command line options.
//   <world folder>                                     that world's map
//   --live <bridge url> --token <token>                a live game's map (the token also from WORLD_BRIDGE_TOKEN)
//   --world <folder or name> [--zone x,z] [--size n]   straight into the 3D editor with that area
//   --driver                                           driven by another program (see Driver)
//   --window <width>x<height>                          the window's size (the documentation's pictures)
//   --data <folder>                                    settings and memory kept there (tests)
public static class Options
{
	public static string? World { get; private set; }
	// --world or --zone given: straight into the 3D editor, not the start page.
	public static bool Direct { get; private set; }
	public static int ZoneX { get; private set; }
	public static int ZoneZ { get; private set; }
	public static int Size { get; private set; } = 5;
	// Driven by another program, one command per line (see Driver).
	public static bool Driver { get; private set; }
	// --data <folder>: settings, saved servers, Place tool memory and stamps go there instead of the
	// user's (the visual tests). The game's look is still read from the usual place.
	public static string? Data { get; private set; }

	// <world folder>: its map, as if picked on the start page.
	public static string? Folder { get; private set; }
	// --live <url> --token <token>: the game's world, as if connected on the start page.
	public static string? LiveUrl { get; private set; }
	public static string? LiveToken { get; private set; }

	// --window <w>x<h>: the window's size (0: the usual).
	public static int WindowWidth { get; private set; }
	public static int WindowHeight { get; private set; }

	public static void Say(string line) => Console.WriteLine(line);

	public static void Parse(string[] args)
	{
		// Each start from nothing (the tests parse several lines in one process).
		Folder = LiveUrl = World = Data = null;
		Direct = Driver = false;
		ZoneX = ZoneZ = 0;
		Size = 5;
		WindowWidth = WindowHeight = 0;
		LiveToken = Environment.GetEnvironmentVariable("WORLD_BRIDGE_TOKEN");
		// Every argument, the last too (a switch such as --driver or --map-back can come last).
		for (int i = 0; i < args.Length; i++)
		{
			switch (args[i])
			{
				case "--world":
					World = args[++i];
					Direct = true;
					break;
				case "--driver":
					Driver = true;
					// Driven by the tests or the documentation: never the computer's own game outside the home.
					TerrainEditor.App.GameLook.SearchOutsideHome = false;
					break;
				case "--data":
					Data = Path.GetFullPath(args[++i]);
					Directory.CreateDirectory(Data);
					TerrainEditor.App.AppSettings.PathOverride = Path.Combine(Data, "settings.json");
					TerrainEditor.App.ServerConfig.PathOverride = Path.Combine(Data, "servers.cfg");
					PlaceMemory.PathOverride = Path.Combine(Data, "place.json");
					Stamps.PathOverride = Path.Combine(Data, "stamps.json");
					TerrainEditor.App.AppSettings.DataDirOverride = Data;
					// The tests' editor: "My game" never finds the player's own profiles.
					TerrainEditor.App.LocalGame.SearchDefaultPlaces = false;
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
				case "--window":
				{
					var wh = args[++i].Split('x');
					WindowWidth = Math.Clamp(int.Parse(wh[0]), 640, 7680);
					WindowHeight = Math.Clamp(int.Parse(wh[1]), 480, 4320);
					break;
				}
				case "--live":
					LiveUrl = args[++i];
					break;
				case "--token":
					LiveToken = args[++i];
					break;
				default:
					if (!args[i].StartsWith("--"))
					{
						Folder ??= args[i];
					}
					break;
			}
		}
	}
}
