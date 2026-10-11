using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SkiaSharp;

namespace TerrainEditor.DocsScreenshots;

// Drives the app for the documentation's pictures (its --driver mode, see Desktop/Driver.cs).
//
// The app runs with a stand-in home: none of this computer's characters, worlds, servers or settings
// show in the pictures. Only the copied game look is shared (read only), so the pictures have the
// game's textures and models. Nothing is ever saved: each scene's changes stay pending and are
// discarded with its editor.
public sealed class Editor : IDisposable
{
	// The repository: the folder above this program that holds VERSION and Desktop.
	public static readonly string Repo = FindRepo();

	private readonly string _home;
	private readonly int _width;
	private readonly FakeGame? _game;
	private readonly Process _p;

	public Editor(string? app = null, int width = 1440, int height = 900, string? gameWorld = null,
		IReadOnlyList<string>? worlds = null, string? homestead = null, IReadOnlyList<string>? recent = null)
	{
		_home = Directory.CreateTempSubdirectory("vwe-docs-home-").FullName;
		_width = width;
		// The stand-in home's own data folder (~/.local/share/ValheimWorldEditor): the start page shows
		// it as such. The index of the game's files and its terrain modifiers are the real ones (made again
		// if stale).
		string data = Path.Combine(_home, ".local", "share");
		string ours = Path.Combine(data, "ValheimWorldEditor");
		Directory.CreateDirectory(ours);
		foreach (string kept in new[] { "game-index.json", "game-terrain-modifiers.json" })
		{
			string mine = Path.Combine(UserHome, ".local", "share", "ValheimWorldEditor", kept);
			if (File.Exists(mine))
			{
				File.CreateSymbolicLink(Path.Combine(ours, kept), mine);
			}
		}
		// A stand-in Valheim in the home's Steam library ("Found automatically" in Settings); the app is
		// driven, so it never looks for Steam outside this home. Its bundles are this computer's
		// Valheim's (read only), for the game's look.
		string valheim = Path.Combine(_home, ".local", "share", "Steam", "steamapps", "common", "Valheim");
		string bundles = Path.Combine(valheim, "valheim_Data", "StreamingAssets", "SoftRef", "Bundles");
		Directory.CreateDirectory(Path.GetDirectoryName(bundles)!);
		if (RealBundles() is string real)
		{
			Directory.CreateSymbolicLink(bundles, real);
		}
		else
		{
			Directory.CreateDirectory(bundles);
		}
		// Worlds in Valheim's own folder, as a player has them (links to the read-only originals).
		string local = Path.Combine(_home, ".config", "unity3d", "IronGate", "Valheim", "worlds_local");
		foreach (string w in worlds ?? [])
		{
			Directory.CreateDirectory(local);
			string original = Path.GetFullPath(w);
			string link = Path.Combine(local, Path.GetFileName(original));
			if (Directory.Exists(original))
			{
				Directory.CreateSymbolicLink(link, original);
			}
			else
			{
				File.CreateSymbolicLink(link, original);
			}
		}
		if (recent is { Count: > 0 })
		{
			// Worlds opened before (the start page's Home), the first a few hours ago, then a day apart.
			var now = DateTime.Now;
			var opened = recent.Select((w, i) => new
			{
				Path = Path.Combine(local, Path.GetFileName(Path.GetFullPath(w))),
				Name = Path.GetFileName(Path.GetFullPath(w)),
				Opened = (now - TimeSpan.FromHours(3) - TimeSpan.FromDays(i)).ToString("yyyy-MM-ddTHH:mm:ss.ffffff"),
			});
			File.WriteAllText(Path.Combine(ours, "settings.json"), JsonSerializer.Serialize(new { Recent = opened }));
		}
		string bep = Path.Combine(_home, ".config", "r2modmanPlus-local", "Valheim", "profiles", "Default", "BepInEx");
		if (gameWorld != null)
		{
			// The plugin in an r2modman profile, as on many players' computers.
			_game = new FakeGame(gameWorld);
			Directory.CreateDirectory(Path.Combine(bep, "plugins"));
			File.WriteAllBytes(Path.Combine(bep, "plugins", "WorldEditorBridge.dll"), []);
			Directory.CreateDirectory(Path.Combine(bep, "config"));
			File.WriteAllText(Path.Combine(bep, "config", "Tie.WorldEditorBridge.cfg"), $"[Bridge]\nPort = {_game.Port}\nToken = {_game.Token}\n");
		}
		if (homestead != null)
		{
			// Homestead (a copy of its Homestead.dll: the app shows its version), in the stand-in game's
			// BepInEx, or with the plugin in its mod manager profile.
			string plugins = Path.Combine(gameWorld != null ? bep : Path.Combine(valheim, "BepInEx"), "plugins");
			Directory.CreateDirectory(plugins);
			File.Copy(homestead, Path.Combine(plugins, "Homestead.dll"), overwrite: true);
		}
		app ??= Path.Combine(Repo, "Desktop", "bin", "Debug", "net10.0", "ValheimWorldEditor.dll");
		bool dll = app.EndsWith(".dll", StringComparison.Ordinal);
		var start = new ProcessStartInfo(dll ? "dotnet" : app)
		{
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		if (dll)
		{
			start.ArgumentList.Add(app);
		}
		foreach (string a in new[] { "--driver", "--window", $"{width}x{height}" })
		{
			start.ArgumentList.Add(a);
		}
		start.Environment["HOME"] = _home;
		start.Environment["XDG_DATA_HOME"] = data;
		start.Environment["XDG_CONFIG_HOME"] = Path.Combine(_home, ".config");
		_p = Process.Start(start) ?? throw new InvalidOperationException("the editor did not start");
		// Its error output is read and dropped (unread, a full pipe would block it).
		_p.ErrorDataReceived += (_, _) => { };
		_p.BeginErrorReadLine();
		if (Answer() != "@@ ready")
		{
			throw new InvalidOperationException("the editor did not start");
		}
	}

	// The real user's home (the stand-in one is only the app's).
	private static string UserHome => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

	private string Answer()
	{
		while (true)
		{
			string line = _p.StandardOutput.ReadLine() ?? throw new InvalidOperationException("the editor stopped");
			if (line.StartsWith("@@ ", StringComparison.Ordinal))
			{
				return line;
			}
		}
	}

	// One command; its answer's state (an error answer throws).
	public JsonElement Send(string command)
	{
		_p.StandardInput.WriteLine(command);
		_p.StandardInput.Flush();
		string answer = Answer();
		if (!answer.StartsWith("@@ ok ", StringComparison.Ordinal))
		{
			throw new InvalidOperationException($"{command}: {answer}");
		}
		using var doc = JsonDocument.Parse(answer["@@ ok ".Length..]);
		return doc.RootElement.Clone();
	}

	// The whole window, saved as a JPEG for the docs (the status bar's message cleared unless kept: it
	// would tell how the scene was set up).
	public JsonElement Shot(string path, int quality = 85, bool keepMessage = false)
	{
		if (!keepMessage)
		{
			Send("message");
		}
		string png = Path.Combine(_home, "shot.png");
		var state = Send($"shot {png}");
		using var shot = SKBitmap.Decode(png) ?? throw new InvalidOperationException($"{png}: not a picture");
		var img = shot;
		// At the window's own size (the screen's scaling makes the snapshot bigger).
		if (img.Width > _width)
		{
			var size = new SKImageInfo(_width, (int)Math.Round(img.Height * (double)_width / img.Width), SKColorType.Rgba8888, SKAlphaType.Opaque);
			img = shot.Resize(size, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
				?? throw new InvalidOperationException($"{png}: could not be resized");
		}
		try
		{
			using var jpeg = img.Encode(SKEncodedImageFormat.Jpeg, quality);
			using var file = File.Create(path);
			jpeg.SaveTo(file);
		}
		finally
		{
			if (img != shot)
			{
				img.Dispose();
			}
		}
		return state;
	}

	public void Dispose()
	{
		try
		{
			_p.StandardInput.WriteLine("quit");
			_p.StandardInput.Flush();
			if (!_p.WaitForExit(15000))
			{
				_p.Kill();
			}
		}
		catch (Exception ex) when (ex is IOException or InvalidOperationException)
		{
			_p.Kill();
		}
		_p.Dispose();
		_game?.Dispose();
		try
		{
			Directory.Delete(_home, recursive: true);
		}
		catch (IOException)
		{
		}
	}

	// This computer's Valheim bundles folder (the app's settings first, then the usual Steam places).
	private static string? RealBundles()
	{
		var places = new List<string>();
		try
		{
			string settings = Path.Combine(UserHome, ".local", "share", "ValheimWorldEditor", "settings.json");
			using var doc = JsonDocument.Parse(File.ReadAllText(settings));
			if (doc.RootElement.TryGetProperty("ValheimPath", out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } path)
			{
				places.Add(path);
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
		{
		}
		foreach (string steam in new[] { Path.Combine(UserHome, ".local", "share", "Steam"), Path.Combine(UserHome, ".steam", "steam"), "/opt/Steam" })
		{
			places.Add(Path.Combine(steam, "steamapps", "common", "Valheim"));
		}
		foreach (string v in places)
		{
			var b = new DirectoryInfo(Path.Combine(v, "valheim_Data", "StreamingAssets", "SoftRef", "Bundles"));
			if (b.Exists)
			{
				return b.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? b.FullName;
			}
		}
		return null;
	}

	private static string FindRepo()
	{
		for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
		{
			if (File.Exists(Path.Combine(d.FullName, "VERSION")) && Directory.Exists(Path.Combine(d.FullName, "Desktop")))
			{
				return d.FullName;
			}
		}
		throw new InvalidOperationException("the repository was not found above " + AppContext.BaseDirectory);
	}
}

// Answers the start page's "is Valheim running?" like the WorldEditorBridge plugin, for the "My game"
// picture (only /status: nothing is ever edited live).
public sealed class FakeGame : IDisposable
{
	public string Token { get; }
	public int Port { get; }

	private readonly HttpListener _listener = new();
	private readonly byte[] _status;

	public FakeGame(string world, string token = "docs")
	{
		Token = token;
		_status = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { world, players = 0 }));
		// A free port (HttpListener cannot be given port 0).
		using (var probe = new TcpListener(IPAddress.Loopback, 0))
		{
			probe.Start();
			Port = ((IPEndPoint)probe.LocalEndpoint).Port;
			probe.Stop();
		}
		_listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
		_listener.Start();
		_ = Task.Run(Serve);
	}

	private async Task Serve()
	{
		while (_listener.IsListening)
		{
			HttpListenerContext c;
			try
			{
				c = await _listener.GetContextAsync().ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
			{
				return;
			}
			bool ok = c.Request.HttpMethod == "GET" && (c.Request.RawUrl ?? "").StartsWith("/status", StringComparison.Ordinal)
				&& c.Request.Headers["X-Bridge-Token"] == Token;
			byte[] body = ok ? _status : "{}"u8.ToArray();
			c.Response.StatusCode = ok ? 200 : 403;
			c.Response.ContentType = "application/json";
			c.Response.ContentLength64 = body.Length;
			try
			{
				await c.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);
				c.Response.Close();
			}
			catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException)
			{
			}
		}
	}

	public void Dispose()
	{
		_listener.Close();
	}
}
