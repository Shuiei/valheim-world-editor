using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// How the app starts: the command line (a world folder, a live game with its token, an area in the
// editor, the tests' data folder), the log that copies everything said, the data folder's override
// (the copied game files stay where they are), the window's icon and the documentation link.
[Collection("DataDir")]
public sealed class StartupTests : IDisposable
{
	private readonly string? _settings = AppSettings.PathOverride, _servers = ServerConfig.PathOverride, _place = PlaceMemory.PathOverride, _stamps = Stamps.PathOverride, _data = AppSettings.DataDirOverride;
	private readonly string? _token = Environment.GetEnvironmentVariable("WORLD_BRIDGE_TOKEN");
	private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-startup-" + Guid.NewGuid().ToString("N")[..8]);

	public void Dispose()
	{
		AppSettings.PathOverride = _settings;
		ServerConfig.PathOverride = _servers;
		PlaceMemory.PathOverride = _place;
		Stamps.PathOverride = _stamps;
		AppSettings.DataDirOverride = _data;
		Environment.SetEnvironmentVariable("WORLD_BRIDGE_TOKEN", _token);
		Options.Parse(Array.Empty<string>());
		try
		{
			Directory.Delete(_dir, true);
		}
		catch (Exception)
		{
		}
	}

	[Fact]
	public void AWorldFolderAloneOpensItsMap()
	{
		Options.Parse(new[] { "/worlds/Midgard" });
		Assert.Equal("/worlds/Midgard", Options.Folder);
		Assert.False(Options.Direct);
		Assert.Null(Options.LiveUrl);
		// The first one counts; options and their values are not folders.
		Options.Parse(new[] { "--size", "3", "/a", "/b" });
		Assert.Equal("/a", Options.Folder);
		Assert.Equal(3, Options.Size);
	}

	[Fact]
	public void ALiveGameTakesItsTokenFromTheLineOrTheEnvironment()
	{
		Environment.SetEnvironmentVariable("WORLD_BRIDGE_TOKEN", null);
		Options.Parse(new[] { "--live", "http://127.0.0.1:5182", "--token", "abc" });
		Assert.Equal("http://127.0.0.1:5182", Options.LiveUrl);
		Assert.Equal("abc", Options.LiveToken);
		Assert.Null(Options.Folder);
		Environment.SetEnvironmentVariable("WORLD_BRIDGE_TOKEN", "from-env");
		Options.Parse(new[] { "--live", "http://server:5182" });
		Assert.Equal("from-env", Options.LiveToken);
		// The line wins over the environment.
		Options.Parse(new[] { "--live", "http://server:5182", "--token", "typed" });
		Assert.Equal("typed", Options.LiveToken);
		// Nothing left over from the last start.
		Options.Parse(Array.Empty<string>());
		Assert.Null(Options.LiveUrl);
		Assert.Null(Options.Folder);
	}

	[Fact]
	public void AnAreaOpensStraightInTheEditor()
	{
		Options.Parse(new[] { "--world", "Midgard", "--zone", "-3,7", "--size", "12" });
		Assert.True(Options.Direct);
		Assert.Equal("Midgard", Options.World);
		Assert.Equal((-3, 7), (Options.ZoneX, Options.ZoneZ));
		// At most 9 zones across, at least 1.
		Assert.Equal(9, Options.Size);
		Options.Parse(new[] { "--size", "0" });
		Assert.Equal(1, Options.Size);
		Assert.Equal((0, 0), (Options.WindowWidth, Options.WindowHeight));
	}

	[Fact]
	public void TheWindowsSizeCanBeGivenWithinLimits()
	{
		Options.Parse(new[] { "--window", "1440x900" });
		Assert.Equal((1440, 900), (Options.WindowWidth, Options.WindowHeight));
		Options.Parse(new[] { "--window", "10x99999" });
		Assert.Equal((640, 4320), (Options.WindowWidth, Options.WindowHeight));
	}

