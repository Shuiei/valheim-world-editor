using System.Diagnostics;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// Drives the app for the documentation's pictures (its --driver mode, see editor/Desktop/Driver.cs).
/// </summary>
/// <remarks>
/// The app runs with a stand-in home: none of this computer's characters, worlds, servers or settings show in
/// the pictures. Only the copied game look is shared (read only), so the pictures have the game's textures and
/// models. Nothing is ever saved: each scene's changes stay pending and are discarded with its editor.
/// </remarks>
public sealed partial class Editor : IDisposable
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
    /// <param name="app">The app's dll (run with dotnet) or executable; null for the Debug build in editor/Desktop/bin.</param>
    /// <param name="width">Window width in pixels.</param>
    /// <param name="height">Window height in pixels.</param>
    /// <param name="gameWorld">When set, a stand-in running game with this world (the plugin in an r2modman
    /// profile, answering /status).</param>
    /// <param name="worlds">World folders or files linked into the stand-in Valheim's worlds_local.</param>
    /// <param name="homestead">A Homestead.dll to copy into the stand-in game's BepInEx plugins.</param>
    /// <param name="recent">Worlds listed as opened before on the start page, the first a few hours ago, then a day
    /// apart.</param>
    /// <param name="extra">More command-line arguments for the app (<c>--live</c> and <c>--token</c>).</param>
    /// <param name="valheim">Whether the stand-in home has Valheim (false: the start page asks for its folder).</param>
    /// <param name="appInHome">Run a copy of the app from the stand-in home's ValheimWorldEditor folder, so the
    /// folders shown next to the program (Settings' add-ons) read "~/ValheimWorldEditor".</param>
    public Editor(string? app = null, int width = 1440, int height = 900, string? gameWorld = null,
        IReadOnlyList<string>? worlds = null, string? homestead = null, IReadOnlyList<string>? recent = null,
        IReadOnlyList<string>? extra = null, bool valheim = true, bool appInHome = false)
    {
        _home = Directory.CreateTempSubdirectory("vwe-docs-home-").FullName;
        _width = width;
        // The stand-in home's own data folder (~/.local/share/ValheimWorldEditor): the start page shows
        // it as such. The index of the game's files and the game data read from them are copied in.
        string data = Path.Combine(_home, ".local", "share");
        _data = Path.Combine(data, "ValheimWorldEditor");
        Directory.CreateDirectory(_data);
        CopyGameData();
        // A stand-in Valheim in the home's Steam library ("Found automatically" in Settings); the app is
        // driven, so it never looks for Steam outside this home. Its game data is this computer's
        // Valheim's (read only): the bundles for the game's look, the game's assembly and assets for
        // the pieces, objects and rules the app reads from the game.
        string game = Path.Combine(_home, ".local", "share", "Steam", "steamapps", "common", "Valheim");
        if (valheim)
        {
            StandInValheim(game);
        }
        // Worlds in Valheim's own folder, as a player has them (links to the read-only originals).
        string local = Path.Combine(_home, ".config", "unity3d", "IronGate", "Valheim", "worlds_local");
        LinkWorlds(local, worlds);
        if (recent is { Count: > 0 })
        {
            WriteRecent(local, recent);
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
            string plugins = Path.Combine(gameWorld != null ? bep : Path.Combine(game, "BepInEx"), "plugins");
            Directory.CreateDirectory(plugins);
            File.Copy(homestead, Path.Combine(plugins, "Homestead.dll"), overwrite: true);
        }
        _p = StartApp(appInHome ? AppInHome(app) : app, width, height, extra, data);
        // Its error output is read and dropped (unread, a full pipe would block it).
        _p.ErrorDataReceived += (_, _) => { };
        _p.BeginErrorReadLine();
        if (Answer() != "@@ ready")
        {
            throw new InvalidOperationException("the editor did not start");
        }
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
}
