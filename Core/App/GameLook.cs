using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TerrainEditor.App;

// The game's own look (terrain shader and textures, map textures, models) is copied from the user's
// own Valheim install, never shipped: this finds the install, runs the bundled exporter
// (export-game-files/export_all.py with its own Python runtime) in the background, and redoes it
// after a game update. The files go to a per-user folder (Dir) that the editor reads them from.
public static class GameLook
{
	public const int ValheimAppId = 892970;

	private static readonly object Lock = new();

	private static Process? _process;

	private static readonly List<string> _log = new();

	public static string Dir => Path.Combine(AppSettings.UserDataDir, "game-look");

	// False: Steam is only looked for under the home folder (not /opt/Steam, /usr/share/steam or
	// Program Files): the editor driven by the tests and the documentation's pictures, whose stand-in
	// home must not lead to the computer's own game.
	public static bool SearchOutsideHome { get; set; } = true;

	private static string MarkerPath => Path.Combine(Dir, "game-look.json");

	// "ready", "missing" (not set up, Valheim not found), "running", "failed".
	public static string State { get; private set; } = "missing";

	public static string? Message { get; private set; }

	public static string? ValheimPath { get; private set; }

	// What the start page shows: the state, its message, the Valheim folder, the exporter's last line
	// and the progress (0..1, null when not known).
	public sealed record Snapshot(string State, string? Message, string? Valheim, string? LastLine, double? Progress);

	public static Snapshot Now()
	{
		lock (Lock)
		{
			string? last = _log.Count > 0 ? Regex.Replace(_log[^1], @"^\d\d:\d\d:\d\d ", "") : null;
			return new Snapshot(State, Message, ValheimPath, last, Progress());
		}
	}


	private sealed record Marker(string Valheim, string? BuildId, int Exporter, DateTime Made);

	// Bump when the exporter's output changes, so existing installs export again.
	// 2: runestone locations and tameable creatures get models.
	// 3: dungeon rooms get models (the Dungeon tool).
	private const int ExporterVersion = 3;

	// Files that only a complete export leaves behind.
	public static bool Present()
	{
		static bool Complete(string root) =>
			File.Exists(Path.Combine(root, "terrain", "heightmap.frag.glsl")) && File.Exists(Path.Combine(root, "maptex", "background.png"))
			&& File.Exists(Path.Combine(root, "models", "objects.json"));
		return Complete(Dir);
	}

	// At start: export when the files are missing, or when the game was updated since (export: false,
	// for the tests' editor: only say whether the files are there, never copy).
	public static void Check(AppSettings settings, bool export = true)
	{
		string? valheim = FindValheim(settings.ValheimPath);
		lock (Lock)
		{
			ValheimPath = valheim;
		}
		Marker? marker = null;
		try
		{
			marker = File.Exists(MarkerPath) ? JsonSerializer.Deserialize<Marker>(File.ReadAllText(MarkerPath)) : null;
		}
		catch
		{
		}
		ConvertShader();
		bool present = Present();
		if (valheim == null)
		{
			Set(present ? "ready" : "missing", present ? null : "Valheim was not found on this computer. Choose its folder to get the game's look.");
			return;
		}
		string? build = BuildId(valheim);
		if (present && marker != null && marker.Exporter == ExporterVersion && (build == null || marker.BuildId == build))
		{
			Set("ready", null);
			return;
		}
		if (!export)
		{
			Set(present ? "ready" : "missing", present ? null : "The game's look is not copied yet.");
			return;
		}
		string? why = !present ? null : marker != null && marker.Exporter != ExporterVersion
			? "This version of the editor draws more of the game: copying the new models (a minute or two)."
			: "Valheim was updated: refreshing the game's look.";
		Start(valheim, settings, why);
	}

