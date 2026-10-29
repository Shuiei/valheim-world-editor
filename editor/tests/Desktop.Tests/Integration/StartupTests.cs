using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// How the app starts: a live game with its token, the tests' data folder, the log that copies everything said, the
/// data folder's override (the copied game files stay where they are), the window's icon and the documentation link.
/// </summary>
[Collection("DataDir")]
public sealed class StartupTests : IDisposable
{
    private readonly string? _settings = AppSettings.PathOverride, _servers = ServerConfig.PathOverride, _place = PlaceMemory.PathOverride, _stamps = Stamps.PathOverride, _data = AppSettings.DataDirOverride;

    private readonly string? _token = Environment.GetEnvironmentVariable("WORLD_BRIDGE_TOKEN");

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-startup-" + Guid.NewGuid().ToString("N")[..8]);

    /// <inheritdoc/>
    public void Dispose()
    {
        AppSettings.PathOverride = _settings;
        ServerConfig.PathOverride = _servers;
        PlaceMemory.PathOverride = _place;
        Stamps.PathOverride = _stamps;
        AppSettings.DataDirOverride = _data;
        Environment.SetEnvironmentVariable("WORLD_BRIDGE_TOKEN", _token);
        Options.Parse([]);
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    [Fact]
    public void ALiveGameTakesItsTokenFromTheLineOrTheEnvironment()
    {
        Environment.SetEnvironmentVariable("WORLD_BRIDGE_TOKEN", null);
        Options.Parse(["--live", "http://127.0.0.1:5182", "--token", "abc"]);
        Assert.Equal("http://127.0.0.1:5182", Options.LiveUrl);
        Assert.Equal("abc", Options.LiveToken);
        Assert.Null(Options.Folder);
        Environment.SetEnvironmentVariable("WORLD_BRIDGE_TOKEN", "from-env");
        Options.Parse(["--live", "http://127.0.0.1:5182"]);
        Assert.Equal("from-env", Options.LiveToken);
        // The line wins over the environment.
        Options.Parse(["--live", "http://127.0.0.1:5182", "--token", "typed"]);
        Assert.Equal("typed", Options.LiveToken);
        // Nothing left over from the last start.
        Options.Parse([]);
        Assert.Null(Options.LiveUrl);
        Assert.Null(Options.Folder);
    }

    [Fact]
    public void TheTestsDataFolderTakesEverythingButTheGamesLook()
    {
        string data = Path.Combine(_dir, "data");
        Options.Parse(["--data", data, "--driver"]);
        Assert.True(Options.Driver);
        Assert.Equal(data, AppSettings.DataDir);
        Assert.Equal(Path.Combine(data, "settings.json"), AppSettings.PathOverride);
        Assert.Equal(Path.Combine(data, "servers.cfg"), ServerConfig.FilePath);
        Assert.Equal(Path.Combine(data, "perf-native.log"), PerfLog.FilePath);
        Assert.Equal(Path.Combine(data, "ValheimWorldEditor.log"), Log.FilePath);
        // The copied game files are read from the user's folder (never written by the tests).
        Assert.Equal(Path.Combine(AppSettings.UserDataDir, "game-look"), GameLook.Dir);
        Assert.NotEqual(data, AppSettings.UserDataDir);
    }

    [Fact]
    public void TheLogCopiesWhatIsSaidAfterWhatIsRunning()
    {
        AppSettings.DataDirOverride = Path.Combine(_dir, "logdata");
        var (stdout, stderr) = (Console.Out, Console.Error);
        try
        {
            Log.Start("9.9.9");
            Console.WriteLine("hello log");
            Console.Error.WriteLine("an error");
            Console.Write('!');
            Console.Write("tail");
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }
        string[] lines = Log.Read().Split('\n');
        Assert.StartsWith("Valheim World Editor 9.9.9, ", lines[0]);
        Assert.Equal("Data folder: " + Path.Combine(_dir, "logdata"), lines[1]);
        // Each line with its time, like the game's LogOutput.log; a line written in parts gets one.
        Assert.Equal(["hello log", "an error", "!tail"], lines[2..].Select(l => System.Text.RegularExpressions.Regex.Replace(l, @"^\d\d:\d\d:\d\d ", "")));
        Assert.All(lines[2..], l => Assert.Matches(@"^\d\d:\d\d:\d\d \S", l));
    }

    [Fact]
    public void EachRunStartsTheLogOver()
    {
        AppSettings.DataDirOverride = Path.Combine(_dir, "logdata2");
        var (stdout, stderr) = (Console.Out, Console.Error);
        try
        {
            Log.Start("1.0.0");
            Console.WriteLine("first run");
            Log.Start("1.0.1");
            Console.WriteLine("second run");
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }
        string log = Log.Read();
        Assert.StartsWith("Valheim World Editor 1.0.1, ", log);
        Assert.Contains("second run", log);
        Assert.DoesNotContain("first run", log);
    }

    [Fact]
    public void ALogThatCannotBeWrittenStopsNothing()
    {
        // The data folder is a file: no log, no error.
        Directory.CreateDirectory(_dir);
        string file = Path.Combine(_dir, "not-a-folder");
        File.WriteAllText(file, "");
        AppSettings.DataDirOverride = Path.Combine(file, "below");
        var stdout = Console.Out;
        try
        {
            Log.Start("1.0.0");
        }
        catch (IOException)
        {
            // DataDir itself cannot make the folder: that is the caller's to see, not the log's.
        }
        finally
        {
            Console.SetOut(stdout);
        }
        Assert.Same(stdout, Console.Out);
    }

    // The game's look is read from the game folder: found, its bundles indexed in the background (here
    // none: said); no game: said. The copy older editors made is deleted.
    [Fact]
    public void TheGamesLookIsReadFromTheGameFolderAndTheOldCopyGoes()
    {
        string xdg = Path.Combine(_dir, "xdg"), game = Path.Combine(_dir, "Valheim");
        Directory.CreateDirectory(Path.Combine(game, "valheim_Data", "StreamingAssets", "SoftRef", "Bundles"));
        string? oldXdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME"), oldLocal = Environment.GetEnvironmentVariable("LOCALAPPDATA"), oldHome = Environment.GetEnvironmentVariable("HOME");
        bool outside = GameLook.SearchOutsideHome;
        Directory.CreateDirectory(Path.Combine(_dir, "home"));
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", xdg);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", null);
        // A home without Steam, and none outside it: no Valheim but the one given.
        Environment.SetEnvironmentVariable("HOME", Path.Combine(_dir, "home"));
        GameLook.SearchOutsideHome = false;
        try
        {
            AppSettings.PathOverride = Path.Combine(_dir, "settings.json");
            // Only ever this test's folder (never the user's).
            Assert.True(GameLook.Dir.StartsWith(_dir, StringComparison.Ordinal), $"game look in {GameLook.Dir}");
            Assert.True(GameLook.IndexFile.StartsWith(_dir, StringComparison.Ordinal), $"index in {GameLook.IndexFile}");
            GameLook.Check(new AppSettings { ValheimPath = Path.Combine(_dir, "nowhere") });
            Assert.Equal("missing", GameLook.Now().State);
            Assert.Null(GameLook.Bundles);
            GameLook.Check(new AppSettings { ValheimPath = game });
            Assert.NotNull(GameLook.Bundles);
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (GameLook.Now().State == "running" && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(20);
            }
            Assert.Equal("failed", GameLook.Now().State);
            Assert.Equal("No models were found in the game's files.", GameLook.Now().Message);
            // The old copy: gone.
            Directory.CreateDirectory(Path.Combine(GameLook.Dir, "models"));
            File.WriteAllText(Path.Combine(GameLook.Dir, "models", "objects.json"), "{}");
            GameLook.DeleteOldCopy();
            Assert.False(Directory.Exists(GameLook.Dir));
        }
        finally
        {
            GameLook.SearchOutsideHome = outside;
            Environment.SetEnvironmentVariable("HOME", oldHome);
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", oldXdg);
            Environment.SetEnvironmentVariable("LOCALAPPDATA", oldLocal);
        }
    }

