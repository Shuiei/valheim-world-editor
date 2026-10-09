using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

// Settings, saved servers, plugin settings and world discovery. These use the app's data folder,
// so they point it at a temporary one (XDG_DATA_HOME, and their own servers file) and run alone.
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
		// The native tests send saved servers to a test file: this test gets its own, in its folder.
		_oldServers = ServerConfig.PathOverride;
		ServerConfig.PathOverride = Path.Combine(_data, "servers.cfg");
		// The data folder from the environment here, not the tests' own.
		_oldDataDir = AppSettings.DataDirOverride;
		AppSettings.DataDirOverride = null;
	}

	private readonly string? _oldServers, _oldDataDir;

	public void Dispose()
	{
		Environment.SetEnvironmentVariable("XDG_DATA_HOME", _old);
		ServerConfig.PathOverride = _oldServers;
		AppSettings.DataDirOverride = _oldDataDir;
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

	// A saved password keeps spaces at its ends (it was trimmed, and the login then failed).
	[Fact]
	public void ASavedPasswordKeepsItsSpaces()
	{
		ServerConfig.Remember(new ServerConfig.Server { Name = "S", Host = "my.server.com", User = "valheim", Password = " pass word  " });
		Assert.Equal(" pass word  ", Assert.Single(ServerConfig.Load()).Password);
		Assert.False(File.Exists(ServerConfig.FilePath + ".tmp"));
	}

	// Settings that cannot be read are kept aside before the defaults are written over them, and a
	// settings file that cannot be written is left alone without an error.
	[Fact]
	public void BrokenSettingsAreKeptAsideAndAFailedWriteIsQuiet()
	{
		string path = Path.Combine(_data, "settings.json");
		string? old = AppSettings.PathOverride;
		AppSettings.PathOverride = path;
		try
		{
			Check(path);
		}
		finally
		{
			AppSettings.PathOverride = old;
		}
	}

	private static void Check(string path)
	{
		File.WriteAllText(path, "{\"ValheimPath\": \"/games/Valh");
		var s = AppSettings.Load();
		Assert.Null(s.ValheimPath);
		Assert.Equal("{\"ValheimPath\": \"/games/Valh", File.ReadAllText(path + ".bad"));
		s.ValheimPath = "/games/Valheim";
		s.Save();
		Assert.Equal("/games/Valheim", AppSettings.Load().ValheimPath);
		Assert.False(File.Exists(path + ".tmp"));
		File.Delete(path);
		Directory.CreateDirectory(path);
		s.Save();
		Assert.True(Directory.Exists(path));
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
		Assert.Null(Worlds.Check(w.Dir));
		Assert.Contains("does not exist", Worlds.Check("/no/such/folder")!);
		Assert.Contains("holds worlds", Worlds.Check(Path.GetDirectoryName(w.Dir)!)!);
		Assert.Contains("No Valheim world", Worlds.Check(_data)!);
	}

	[Fact]
	public void ExtraWorldFoldersAreListed()
	{
		using var w = new TempWorld();
		var settings = new AppSettings { WorldFolders = { Path.GetDirectoryName(w.Dir)! } };
		var found = Worlds.Find(settings);
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