	// Start (or restart) the export from this Valheim folder.
	public static string? Start(string valheim, AppSettings settings, string? why = null)
	{
		valheim = valheim.Trim().Trim('"');
		if (BundlesDir(valheim) == null)
		{
			return "That folder is not a Valheim game folder (it has no valheim_Data). Pick the folder Steam installed Valheim into.";
		}
		lock (Lock)
		{
			if (_process is { HasExited: false })
			{
				return null;
			}
			string? python = PythonPath(), script = ScriptPath();
			if (python == null || script == null)
			{
				Set("failed", "The game-look exporter is missing from this installation (export-game-files).");
				return null;
			}
			settings.ValheimPath = valheim;
			settings.Save();
			ValheimPath = valheim;
			_log.Clear();
			Directory.CreateDirectory(Dir);
			var psi = new ProcessStartInfo(python)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			foreach (string a in new[] { "-u", script, "--valheim", valheim, "--out", Dir, "--work", Path.Combine(AppSettings.UserDataDir, "export-cache") })
			{
				psi.ArgumentList.Add(a);
			}
			psi.Environment["PYTHONIOENCODING"] = "utf-8";
			var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
			p.OutputDataReceived += (_, e) => Add(e.Data);
			p.ErrorDataReceived += (_, e) => Add(e.Data);
			string build = BuildId(valheim) ?? "";
			p.Exited += (_, _) =>
			{
				lock (Lock)
				{
					Console.WriteLine($"game look: the exporter ended (exit code {p.ExitCode})");
					string? shaderProblem = null;
					if (p.ExitCode == 0)
					{
						shaderProblem = ConvertShader();
					}
					if (shaderProblem != null)
					{
						Set("failed", "Copying the game's look failed: " + shaderProblem);
					}
					else if (p.ExitCode == 0 && Present())
					{
						File.WriteAllText(MarkerPath, JsonSerializer.Serialize(new Marker(valheim, build, ExporterVersion, DateTime.Now)));
						Set("ready", "The game's look is ready.");
					}
					else
					{
						Set("failed", "Copying the game's look failed: " + (_log.LastOrDefault(l => l.Contains("Error") || l.StartsWith("No ") || l.Contains("exit")) ?? _log.LastOrDefault() ?? $"exit code {p.ExitCode}"));
					}
				}
			};
			Console.WriteLine($"game look: copying from {valheim} (Steam build {(build.Length > 0 ? build : "unknown")})");
			p.Start();
			p.BeginOutputReadLine();
			p.BeginErrorReadLine();
			_process = p;
			Set("running", why ?? "Copying the game's look from your Valheim install (a few minutes, only this once).");
			return null;
		}
	}

	private static void Add(string? line)
	{
		if (string.IsNullOrWhiteSpace(line))
		{
			return;
		}
		Console.WriteLine("game look: " + Regex.Replace(line, @"^\d\d:\d\d:\d\d ", ""));
		lock (Lock)
		{
			_log.Add(line);
			if (_log.Count > 400)
			{
				_log.RemoveRange(0, 200);
			}
		}
	}

	private static void Set(string state, string? message)
	{
		if (state != State || message != Message)
		{
			Console.WriteLine($"game look: {state}{(message == null ? "" : ": " + message)}");
		}
		State = state;
		Message = message;
	}

	// A copy from Valheim for Windows brings the terrain shader as SPIR-V (its Vulkan program): made
	// into GLSL here (TerrainShader). Null when done or not needed, else what went wrong.
	private static string? ConvertShader()
	{
		string terrain = Path.Combine(Dir, "terrain");
		if (File.Exists(Path.Combine(terrain, TerrainShader.GlslFile)) || !File.Exists(Path.Combine(terrain, TerrainShader.SpirvFile)))
		{
			return null;
		}
		try
		{
			TerrainShader.ConvertIn(terrain);
			Console.WriteLine("game look: terrain shader made from its Vulkan program");
			return null;
		}
		catch (Exception e)
		{
			Console.WriteLine($"game look: the terrain shader could not be made: {e}");
			return e.Message;
		}
	}

	// Rough progress 0..1 from the exporter's output: the bundle scan, then the models.
	private static double? Progress()
	{
		if (State != "running")
		{
			return State == "ready" ? 1 : null;
		}
		for (int i = _log.Count - 1; i >= 0; i--)
		{
			Match m = Regex.Match(_log[i], @"scan: (\d+)/(\d+) bundles");
			if (m.Success)
			{
				return 0.4 * int.Parse(m.Groups[1].Value) / Math.Max(1, int.Parse(m.Groups[2].Value));
			}
			m = Regex.Match(_log[i], @"^(\d+)/(\d+) ");
			if (m.Success)
			{
				return 0.45 + 0.5 * int.Parse(m.Groups[1].Value) / Math.Max(1, int.Parse(m.Groups[2].Value));
			}
			if (_log[i].Contains("terrain:") || _log[i].Contains("map:"))
			{
				return 0.42;
			}
		}
		return 0.02;
	}

	public static void StopExport()
	{
		lock (Lock)
		{
			try
			{
				if (_process is { HasExited: false })
				{
					_process.Kill(true);
				}
			}
			catch
			{
			}
		}
	}

	// ---- Finding things.

	private static string? ExportDir()
	{
		foreach (string d in new[] { Path.Combine(AppContext.BaseDirectory, "export-game-files"), Path.Combine(AppContext.BaseDirectory, "..", "tools", "asset-export") })
		{
			if (File.Exists(Path.Combine(d, "export_all.py")))
			{
				return Path.GetFullPath(d);
			}
		}
		return null;
	}