    [Fact]
    public void DrivenTheEditorLooksForTheGameOnlyUnderTheHome()
    {
        string home = Path.Combine(_dir, "home");
        Directory.CreateDirectory(home);
        string? oldHome = Environment.GetEnvironmentVariable("HOME");
        bool outside = GameLook.SearchOutsideHome;
        Environment.SetEnvironmentVariable("HOME", home);
        try
        {
            // An empty home: no Valheim, wherever else the computer has one.
            Options.Parse(["--driver"]);
            Assert.False(GameLook.SearchOutsideHome);
            Assert.Null(GameLook.FindValheim(null));
            // One in the home's Steam library is found.
            string valheim = Path.Combine(home, ".local", "share", "Steam", "steamapps", "common", "Valheim");
            Directory.CreateDirectory(Path.Combine(valheim, "valheim_Data", "StreamingAssets", "SoftRef", "Bundles"));
            Assert.Equal(Path.GetFullPath(valheim), Path.GetFullPath(GameLook.FindValheim(null)!));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", oldHome);
            GameLook.SearchOutsideHome = outside;
        }
    }

    [AvaloniaFact]
    public void TheWindowHasItsIconAndTheStartPageOpensTheHelp()
    {
        var w = new MainWindow(load: false);
        Assert.NotNull(w.Icon);
        Assert.Equal($"Valheim World Editor {BuildInfo.Version}", w.Title);
        var page = new StartPage(new AppSettings());
        int opened = 0;
        page.OpenHelp = () => opened++;
        page.DocsLink.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1, opened);
    }
}
