using System.Text.RegularExpressions;

namespace TerrainEditor.App;

// The start page (wwwroot/start.html): the worlds found on this computer, a folder of your own, or a
// running server (live mode). Serves until a choice is made, then hands back the editor's arguments.
public static class Launcher
{
	public sealed record Choice(string[] Args, string Label);

	public sealed record OpenRequest(string? Path, string? LiveUrl, string? Token);

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
		app.MapGet("/api/launcher/state", () => new { phase = "start", error = lastError, live = settings.LiveUrl });
		app.MapGet("/api/launcher/worlds", () => FindWorlds(settings));
		app.MapPost("/api/launcher/open", async (OpenRequest req) =>
		{
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
				settings.Save();
				chosen.TrySetResult(new Choice(new[] { "--live", url, "--token", req.Token?.Trim() ?? "" }, "live " + url));
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
			settings.AddRecent(Path.GetFullPath(path), WorldName(path));
			chosen.TrySetResult(new Choice(new[] { Path.GetFullPath(path) }, WorldName(path)));
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
		app.MapGet("/api/app", () => new { version = AppHost.Version, window = AppHost.InWindow, dataDir = AppSettings.DataDir });
	}

	// ---- Worlds on this computer.

	public sealed record WorldInfo(string Name, string Path, DateTime Saved, int SaveNumber, string Where, bool Usable, string? Problem);

	public static List<WorldInfo> FindWorlds(AppSettings settings)
	{
		var list = new List<WorldInfo>();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		void Add(string dir, string where)
		{
			try
			{
				string full = Path.GetFullPath(dir);
				if (!Directory.Exists(full) || !seen.Add(full))
				{
					return;
				}
				string? problem = CheckWorld(full);
				(int n, DateTime t) = LatestSave(full);
				list.Add(new WorldInfo(WorldName(full), full, t, n, where, problem == null, problem));
			}
			catch
			{
			}
		}
		foreach (var r in settings.Recent)
		{
			Add(r.Path, "recent");
		}
		foreach (string root in WorldRoots())
		{
			if (!Directory.Exists(root))
			{
				continue;
			}
			foreach (string d in Directory.GetDirectories(root))
			{
				if (Directory.GetFiles(d, "_main.*.chunks").Length > 0)
				{
					Add(d, "local");
				}
			}
			// Old single-file worlds (<World>.db): the game converts them when it next loads them.
			foreach (string db in Directory.GetFiles(root, "*.db"))
			{
				string name = Path.GetFileNameWithoutExtension(db);
				if (!Directory.Exists(Path.Combine(root, name)) && seen.Add(db))
				{
					list.Add(new WorldInfo(name, db, File.GetLastWriteTime(db), 0, "local", false, "Old save format: load this world in Valheim once, then it can be edited here."));
				}
			}
		}
		return list.OrderByDescending(w => w.Where == "recent").ThenByDescending(w => w.Saved).ToList();
	}

	// Where Valheim keeps local worlds.
	public static IEnumerable<string> WorldRoots()
	{
		string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (OperatingSystem.IsWindows())
		{
			yield return Path.Combine(home, "AppData", "LocalLow", "IronGate", "Valheim", "worlds_local");
			yield return Path.Combine(home, "AppData", "LocalLow", "IronGate", "Valheim", "worlds");
		}
		else
		{
			yield return Path.Combine(home, ".config", "unity3d", "IronGate", "Valheim", "worlds_local");
			yield return Path.Combine(home, ".config", "unity3d", "IronGate", "Valheim", "worlds");
			// Valheim through Proton (Steam Play) keeps its Windows-style folder inside the prefix.
			foreach (string steam in new[] { Path.Combine(home, ".steam", "steam"), Path.Combine(home, ".local", "share", "Steam") })
			{
				yield return Path.Combine(steam, "steamapps", "compatdata", GameLook.ValheimAppId.ToString(), "pfx", "drive_c", "users", "steamuser", "AppData", "LocalLow", "IronGate", "Valheim", "worlds_local");
			}
		}
	}

	public static string? CheckWorld(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return "Enter the world's folder.";
		}
		if (File.Exists(path) && path.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
		{
			return "That is an old single-file world: load it in Valheim once, then open its folder here.";
		}
		if (!Directory.Exists(path))
		{
			return "That folder does not exist.";
		}
		if (Directory.GetFiles(path, "_main.*.chunks").Length == 0)
		{
			// A worlds_local folder with one world in it: point at the world instead.
			string[] inside = Directory.GetDirectories(path).Where(d => Directory.GetFiles(d, "_main.*.chunks").Length > 0).ToArray();
			return inside.Length > 0
				? $"That folder holds worlds; pick one of them ({string.Join(", ", inside.Select(System.IO.Path.GetFileName).Take(4))})."
				: "No Valheim world in that folder (a world folder holds _main.<n>.chunks and *.chunk files).";
		}
		return null;
	}

	private static string WorldName(string path)
	{
		// The name is in _main.<n>.fwl2; the folder is named after the world anyway.
		return Path.GetFileName(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
	}

	private static (int, DateTime) LatestSave(string dir)
	{
		int best = 0;
		DateTime t = DateTime.MinValue;
		foreach (string f in Directory.GetFiles(dir, "_main.*.chunks"))
		{
			Match m = Regex.Match(Path.GetFileName(f), @"_main\.(\d+)\.chunks");
			if (m.Success && int.Parse(m.Groups[1].Value) >= best)
			{
				best = int.Parse(m.Groups[1].Value);
				t = File.GetLastWriteTime(f);
			}
		}
		return (best, t);
	}
}