	private static string? ScriptPath() => ExportDir() is string d ? Path.Combine(d, "export_all.py") : null;

	// Mod managers unpack zips without Unix permissions: give the bundled Python back its run bit.
	private static void MakeRunnable(string path)
	{
		if (OperatingSystem.IsWindows())
		{
			return;
		}
		try
		{
			UnixFileMode mode = File.GetUnixFileMode(path);
			const UnixFileMode run = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
			if ((mode & UnixFileMode.UserExecute) == 0)
			{
				File.SetUnixFileMode(path, mode | run);
			}
		}
		catch (Exception e)
		{
			Console.WriteLine($"could not make {path} runnable: {e.Message}");
		}
	}

	// The bundled Python runtime, else one installed on the computer (development).
	private static string? PythonPath()
	{
		if (ExportDir() is string d)
		{
			foreach (string p in new[] { Path.Combine(d, "python", "python.exe"), Path.Combine(d, "python", "bin", "python3.12"), Path.Combine(d, "python", "bin", "python3") })
			{
				if (File.Exists(p))
				{
					MakeRunnable(p);
					return p;
				}
			}
		}
		foreach (string name in OperatingSystem.IsWindows() ? new[] { "python.exe", "py.exe" } : new[] { "python3" })
		{
			foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
			{
				string p = Path.Combine(dir, name);
				if (File.Exists(p))
				{
					return p;
				}
			}
		}
		return null;
	}

	public static string? BundlesDir(string valheim)
	{
		foreach (string sub in new[] { "valheim_Data", "Valheim_Data" })
		{
			string d = Path.Combine(valheim, sub, "StreamingAssets", "SoftRef", "Bundles");
			if (Directory.Exists(d))
			{
				return d;
			}
		}
		return null;
	}

	// The configured folder if it is valid, else Valheim in any Steam library on this computer.
	public static string? FindValheim(string? configured)
	{
		if (configured != null && BundlesDir(configured) != null)
		{
			return configured;
		}
		foreach (string lib in SteamLibraries())
		{
			string v = Path.Combine(lib, "steamapps", "common", "Valheim");
			if (BundlesDir(v) != null)
			{
				return v;
			}
		}
		return null;
	}

	private static readonly string[] SystemSteamRoots = { "/opt/Steam", "/usr/share/steam" };

	private static IEnumerable<string> SteamLibraries()
	{
		var roots = new List<string>();
		string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (OperatingSystem.IsWindows())
		{
			try
			{
				if (Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is string sp)
				{
					roots.Add(sp.Replace('/', '\\'));
				}
			}
			catch
			{
			}
			if (SearchOutsideHome)
			{
				roots.Add(@"C:\Program Files (x86)\Steam");
				roots.Add(@"C:\Program Files\Steam");
			}
		}
		else
		{
			roots.AddRange(new[]
			{
				Path.Combine(home, ".steam", "steam"), Path.Combine(home, ".local", "share", "Steam"), Path.Combine(home, ".steam", "root"),
				Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"),
			});
			if (SearchOutsideHome)
			{
				roots.AddRange(SystemSteamRoots);
			}
		}
		var seen = new HashSet<string>();
		foreach (string root in roots)
		{
			if (!Directory.Exists(root) || !seen.Add(Path.GetFullPath(root)))
			{
				continue;
			}
			yield return root;
			// Other libraries are listed in libraryfolders.vdf: "path"   "D:\\SteamLibrary"
			string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
			if (!File.Exists(vdf))
			{
				continue;
			}
			foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
			{
				string lib = m.Groups[1].Value.Replace(@"\\", @"\");
				if (Directory.Exists(lib) && seen.Add(Path.GetFullPath(lib)))
				{
					yield return lib;
				}
			}
		}
	}

	// Steam's build id of the installed game (changes with every update), or null.
	private static string? BuildId(string valheim)
	{
		try
		{
			string manifest = Path.Combine(valheim, "..", "..", $"appmanifest_{ValheimAppId}.acf");
			if (File.Exists(manifest))
			{
				Match m = Regex.Match(File.ReadAllText(manifest), "\"buildid\"\\s+\"(\\d+)\"");
				if (m.Success)
				{
					return m.Groups[1].Value;
				}
			}
			// No Steam manifest (copied install): the newest bundle file time stands in.
			string? b = BundlesDir(valheim);
			return b == null ? null : Directory.GetFiles(b).Select(File.GetLastWriteTimeUtc).DefaultIfEmpty().Max().Ticks.ToString();
		}
		catch
		{
			return null;
		}
	}
}
