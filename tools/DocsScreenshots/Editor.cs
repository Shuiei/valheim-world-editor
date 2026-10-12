using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SkiaSharp;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// Drives the app for the documentation's pictures (its --driver mode, see Desktop/Driver.cs).
/// </summary>
/// <remarks>
/// The app runs with a stand-in home: none of this computer's characters, worlds, servers or settings show in
/// the pictures. Only the copied game look is shared (read only), so the pictures have the game's textures and
/// models. Nothing is ever saved: each scene's changes stay pending and are discarded with its editor.
/// </remarks>
public sealed class Editor : IDisposable
{
    /// <summary>The repository: the folder above this program that holds VERSION and Desktop.</summary>
    public static readonly string Repo = FindRepo();

    /// <summary>The stand-in home, a temporary folder deleted on Dispose.</summary>
    private readonly string _home;
    /// <summary>The window's width in pixels; pictures are scaled down to it.</summary>
    private readonly int _width;
    /// <summary>The stand-in running game, when the scene shows one.</summary>
    private readonly FakeGame? _game;
    /// <summary>The driven app.</summary>
    private readonly Process _p;
    /// <summary>The stand-in home's data folder (its settings, log and game data).</summary>
    private readonly string _data;

    /// <summary>
    /// Where the game data read by the scenes' editors is kept between scenes and runs (never this computer's own
    /// data folder).
    /// </summary>
    private static string GameDataCache => Path.Combine(Path.GetTempPath(), "vwe-docs-gamedata");

    /// <summary>
    /// Makes the stand-in home and starts the app in it, driven, and waits until it is ready.
    /// </summary>
    /// <param name="app">The app's dll (run with dotnet) or executable; null for the Debug build in Desktop/bin.</param>
    /// <param name="width">Window width in pixels.</param>
    /// <param name="height">Window height in pixels.</param>
    /// <param name="gameWorld">When set, a stand-in running game with this world (the plugin in an r2modman
    /// profile, answering /status).</param>
    /// <param name="worlds">World folders or files linked into the stand-in Valheim's worlds_local.</param>
    /// <param name="homestead">A Homestead.dll to copy into the stand-in game's BepInEx plugins.</param>
    /// <param name="recent">Worlds listed as opened before on the start page, the first a few hours ago, then a day
    /// apart.</param>
    public Editor(string? app = null, int width = 1440, int height = 900, string? gameWorld = null,
        IReadOnlyList<string>? worlds = null, string? homestead = null, IReadOnlyList<string>? recent = null)
    {
        _home = Directory.CreateTempSubdirectory("vwe-docs-home-").FullName;
        _width = width;
        // The stand-in home's own data folder (~/.local/share/ValheimWorldEditor): the start page shows
        // it as such. The index of the game's files and the game data read from them are copied in: the
        // ones the last scene kept (GameDataCache), else this computer's. The game data is stamped with
        // the build that read it, so a new build reads the game again, once: the next scenes get it.
        string data = Path.Combine(_home, ".local", "share");
        _data = Path.Combine(data, "ValheimWorldEditor");
        Directory.CreateDirectory(_data);
        string mineDir = Path.Combine(UserHome, ".local", "share", "ValheimWorldEditor");
        string from = Directory.Exists(GameDataCache) && Directory.GetFiles(GameDataCache, "game-*.json").Length > 0 ? GameDataCache : mineDir;
        foreach (string mine in Directory.Exists(from) ? Directory.GetFiles(from, "game-*.json") : [])
        {
            File.Copy(mine, Path.Combine(_data, Path.GetFileName(mine)));
        }
        // A stand-in Valheim in the home's Steam library ("Found automatically" in Settings); the app is
        // driven, so it never looks for Steam outside this home. Its game data is this computer's
        // Valheim's (read only): the bundles for the game's look, the game's assembly and assets for
        // the pieces, objects and rules the app reads from the game.
        string valheim = Path.Combine(_home, ".local", "share", "Steam", "steamapps", "common", "Valheim");
        string gameData = Path.Combine(valheim, "valheim_Data");
        Directory.CreateDirectory(valheim);
        if (RealGameData() is string real)
        {
            Directory.CreateSymbolicLink(gameData, real);
        }
        else
        {
            Directory.CreateDirectory(Path.Combine(gameData, "StreamingAssets", "SoftRef", "Bundles"));
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
            File.WriteAllText(Path.Combine(_data, "settings.json"), JsonSerializer.Serialize(new { Recent = opened }));
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

    /// <summary>The real user's home (the stand-in one is only the app's).</summary>
    private static string UserHome => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>The app's next answer line (starting "@@ "); other output is skipped.</summary>
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

    /// <summary>One command; its answer's state (an error answer throws).</summary>
    /// <returns>The JSON state after "@@ ok ", cloned so it outlives the answer.</returns>
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

    /// <summary>
    /// The whole window, saved as a JPEG for the docs (the status bar's message cleared unless kept: it would tell
    /// how the scene was set up).
    /// </summary>
    /// <param name="path">The JPEG file to write.</param>
    /// <param name="quality">JPEG quality, 0 to 100.</param>
    /// <param name="keepMessage">Keep the status bar's message in the picture.</param>
    /// <returns>The app's state when the picture was taken.</returns>
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

    /// <summary>
    /// Asks the app to quit (killing it when it cannot be asked or does not exit within 15 s), stops the
    /// stand-in game and deletes the stand-in home.
    /// </summary>
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
            // The game data as this build read it, for the next scenes.
            Directory.CreateDirectory(GameDataCache);
            foreach (string f in Directory.GetFiles(_data, "game-*.json"))
            {
                File.Copy(f, Path.Combine(GameDataCache, Path.GetFileName(f)), overwrite: true);
            }
        }
        catch (IOException)
        {
        }
        try
        {
            Directory.Delete(_home, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>
    /// This computer's Valheim valheim_Data folder (the app's settings first, then the usual Steam places), links
    /// resolved; null when none is found.
    /// </summary>
    private static string? RealGameData()
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
            var b = new DirectoryInfo(Path.Combine(v, "valheim_Data"));
            if (b.Exists && Directory.Exists(Path.Combine(b.FullName, "StreamingAssets", "SoftRef", "Bundles")))
            {
                return b.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? b.FullName;
            }
        }
        return null;
    }

    /// <summary>Finds <c>Repo</c> from this program's folder upwards; throws when there is none.</summary>
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

/// <summary>
/// Answers the start page's "is Valheim running?" like the WorldEditorBridge plugin, for the "My game" picture
/// (only /status: nothing is ever edited live).
/// </summary>
public sealed class FakeGame : IDisposable
{
    /// <summary>The token the app must send (X-Bridge-Token), written into the stand-in plugin's settings.</summary>
    public string Token { get; }
    /// <summary>The free local port it listens on.</summary>
    public int Port { get; }

    /// <summary>Listens on 127.0.0.1 at <c>Port</c>.</summary>
    private readonly HttpListener _listener = new();
    /// <summary>The /status answer: the world's name and no players, as JSON.</summary>
    private readonly byte[] _status;

    /// <summary>Starts listening on a free local port.</summary>
    /// <param name="world">The world name /status reports.</param>
    /// <param name="token">The token requests must carry.</param>
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

    /// <summary>Answers GET /status with the right token; anything else gets 403 and an empty JSON object.</summary>
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

    /// <summary>Stops listening, which ends <c>Serve</c>.</summary>
    public void Dispose()
    {
        _listener.Close();
    }
}
