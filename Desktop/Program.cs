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
		Ui.Apply(this);
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
//   --world <folder or name> [--zone x,z] [--size n]   straight into the 3D editor with that area
//   --driver                                           driven by another program (see Driver)
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

	public static void Say(string line) => Console.WriteLine(line);

	public static void Parse(string[] args)
	{
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
					break;
				case "--data":
					Data = Path.GetFullPath(args[++i]);
					Directory.CreateDirectory(Data);
					TerrainEditor.App.AppSettings.PathOverride = Path.Combine(Data, "settings.json");
					TerrainEditor.App.ServerConfig.PathOverride = Path.Combine(Data, "servers.cfg");
					PlaceMemory.PathOverride = Path.Combine(Data, "place.json");
					Stamps.PathOverride = Path.Combine(Data, "stamps.json");
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
			}
		}
	}
}