	[Fact]
	public void TheTestsDataFolderTakesEverythingButTheGamesLook()
	{
		string data = Path.Combine(_dir, "data");
		Options.Parse(new[] { "--data", data, "--driver" });
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
		var lines = Log.Read().Split('\n');
		Assert.StartsWith("Valheim World Editor 9.9.9, ", lines[0]);
		Assert.Equal("Data folder: " + Path.Combine(_dir, "logdata"), lines[1]);
		// Each line with its time, like the game's LogOutput.log; a line written in parts gets one.
		Assert.Equal(new[] { "hello log", "an error", "!tail" }, lines[2..].Select(l => System.Text.RegularExpressions.Regex.Replace(l, @"^\d\d:\d\d:\d\d ", "")));
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

	[Fact]
	public void TheTestsEditorOnlyLooksForTheGamesLookAndNeverCopiesIt()
	{
		string xdg = Path.Combine(_dir, "xdg"), game = Path.Combine(_dir, "Valheim");
		Directory.CreateDirectory(Path.Combine(game, "valheim_Data", "StreamingAssets", "SoftRef", "Bundles"));
		string? oldXdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME"), oldLocal = Environment.GetEnvironmentVariable("LOCALAPPDATA");
		Environment.SetEnvironmentVariable("XDG_DATA_HOME", xdg);
		Environment.SetEnvironmentVariable("LOCALAPPDATA", null);
		try
		{
			AppSettings.PathOverride = Path.Combine(_dir, "settings.json");
			// Only ever this test's folder (never the user's copied game files).
			Assert.True(GameLook.Dir.StartsWith(_dir), $"game look in {GameLook.Dir}");
			var settings = new AppSettings { ValheimPath = game };
			GameLook.Check(settings, export: false);
			Assert.Equal("missing", GameLook.Now().State);
			Assert.False(Directory.Exists(GameLook.Dir), "nothing copied");
			// Once the files are there: ready.
			foreach (string f in new[] { "terrain/heightmap.frag.glsl", "maptex/background.png", "models/objects.json" })
			{
				Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(GameLook.Dir, f))!);
				File.WriteAllText(Path.Combine(GameLook.Dir, f), "");
			}
			GameLook.Check(settings, export: false);
			Assert.Equal("ready", GameLook.Now().State);
		}
		finally
		{
			Environment.SetEnvironmentVariable("XDG_DATA_HOME", oldXdg);
			Environment.SetEnvironmentVariable("LOCALAPPDATA", oldLocal);
		}
	}

	// A copy from Valheim for Windows whose terrain shader cannot be made here: said, not copied again
	// at every start (minutes each time, ending the same way). An older converter's shader is made
	// again from the kept SPIR-V, without a new copy.
	[Fact]
	public void AShaderThatCannotBeMadeIsSaidNotCopiedAgain()
	{
		string xdg = Path.Combine(_dir, "xdg"), game = Path.Combine(_dir, "Valheim");
		Directory.CreateDirectory(Path.Combine(game, "valheim_Data", "StreamingAssets", "SoftRef", "Bundles"));
		string? oldXdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME"), oldLocal = Environment.GetEnvironmentVariable("LOCALAPPDATA");
		Environment.SetEnvironmentVariable("XDG_DATA_HOME", xdg);
		Environment.SetEnvironmentVariable("LOCALAPPDATA", null);
		try
		{
			AppSettings.PathOverride = Path.Combine(_dir, "settings.json");
			Assert.True(GameLook.Dir.StartsWith(_dir), $"game look in {GameLook.Dir}");
			foreach (string f in new[] { "maptex/background.png", "models/objects.json" })
			{
				Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(GameLook.Dir, f))!);
				File.WriteAllText(Path.Combine(GameLook.Dir, f), "");
			}
			string terrain = Path.Combine(GameLook.Dir, "terrain");
			Directory.CreateDirectory(terrain);
			File.WriteAllBytes(Path.Combine(terrain, TerrainShader.SpirvFile), new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
			File.WriteAllText(GameLook.MarkerPath, System.Text.Json.JsonSerializer.Serialize(new GameLook.Marker(game, GameLook.BuildId(game), GameLook.ExporterVersion, DateTime.Now)));
			var settings = new AppSettings { ValheimPath = game };
			GameLook.Check(settings, export: false);
			Assert.Equal("failed", GameLook.Now().State);
			Assert.StartsWith("The game's look is copied, but its terrain shader could not be made: ", GameLook.Now().Message);
			// A shader made by an older converter: it is the one used while it cannot be made again.
			File.WriteAllText(Path.Combine(terrain, TerrainShader.GlslFile), "// an older converter's shader");
			Assert.False(TerrainShader.IsCurrent(terrain));
			GameLook.Check(settings, export: false);
			Assert.Equal("ready", GameLook.Now().State);
			Assert.Equal("// an older converter's shader", File.ReadAllText(Path.Combine(terrain, TerrainShader.GlslFile)));
		}
		finally
		{
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
			Options.Parse(new[] { "--driver" });
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
	public void TheWindowHasItsIconAndTheStartPageLinksTheDocumentation()
	{
		var w = new MainWindow(load: false);
		Assert.NotNull(w.Icon);
		Assert.Equal($"Valheim World Editor {BuildInfo.Version}", w.Title);
		var page = new StartPage(new AppSettings());
		Uri? opened = null;
		page.OpenUrl = u => { opened = u; return Task.FromResult(true); };
		page.DocsLink.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
		Assert.Equal(new Uri(StartPage.DocsUrl), opened);
		Assert.Equal("https://github.com/Shuiei/valheim-world-editor#readme", StartPage.DocsUrl);
	}
}
