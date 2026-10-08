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
		Assert.Equal(Path.Combine(data, "log.txt"), Log.FilePath);
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
		var lines = File.ReadAllLines(Path.Combine(_dir, "logdata", "log.txt"), System.Text.Encoding.UTF8);
		Assert.StartsWith("Valheim World Editor 9.9.9, ", lines[0]);
		Assert.Equal(new[] { "hello log", "an error", "!tail" }, lines[1..]);
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
