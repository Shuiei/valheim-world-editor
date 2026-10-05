using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

// Settings, saved servers, plugin settings and world discovery. These use the app's data folder,
// so they point it at a temporary one (XDG_DATA_HOME) and do not run in parallel.
[Collection("DataDir")]
public class AppTests : IDisposable
{
	private readonly string _data = Path.Combine(Path.GetTempPath(), "vwe-data-" + Guid.NewGuid().ToString("N")[..8]);

	private readonly string? _old = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

	public AppTests()
	{
		Directory.CreateDirectory(_data);
		Environment.SetEnvironmentVariable("XDG_DATA_HOME", _data);
		Environment.SetEnvironmentVariable("LOCALAPPDATA", _data);
	}

	public void Dispose()
	{
		Environment.SetEnvironmentVariable("XDG_DATA_HOME", _old);
		try
		{
			Directory.Delete(_data, true);
		}
		catch
		{
		}
	}

	[Fact]
	public void DataFolderFollowsTheEnvironment()
	{
		Assert.StartsWith(_data, AppSettings.DataDir);
	}

	[Fact]
	public void SavedServersRoundTripWithoutThePasswordUnlessAsked()
	{
		ServerConfig.Remember(new ServerConfig.Server { Name = "A", Host = "my.server.com", User = "valheim", Token = "t1", HostKey = "SHA256:abc", GameFolder = "/home/valheim/server", BridgePort = 5190 });
		ServerConfig.Remember(new ServerConfig.Server { Name = "B", Host = "203.0.113.10", SshPort = 2222, User = "admin", Password = "secret", Token = "t2" });
		var list = ServerConfig.Load();
		Assert.Equal(2, list.Count);
		Assert.Equal("B", list[0].Name);
		var a = list.Single(s => s.Name == "A");
		Assert.Null(a.Password);
		Assert.Equal("t1", a.Token);
		Assert.Equal("SHA256:abc", a.HostKey);
		Assert.Equal("/home/valheim/server", a.GameFolder);
		Assert.Equal(5190, a.BridgePort);
		Assert.Equal("secret", list.Single(s => s.Name == "B").Password);
		if (!OperatingSystem.IsWindows())
		{
			Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(ServerConfig.FilePath));
		}
		ServerConfig.Forget(a.Id);
		Assert.Single(ServerConfig.Load());
	}

	[Fact]
	public void RememberingTheSameServerUpdatesIt()
	{
		ServerConfig.Remember(new ServerConfig.Server { Name = "Old name", Host = "my.server.com", User = "valheim", Token = "t1" });
		ServerConfig.Remember(new ServerConfig.Server { Host = "my.server.com", User = "valheim", Token = "t2" });
		var s = Assert.Single(ServerConfig.Load());
		Assert.Equal("Old name", s.Name);
		Assert.Equal("t2", s.Token);
	}

	[Fact]
	public void PluginSettingsAreParsed()
	{
		var b = LocalGame.ParseText("[Http]\nBindAddress = 127.0.0.1\nPort = 5190\nToken = abc123\n", "x.cfg", "test");
		Assert.NotNull(b);
		Assert.Equal(5190, b!.Port);
		Assert.Equal("abc123", b.Token);
		Assert.Null(LocalGame.ParseText("Port = 5182\nToken = \n", "x.cfg", "test"));
		Assert.Equal(5182, LocalGame.ParseText("Token = z", "x.cfg", "test")!.Port);
	}

	[Fact]
	public void WorldFoldersAreChecked()
	{
		using var w = new TempWorld();
		Assert.Null(Launcher.CheckWorld(w.Dir));
		Assert.Contains("does not exist", Launcher.CheckWorld("/no/such/folder")!);
		Assert.Contains("holds worlds", Launcher.CheckWorld(Path.GetDirectoryName(w.Dir)!)!);
		Assert.Contains("No Valheim world", Launcher.CheckWorld(_data)!);
	}

	[Fact]
	public void ExtraWorldFoldersAreListed()
	{
		using var w = new TempWorld();
		var settings = new AppSettings { WorldFolders = { Path.GetDirectoryName(w.Dir)! } };
		var found = Launcher.FindWorlds(settings);
		var world = Assert.Single(found, x => x.Path == w.Dir);
		Assert.Equal("yours", world.Where);
		Assert.True(world.Usable);
		Assert.Equal(2, world.SaveNumber);
	}

	[Fact]
	public void BepInExFoldersFromSettingsAreSearched()
	{
		string profiles = Path.Combine(_data, "profiles");
		string cfg = Path.Combine(profiles, "Default", "BepInEx", "config");
		Directory.CreateDirectory(cfg);
		Directory.CreateDirectory(Path.Combine(profiles, "Default", "BepInEx", "plugins"));
		File.WriteAllText(Path.Combine(cfg, "local.worldeditorbridge.cfg"), "Port = 5191\nToken = tok\n");
		var settings = new AppSettings { BepInExFolders = { profiles } };
		var bridges = LocalGame.FindBridges(settings, out bool bepInEx, out _);
		Assert.True(bepInEx);
		Assert.Contains(bridges, b => b.Port == 5191 && b.Token == "tok");
	}
}

[CollectionDefinition("DataDir", DisableParallelization = true)]
public class DataDirCollection
{
}
