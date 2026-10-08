using System.Text.RegularExpressions;

namespace TerrainEditor.App;

// The start page (wwwroot/start.html): the worlds found on this computer, a folder of your own, or a
// running server (live mode). Serves until a choice is made, then hands back the editor's arguments.
public static class Launcher
{
	private static readonly object PerfLogLock = new();
	public static string PerfLogPath() => Path.Combine(AppSettings.DataDir, AppHost.InWindow ? "perf-window.log" : "perf-browser.log");

	public sealed record Choice(string[] Args, string Label);

	// mode: "game" (live, own game), "server" (live, built-in SSH tunnel), "saved" (a saved server),
	// "url" (live, a tunnel made elsewhere: LiveUrl + Token), or a world Path (offline).
	public sealed record SettingsRequest(string? Valheim, List<string>? BepInEx, List<string>? Worlds);

	private static string Expand(string path)
	{
		path = path.Trim().Trim('"');
		if (path.StartsWith('~'))
		{
			path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];
		}
		return Path.GetFullPath(path);
	}

	public sealed record OpenRequest(string? Path, string? LiveUrl, string? Token, string? Mode = null, string? Host = null, int SshPort = 22,
		string? User = null, string? Password = null, string? KeyPath = null, string? Passphrase = null, bool SavePassword = false, string? Name = null, string? Id = null, int? BridgePort = null, string? GameFolder = null);

	public static async Task<Choice?> RunAsync(int port, AppSettings settings, string? lastError, CancellationToken quit)
	{
		var builder = WebApplication.CreateBuilder(new WebApplicationOptions
		{
			ContentRootPath = AppContext.BaseDirectory,
			WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
		});
		builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
		builder.Logging.SetMinimumLevel(LogLevel.Warning);
		var app = builder.Build();
		var chosen = new TaskCompletionSource<Choice?>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var reg = quit.Register(() => chosen.TrySetResult(null));

		app.Use(async (ctx, next) =>
		{
			ctx.Response.Headers.CacheControl = "no-cache";
			// Every page leads to the start page until a world is open.
			string p = ctx.Request.Path.Value ?? "/";
			if (p == "/" || p.EndsWith(".html", StringComparison.Ordinal) && p != "/start.html")
			{
				ctx.Request.Path = "/start.html";
			}
			await next();
		});
		app.UseStaticFiles();
		Shared(app, settings);
		app.MapGet("/api/launcher/state", () => new { phase = "start", error = lastError, live = settings.LiveUrl, mode = settings.LastMode });
		app.MapGet("/api/launcher/worlds", () => FindWorlds(settings));
		app.MapGet("/api/settings", () => new
		{
			valheim = settings.ValheimPath,
			detected = GameLook.FindValheim(null),
			bepInEx = settings.BepInExFolders,
			worlds = settings.WorldFolders,
			defaultWorlds = WorldRoots().Where(Directory.Exists),
		});
		app.MapPost("/api/settings", (SettingsRequest req) =>
		{
			var problems = new List<string>();
			string? valheim = string.IsNullOrWhiteSpace(req.Valheim) ? null : Expand(req.Valheim);
			if (valheim != null && GameLook.BundlesDir(valheim) == null)
			{
				problems.Add("The Valheim folder must be the game's folder, the one with valheim_Data.");
			}
			List<string> Clean(IEnumerable<string>? list, string what) => (list ?? Enumerable.Empty<string>()).Select(p => p.Trim()).Where(p => p.Length > 0).Select(Expand).Distinct()
				.Where(p => { if (Directory.Exists(p)) return true; problems.Add($"{what} not found: {p}"); return false; }).ToList();
			var bep = Clean(req.BepInEx, "BepInEx folder");
			var worlds = Clean(req.Worlds, "World folder");
			if (problems.Count > 0)
			{
				return Results.Ok(new { ok = false, error = string.Join(" ", problems) });
			}
			bool gameChanged = valheim != settings.ValheimPath;
			settings.ValheimPath = valheim;
			settings.BepInExFolders = bep;
			settings.WorldFolders = worlds;
			settings.Save();
			if (gameChanged)
			{
				GameLook.Check(Path.Combine(AppContext.BaseDirectory, "wwwroot"), settings);
			}
			return Results.Ok(new { ok = true });
		});
		app.MapGet("/api/launcher/local", async () => await LocalGame.Status(settings));
		app.MapGet("/api/launcher/servers", () => ServerConfig.Load().Select(s => new { id = s.Id, name = s.Name, host = s.Host, user = s.User, sshPort = s.SshPort, keyFile = s.KeyFile, hasPassword = s.Password != null, hasToken = s.Token != null, gameFolder = s.GameFolder }));
		app.MapPost("/api/launcher/forget", (OpenRequest req) => { if (req.Id != null) ServerConfig.Forget(req.Id); return Results.Ok(new { ok = true }); });
		app.MapPost("/api/launcher/open", async (OpenRequest req) =>
		{
			Choice Live(string url, string token, string label, string mode)
			{
				settings.LastMode = mode;
				settings.Save();
				return new Choice(new[] { "--live", url, "--token", token }, label);
			}
			if (req.Mode == "game")
			{
				List<LocalGame.Bridge> bridges = LocalGame.FindBridges(settings, out _, out _);
				LocalGame.Running? run = await LocalGame.FindRunning(bridges);
				if (run == null)
				{
					return Results.Ok(new { ok = false, error = "Valheim is not running with the WorldEditorBridge plugin (or no world is loaded yet). Start the game, load your world, then try again." });
				}
				chosen.TrySetResult(Live($"http://127.0.0.1:{run.Bridge.Port}", run.Bridge.Token, "my game", "game"));
				return Results.Ok(new { ok = true });
			}
			if (req.Mode == "server" && string.IsNullOrWhiteSpace(req.Token))
			{
				return Results.Ok(new { ok = false, error = "Enter the plugin's token: the Token line in BepInEx/config/Tie.WorldEditorBridge.cfg on the server." });
			}
			if (req.Mode is "server" or "saved")
			{
				ServerConfig.Server? s = req.Mode == "saved" ? ServerConfig.Load().FirstOrDefault(x => x.Id == req.Id) : null;
				if (req.Mode == "saved" && s == null)
				{
					return Results.Ok(new { ok = false, error = "That saved server is gone from servers.cfg." });
				}
				var t = s != null
					? new Tunnel.Request(s.Host, s.SshPort, s.User, req.Password ?? s.Password, s.KeyFile, req.Passphrase, req.Token, null, s.Password != null || req.SavePassword, s.Name, s.GameFolder)
					: new Tunnel.Request(req.Host ?? "", req.SshPort, req.User ?? "", req.Password, req.KeyPath, req.Passphrase, req.Token, req.BridgePort, req.SavePassword, req.Name, req.GameFolder);
				Tunnel.Result tr = await Tunnel.Start(t);
				if (tr.Error != null)
				{
					return Results.Ok(new { ok = false, error = tr.Error, needPassword = s != null && s.Password == null && s.KeyFile == null });
				}
				string url = $"http://127.0.0.1:{tr.LocalPort}";
				string? down = await TerrainEditor.Save.LiveBridge.Check(url, tr.Token, TimeSpan.FromSeconds(10));
				if (down != null)
				{
					string? why = down.Contains("refused the token") ? null : Tunnel.Diagnose(t.GameFolder ?? ServerConfig.Load().FirstOrDefault(x => x.Id == $"{t.User}@{t.Host}:{(t.Port > 0 ? t.Port : 22)}")?.GameFolder);
					Tunnel.Close();
					return Results.Ok(new { ok = false, error = down.Contains("refused the token") ? down : why ?? "The tunnel is open, but the plugin does not answer on the server: is the server running with BepInEx and WorldEditorBridge? Set the server's Valheim folder under \"More options\" for a precise check." });
				}
				// Saved only after a successful connection.
				if (tr.Server != null)
				{
					ServerConfig.Remember(tr.Server);
				}
				chosen.TrySetResult(Live(url, tr.Token, "server " + t.Host, "server"));
				return Results.Ok(new { ok = true });
			}
			if (!string.IsNullOrWhiteSpace(req.LiveUrl))
			{
				string url = req.LiveUrl.Trim();
				if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
				{
					url = "http://" + url;
				}
				// Check that the bridge answers before leaving the start page, with a short timeout.
				string? unreachable = await TerrainEditor.Save.LiveBridge.Check(url, req.Token?.Trim() ?? "", TimeSpan.FromSeconds(10));
				if (unreachable != null)
				{
					return Results.Ok(new { ok = false, error = unreachable });
				}
				settings.LiveUrl = url;
				chosen.TrySetResult(Live(url, req.Token?.Trim() ?? "", "live " + url, "server"));
				return Results.Ok(new { ok = true });
			}
			string path = (req.Path ?? "").Trim().Trim('"');
			if (path.StartsWith("~"))
			{
				path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];
			}
			string? problem = CheckWorld(path);
			if (problem != null)
			{
				return Results.Ok(new { ok = false, error = problem });
			}
			settings.LastMode = "offline";
			settings.AddRecent(Path.GetFullPath(path), Worlds.WorldName(path));
			chosen.TrySetResult(new Choice(new[] { Path.GetFullPath(path) }, Worlds.WorldName(path)));
			return Results.Ok(new { ok = true });
		});
		await app.StartAsync();
		Console.WriteLine($"Start page at http://127.0.0.1:{port}");
		Choice? result = await chosen.Task;
		// Let the browser receive the answer before the server goes away.
		await Task.Delay(300);
		await app.StopAsync();
		await app.DisposeAsync();
		return result;
	}

	// Endpoints both the start page and the editor have: the game-look setup and the app's own actions.
	public static void Shared(WebApplication app, AppSettings settings)
	{
		app.MapGet("/api/gamelook", () => GameLook.Status());
		app.MapPost("/api/gamelook/start", (OpenRequest req) =>
		{
			string? folder = string.IsNullOrWhiteSpace(req.Path) ? GameLook.ValheimPath ?? settings.ValheimPath : req.Path;
			if (folder == null)
			{
				return Results.Ok(new { ok = false, error = "Choose the Valheim game folder first." });
			}
			string? problem = GameLook.Start(folder, settings);
			return Results.Ok(new { ok = problem == null, error = problem });
		});
		app.MapGet("/api/app", () => new { version = AppHost.Version, window = AppHost.InWindow, dataDir = AppSettings.DataDir, perfLog = PerfLogPath() });
		// Frame rates measured by the 3D editor (a sample every 0.2 s while the view is used, sent in
		// batches), in the data folder: perf-window.log in the app's window, perf-browser.log in a browser
		// (--browser), to compare the two. Header lines (# ...) get the date and the version.
		app.MapPost("/api/perflog", async (HttpRequest req) =>
		{
			using var reader = new StreamReader(req.Body);
			string body = await reader.ReadToEndAsync();
			if (body.Length == 0 || body.Length > 200_000) return Results.BadRequest();
			string path = PerfLogPath();
			string mode = AppHost.InWindow ? "app window" : "browser (--browser)";
			var text = new System.Text.StringBuilder();
			foreach (string line in body.Split('\n'))
			{
				string l = line.Trim();
				if (l.Length == 0) continue;
				text.AppendLine(l.StartsWith('#') ? $"# {DateTime.Now:yyyy-MM-dd HH:mm:ss}  v{AppHost.Version}  {mode}  {l[1..].Trim()}" : l);
			}
			lock (PerfLogLock)
			{
				// Kept small: past 5 MB the old lines move to <name>.old.
				if (File.Exists(path) && new FileInfo(path).Length > 5_000_000) File.Move(path, path + ".old", overwrite: true);
				File.AppendAllText(path, text.ToString());
			}
			return Results.Ok(new { path });
		});
	}

	// ---- Worlds on this computer.

	public sealed record WorldInfo(string Name, string Path, DateTime Saved, int SaveNumber, string Where, bool Usable, string? Problem);

	public static List<WorldInfo> FindWorlds(AppSettings settings) => Worlds.Find(settings).Select(w => new WorldInfo(w.Name, w.Path, w.Saved, w.SaveNumber, w.Where, w.Usable, w.Problem)).ToList();

	public static IEnumerable<string> WorldRoots() => Places.WorldRoots();

	public static string? CheckWorld(string path) => Worlds.Check(path);
}
