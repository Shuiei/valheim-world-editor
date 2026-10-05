using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace TerrainEditor.App;

// The app around the editor: picks a free port, shows a native window (or the browser), runs the
// start page and the editor sessions in turn, and sets up the game's look in the background.
public static class AppHost
{
	public const string Version = "0.2.0";

	public static bool InWindow { get; private set; }

	private static SessionControl? _session;

	private static readonly string[] CliChecks = { "--verify", "--verify-ingame", "--selftest-save", "--inspect", "--summary" };

	public static async Task<int> Run(string[] args)
	{
		// Command-line checks of the world generator and saves: no window, no start page.
		if (args.Any(a => CliChecks.Contains(a)))
		{
			await EditorSession.RunAsync(args, new SessionControl());
			return 0;
		}
		Log.Start();
		bool browser = args.Contains("--browser");
		int port = Option(args, "--port") is string ps ? int.Parse(ps) : FreePort(5180);
		var settings = AppSettings.Load();
		string wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
		GameLook.Check(wwwroot, settings);

		// What to open first: a world given on the command line, live mode, or the start page.
		string[] rest = args.Where((a, i) => a != "--browser" && a != "--port" && !(i > 0 && args[i - 1] == "--port")).ToArray();
		string[]? first = rest.Length > 0 ? rest : null;
		using var quit = new CancellationTokenSource();
		Console.CancelKeyPress += (_, e) => { e.Cancel = true; Quit(quit); };
		// Logging out, shutting down or `kill` (SIGTERM / SIGHUP) also end the app cleanly.
		using var term = System.Runtime.InteropServices.PosixSignalRegistration.Create(System.Runtime.InteropServices.PosixSignal.SIGTERM, c => { c.Cancel = true; Quit(quit); });
		using var hup = System.Runtime.InteropServices.PosixSignalRegistration.Create(System.Runtime.InteropServices.PosixSignal.SIGHUP, c => { c.Cancel = true; Quit(quit); });
		string url = $"http://127.0.0.1:{port}/";
		var server = Task.Run(() => ServeAsync(first, port, settings, quit.Token));
		// The window must be made on this (main) thread, after the server listens.
		WaitForPort(port, TimeSpan.FromSeconds(60));

		InWindow = !browser;
		if (!browser && RunWindow(url, () => Quit(quit)))
		{
		}
		else
		{
			InWindow = false;
			Console.WriteLine($"Open {url} in your browser (Ctrl+C to stop).");
			OpenBrowser(url);
		}
		await server;
		GameLook.StopExport();
		return 0;
	}

	// WebView2 (Windows) needs a single-threaded apartment thread; WebKitGTK (Linux) the main thread.
	private static bool RunWindow(string url, Action closed)
	{
		string title = $"Valheim World Editor {Version}";
		if (!OperatingSystem.IsWindows())
		{
			return NativeWindow.TryRun(url, title, closed);
		}
		bool ok = false;
		var t = new Thread(() => ok = NativeWindow.TryRun(url, title, closed));
		t.SetApartmentState(ApartmentState.STA);
		t.Start();
		t.Join();
		return ok;
	}

	private static void Quit(CancellationTokenSource quit)
	{
		quit.Cancel();
		_session?.Stop.TrySetResult(SessionEnd.Exit);
		NativeWindow.Close();
	}

	private static async Task ServeAsync(string[]? first, int port, AppSettings settings, CancellationToken quit)
	{
		string[]? next = first;
		string? error = null;
		while (!quit.IsCancellationRequested)
		{
			if (next == null)
			{
				Launcher.Choice? choice = await Launcher.RunAsync(port, settings, error, quit);
				if (choice == null)
				{
					return;
				}
				next = choice.Args;
			}
			var ctl = new SessionControl
			{
				GameLookDir = GameLook.Dir,
				AddEndpoints = app =>
				{
					Launcher.Shared(app, settings);
					app.MapGet("/api/launcher/state", () => new { phase = "editor" });
					// Back to the start page (the page asks first when changes are not saved).
					app.MapPost("/api/app/worlds", () => { _session?.Stop.TrySetResult(SessionEnd.SwitchWorld); return Results.Ok(); });
				},
			};
			_session = ctl;
			var args = next.Concat(new[] { "--port", port.ToString() }).ToArray();
			SessionEnd end;
			try
			{
				end = await EditorSession.RunAsync(args, ctl);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex);
				ctl.Error = "The world could not be opened: " + ex.Message;
				end = SessionEnd.Failed;
			}
			_session = null;
			if (end == SessionEnd.Exit)
			{
				return;
			}
			error = end == SessionEnd.Failed ? ctl.Error ?? "The world could not be opened." : null;
			next = null;
		}
	}

	private static void WaitForPort(int port, TimeSpan max)
	{
		var sw = Stopwatch.StartNew();
		while (sw.Elapsed < max)
		{
			try
			{
				using var c = new TcpClient();
				c.Connect(IPAddress.Loopback, port);
				return;
			}
			catch (SocketException)
			{
				Thread.Sleep(150);
			}
		}
	}

	private static string? Option(string[] args, string name)
	{
		int i = Array.IndexOf(args, name);
		return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
	}

	// The first free port from `from` on, so a second copy (or another program) never blocks the start.
	private static int FreePort(int from)
	{
		for (int p = from; p < from + 50; p++)
		{
			try
			{
				var l = new TcpListener(IPAddress.Loopback, p);
				l.Start();
				l.Stop();
				return p;
			}
			catch (SocketException)
			{
			}
		}
		var any = new TcpListener(IPAddress.Loopback, 0);
		any.Start();
		int port = ((IPEndPoint)any.LocalEndpoint).Port;
		any.Stop();
		return port;
	}

	public static void OpenBrowser(string url)
	{
		// Automated tests run the editor without opening anything on the desktop.
		if (Environment.GetEnvironmentVariable("VWE_NO_OPEN") == "1")
		{
			return;
		}
		try
		{
			if (OperatingSystem.IsWindows())
			{
				Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
			}
			else
			{
				Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true });
			}
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine("Could not open the browser: " + ex.Message);
		}
	}
}

// Console output also goes to a log file in the data folder (a window app has no console on Windows).
public static class Log
{
	public static string FilePath => Path.Combine(AppSettings.DataDir, "log.txt");

	public static void Start()
	{
		try
		{
			var file = new StreamWriter(FilePath, append: false) { AutoFlush = true };
			Console.SetOut(new Tee(Console.Out, file));
			Console.SetError(new Tee(Console.Error, file));
		}
		catch
		{
		}
	}

	private sealed class Tee(TextWriter a, TextWriter b) : TextWriter
	{
		public override System.Text.Encoding Encoding => a.Encoding;

		public override void Write(char value)
		{
			try { a.Write(value); } catch { }
			try { b.Write(value); } catch { }
		}

		public override void Write(string? value)
		{
			try { a.Write(value); } catch { }
			try { b.Write(value); } catch { }
		}

		public override void WriteLine(string? value)
		{
			try { a.WriteLine(value); } catch { }
			try { b.WriteLine(value); } catch { }
		}
	}
}
