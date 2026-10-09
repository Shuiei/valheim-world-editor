using System.Net;
using System.Net.Sockets;
using System.Text;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

// What the app finds in a player's home folder, on a pretend home (HOME and the XDG folders point at a
// temporary folder, put back afterwards): settings, Valheim's world folders and old single-file worlds,
// characters, the BepInEx folders of the game and of mod managers, the plugin's status, free ports,
// and the SSH tunnel's checks before it connects (its key files, a closed port, a server that does
// not speak SSH). Nothing here reads the real home, ~/.ssh or the real settings.
[Collection("DataDir")]
public sealed class CoreAppHomeTests : IDisposable
{
	private static readonly string[] Vars = { "HOME", "XDG_CONFIG_HOME", "XDG_DATA_HOME", "LOCALAPPDATA" };
	private readonly Dictionary<string, string?> _old = Vars.ToDictionary(v => v, Environment.GetEnvironmentVariable);
	private readonly string? _settings = AppSettings.PathOverride, _servers = ServerConfig.PathOverride, _dataDir = AppSettings.DataDirOverride;
	private readonly bool _defaults = LocalGame.SearchDefaultPlaces;
	private readonly string _home = Path.Combine(Path.GetTempPath(), "vwe-home-" + Guid.NewGuid().ToString("N")[..8]);

	public CoreAppHomeTests()
	{
		Directory.CreateDirectory(_home);
		Environment.SetEnvironmentVariable("HOME", _home);
		Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(_home, ".config"));
		Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(_home, ".local", "share"));
		Environment.SetEnvironmentVariable("LOCALAPPDATA", null);
		AppSettings.PathOverride = null;
		AppSettings.DataDirOverride = null;
		ServerConfig.PathOverride = Path.Combine(_home, "servers.cfg");
		// Only the folders a test sets up (Steam's own folders outside the home are never looked at).
		LocalGame.SearchDefaultPlaces = false;
	}

	// A folder that looks like the game's (for the tests that look in the default places).
	private string FakeValheim()
	{
		string valheim = Local("Steam", "Valheim");
		Directory.CreateDirectory(Path.Combine(valheim, "valheim_Data", "StreamingAssets", "SoftRef", "Bundles"));
		return valheim;
	}

	public void Dispose()
	{
		foreach (var (k, v) in _old)
		{
			Environment.SetEnvironmentVariable(k, v);
		}
		AppSettings.PathOverride = _settings;
		AppSettings.DataDirOverride = _dataDir;
		ServerConfig.PathOverride = _servers;
		LocalGame.SearchDefaultPlaces = _defaults;
		try
		{
			Directory.Delete(_home, true);
		}
		catch (Exception)
		{
		}
	}

	private string Local(params string[] parts) => Path.Combine(new[] { _home }.Concat(parts).ToArray());

	private static void CopyWorld(string to)
	{
		using var w = new TempWorld();
		TempWorld.CopyDir(w.Dir, to);
	}

	// ---- Settings.

	[Fact]
	public void SettingsLiveInTheDataFolderAndSurviveARoundTrip()
	{
		Assert.Equal(Local(".local", "share", "ValheimWorldEditor"), AppSettings.DataDir);
		Assert.True(Directory.Exists(AppSettings.DataDir));
		var s = new AppSettings { ValheimPath = "/games/valheim", LiveUrl = "http://x:1", LastMode = "server", WorldFolders = { "/w" }, BepInExFolders = { "/b" } };
		s.Save();
		var back = AppSettings.Load();
		Assert.Equal("/games/valheim", back.ValheimPath);
		Assert.Equal("server", back.LastMode);
		Assert.Equal(new[] { "/w" }, back.WorldFolders);
		Assert.Equal(new[] { "/b" }, back.BepInExFolders);
	}

	[Fact]
	public void BrokenSettingsGiveDefaults()
	{
		File.WriteAllText(Path.Combine(AppSettings.DataDir, "settings.json"), "{ this is not json");
		var s = AppSettings.Load();
		Assert.Null(s.ValheimPath);
		Assert.Empty(s.Recent);
		// "null" is valid JSON for no settings at all.
		File.WriteAllText(Path.Combine(AppSettings.DataDir, "settings.json"), "null");
		Assert.NotNull(AppSettings.Load());
	}

	[Fact]
	public void RecentWorldsAreNewestFirstOnceEachAndAtMostTwelve()
	{
		var s = new AppSettings();
		for (int i = 0; i < 14; i++)
		{
			s.AddRecent(Local("w" + i), "W" + i);
		}
		Assert.Equal(12, s.Recent.Count);
		Assert.Equal("W13", s.Recent[0].Name);
		// The same folder written another way moves to the top instead of appearing twice.
		s.AddRecent(Local("w5", "..", "w5"), "W5 again");
		Assert.Equal(12, s.Recent.Count);
		Assert.Equal("W5 again", s.Recent[0].Name);
		Assert.Single(s.Recent, r => Path.GetFullPath(r.Path) == Local("w5"));
		Assert.Equal(12, AppSettings.Load().Recent.Count);
	}

	// ---- Valheim's folders.

	[Fact]
	public void ValheimsWorldFoldersAreUnderTheHome()
	{
		var roots = Places.WorldRoots().ToList();
		Assert.Contains(Local(".config", "unity3d", "IronGate", "Valheim", "worlds_local"), roots);
		Assert.Contains(Local(".config", "unity3d", "IronGate", "Valheim", "worlds"), roots);
		Assert.All(roots, r => Assert.StartsWith(_home, r));
		// Valheim through Proton keeps its folder inside Steam's prefix.
		Assert.Contains(roots, r => r.Contains(Path.Combine("compatdata", "892970")));
	}

	[Fact]
	public void TheGamesWorldsAreFoundWithOldSingleFileWorlds()
	{
		string local = Local(".config", "unity3d", "IronGate", "Valheim", "worlds_local");
		CopyWorld(Path.Combine(local, "Alpha"));
		Directory.CreateDirectory(Path.Combine(local, "Empty"));
		File.WriteAllText(Path.Combine(local, "Ancient.db"), "old");
		// An old world that has been converted already: its folder is listed, not the .db.
		CopyWorld(Path.Combine(local, "Converted"));
		File.WriteAllText(Path.Combine(local, "Converted.db"), "old");
		var found = Worlds.Find(new AppSettings());
		Assert.Contains(found, w => w.Name == "Alpha" && w.Where == "local" && w.Usable && w.SaveNumber == 2);
		Assert.DoesNotContain(found, w => w.Name == "Empty");
		var ancient = Assert.Single(found, w => w.Name == "Ancient");
		Assert.False(ancient.Usable);
		Assert.StartsWith("Old save format", ancient.Problem);
		Assert.DoesNotContain(found, w => w.Path.EndsWith("Converted.db"));
		Assert.Single(found, w => w.Name == "Converted");
	}

	[Fact]
	public void RecentWorldsComeFirstAndMissingOnesAreLeftOut()
	{
		string mine = Local("saves", "Mine");
		CopyWorld(mine);
		string direct = Local("server", "Direct");
		CopyWorld(direct);
		var s = new AppSettings { WorldFolders = { direct, Local("no such folder") } };
		s.Recent.Add(new AppSettings.RecentWorld(Local("gone"), "Gone", DateTime.Now));
		s.Recent.Add(new AppSettings.RecentWorld(mine, "Mine", DateTime.Now));
		// Listed once, as recent, though it is also in a world folder.
		s.WorldFolders.Add(Local("saves"));
		var found = Worlds.Find(s);
		Assert.Equal("Mine", found[0].Name);
		Assert.Equal("recent", found[0].Where);
		Assert.Single(found, w => w.Path == mine);
		Assert.Contains(found, w => w.Path == direct && w.Where == "yours");
		Assert.DoesNotContain(found, w => w.Name == "Gone");
	}

	[Fact]
	public void WorldFoldersAreCheckedForWhatTheyAre()
	{
		Assert.Equal("Enter the world's folder.", Worlds.Check(" "));
		File.WriteAllText(Local("Old.db"), "x");
		Assert.StartsWith("That is an old single-file world", Worlds.Check(Local("Old.db")));
		Assert.Equal("Alpha", Worlds.WorldName(Local("x", "Alpha") + Path.DirectorySeparatorChar));
	}

	[Fact]
	public void AFolderThatCannotBeReadIsSaidSo()
	{
		if (OperatingSystem.IsWindows() || Environment.UserName == "root")
		{
			return;
		}
		string locked = Local("locked");
		Directory.CreateDirectory(Path.Combine(locked, "inside"));
		File.SetUnixFileMode(locked, UnixFileMode.None);
		try
		{
			Assert.StartsWith("That folder cannot be read", Worlds.Check(locked));
			// A readable folder holding an unreadable one: that one simply holds no world.
			Assert.StartsWith("No Valheim world", Worlds.Check(_home));
			// A recent world now in a folder that cannot be read: left out, the rest still listed.
			var s = new AppSettings();
			s.Recent.Add(new AppSettings.RecentWorld(locked, "Locked", DateTime.Now));
			Assert.DoesNotContain(Worlds.Find(s), w => w.Path == locked);
			// A folder of worlds added in Settings that holds one that cannot be read (a drive's root):
			// the list is still made (it threw, and the start page listed no world at all).
			s.WorldFolders.Add(_home);
			Assert.DoesNotContain(Worlds.Find(s), w => w.Path == locked);
		}
		finally
		{
			File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}
	}

	// ---- Characters.

	// A character file as the game writes it, around the bits that are read: name, id, seed.
	private static byte[] Fch(string name, long id, string seed = "AbCdEfGh", byte[]? before = null)
	{
		var b = new List<byte>(before ?? new byte[] { 0x26, 0x00, 0x00, 0x00, 0x05 });
		b.Add((byte)Encoding.UTF8.GetByteCount(name));
		b.AddRange(Encoding.UTF8.GetBytes(name));
		b.AddRange(BitConverter.GetBytes(id));
		b.Add((byte)seed.Length);
		b.AddRange(Encoding.ASCII.GetBytes(seed));
		b.AddRange(new byte[16]);
		return b.ToArray();
	}

	[Fact]
	public void CharactersAreReadFromTheGamesFolders()
	{
		string chars = Local(".config", "unity3d", "IronGate", "Valheim", "characters_local");
		Directory.CreateDirectory(chars);
		File.WriteAllBytes(Path.Combine(chars, "ragnar.fch"), Fch("Ragnar", 1234567890123));
		// The same character twice (a backup copy) counts once.
		File.WriteAllBytes(Path.Combine(chars, "ragnar_copy.fch"), Fch("ragnar_copy", 1234567890123));
		File.WriteAllBytes(Path.Combine(chars, "junk.fch"), new byte[] { 1, 2, 3 });
		var list = Characters.Local();
		var r = Assert.Single(list);
		Assert.Equal("Ragnar", r.Name);
		Assert.Equal(1234567890123, r.Id);
		Assert.Contains(Characters.Roots(), d => d == chars);
		Assert.Contains(Characters.Roots(), d => d.EndsWith(Path.Combine("Valheim", "characters")));
	}

	[Fact]
	public void ACharacterFileThatCannotBeReadIsPassedOver()
	{
		if (OperatingSystem.IsWindows() || Environment.UserName == "root")
		{
			return;
		}
		string chars = Local(".config", "unity3d", "IronGate", "Valheim", "characters_local");
		Directory.CreateDirectory(chars);
		File.WriteAllBytes(Path.Combine(chars, "secret.fch"), Fch("Secret", 9));
		File.SetUnixFileMode(Path.Combine(chars, "secret.fch"), UnixFileMode.None);
		File.WriteAllBytes(Path.Combine(chars, "open.fch"), Fch("Open", 10));
		Assert.Equal(new[] { 10L }, Characters.Local().Select(c => c.Id).ToArray());
	}

	[Fact]
	public void ACharacterFileIsReadOnlyWhereNameIdAndSeedFit()
	{
		Assert.Equal(new Characters.Character("Bjorn", 42), Characters.Read(Fch("Bjorn", 42), "bjorn"));
		// The name in the file may differ in case from the file name.
		Assert.Equal("BJORN", Characters.Read(Fch("BJORN", 42), "bjorn")!.Name);
		Assert.Null(Characters.Read(Fch("Bjorn", 0), "bjorn"));
		Assert.Null(Characters.Read(Fch("Bjorn", 42, seed: "bad\u0001seed"), "bjorn"));
		Assert.Null(Characters.Read(Fch("Bjorn", 42, seed: new string('s', 40)), "bjorn"));
		Assert.Null(Characters.Read(Fch("Bjorn", 42), "freya"));
		Assert.Null(Characters.Read(Fch("Bjorn", 42), ""));
		Assert.Null(Characters.Read(Fch("Bjorn", 42), new string('x', 200)));
		// A first match that does not fit (no id) is skipped for a later one that does.
		var twice = Fch("Bjorn", 0).Concat(Fch("Bjorn", 77)).ToArray();
		Assert.Equal(77, Characters.Read(twice, "bjorn")!.Id);
	}

	[Fact]
	public void TheDefaultBuilderIsThisComputersCharacterWhenTheWorldHasNone()
	{
		using var w = new TempWorld();
		WorldSave world = w.Load();
		if (world.TopBuilder != 0)
		{
			Assert.Equal(world.TopBuilder, Builders.Default(world));
			return;
		}
		long expected = world.PlayerNames.Count > 0 ? world.PlayerNames.Keys.First() : Builders.Unknown;
		Assert.Equal(expected, Builders.Default(world));
		string chars = Local(".config", "unity3d", "IronGate", "Valheim", "characters_local");
		Directory.CreateDirectory(chars);
		File.WriteAllBytes(Path.Combine(chars, "freya.fch"), Fch("Freya", 555));
		Assert.Equal(555, Builders.Default(world));
		var me = Assert.Single(Builders.Players(world), p => p.Id == 555);
		Assert.True(me.Local);
		Assert.Equal("Freya (this computer)", me.Label);
	}

	// ---- The game's BepInEx and the plugin.

	private static void Plugin(string bepInEx, int? port = null, string? token = null)
	{
		Directory.CreateDirectory(Path.Combine(bepInEx, "plugins", "Tie-WorldEditorBridge"));
		File.WriteAllText(Path.Combine(bepInEx, "plugins", "Tie-WorldEditorBridge", "WorldEditorBridge.dll"), "");
		if (port != null)
		{
			Directory.CreateDirectory(Path.Combine(bepInEx, "config"));
			File.WriteAllText(Path.Combine(bepInEx, "config", LocalGame.ConfigName), $"[Http]\nPort = {port}\nToken = {token}\n");
		}
	}

	[Fact]
	public void TheGameAndModManagerProfilesAreSearched()
	{
		LocalGame.SearchDefaultPlaces = true;
		// A Valheim folder (it must look like the game's), and profiles of r2modman (native and Flatpak)
		// and the Thunderstore Mod Manager.
		string valheim = FakeValheim();
		Plugin(Path.Combine(valheim, "BepInEx"), 5301, "game");
		Plugin(Local(".config", "r2modmanPlus-local", "Valheim", "profiles", "Default", "BepInEx"), 5302, "r2");
		Plugin(Local(".var", "app", "com.github.ebkr.r2modman", "config", "r2modmanPlus-local", "Valheim", "profiles", "Flat", "BepInEx"), 5303, "flat");
		Plugin(Local(".config", "Thunderstore Mod Manager", "DataFolder", "Valheim", "profiles", "Tsmm", "BepInEx"), 5304, "ts");
		// A BepInEx folder given in Settings, named BepInEx itself.
		Plugin(Local("custom", "BepInEx"), 5305, "mine");
		var settings = new AppSettings { ValheimPath = valheim, BepInExFolders = { Local("custom", "BepInEx") } };
		var folders = LocalGame.BepInExFolders(settings).ToList();
		Assert.Contains(folders, f => f.Where == "Valheim folder");
		Assert.Contains(folders, f => f.Where == "your folder");
		// r2modman's folder is reached twice on Linux (application data is ~/.config): listed once.
		Assert.Single(folders, f => f.Where == "r2modman profile \"Default\"");
		Assert.Contains(folders, f => f.Where == "r2modman profile \"Flat\"");
		Assert.Contains(folders, f => f.Where == "Thunderstore Mod Manager profile \"Tsmm\"");
		var bridges = LocalGame.FindBridges(settings, out bool bepInEx, out bool plugin);
		Assert.True(bepInEx && plugin);
		Assert.Equal(new[] { "flat", "game", "mine", "r2", "ts" }, bridges.Select(b => b.Token).Order().ToArray());
	}

	[Fact]
	public void AFolderListedThatDoesNotExistIsPassedOver()
	{
		LocalGame.SearchDefaultPlaces = true;
		var settings = new AppSettings { ValheimPath = FakeValheim(), BepInExFolders = { Local("nowhere") } };
		Assert.Empty(LocalGame.FindBridges(settings, out bool bepInEx, out bool plugin));
		Assert.False(bepInEx || plugin);
	}

	private sealed class Status(int code, string body) : IDisposable
	{
		private readonly HttpListener _l = new();
		public int Port { get; } = TerrainEditor.Desktop.Tests.FakeGame.FreePort();

		public Status Start()
		{
			_l.Prefixes.Add($"http://127.0.0.1:{Port}/");
			_l.Start();
			_ = Task.Run(async () =>
			{
				while (_l.IsListening)
				{
					try
					{
						var ctx = await _l.GetContextAsync();
						ctx.Response.StatusCode = code;
						await ctx.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(body));
						ctx.Response.Close();
					}
					catch (Exception)
					{
						return;
					}
				}
			});
			return this;
		}

		public void Dispose() => _l.Close();
	}

	[Fact]
	public async Task TheStartPagesStateFollowsWhatIsInstalledAndRunning()
	{
		string bep = Local("profile", "BepInEx");
		var settings = new AppSettings { BepInExFolders = { Local("profile") } };
		async Task<string> State() => (string)(await LocalGame.Status(settings)).GetType().GetProperty("state")!.GetValue(await LocalGame.Status(settings))!;
		Assert.Equal("no-bepinex", await State());
		Directory.CreateDirectory(Path.Combine(bep, "core"));
		Assert.Equal("no-plugin", await State());
		Plugin(bep);
		Assert.Equal("plugin-not-started", await State());
		using var refusing = new Status(401, "no").Start();
		Plugin(bep, refusing.Port, "t");
		Assert.Equal("installed", await State());
		using var running = new Status(200, "{\"world\":\"Midgard\",\"players\":3}").Start();
		Plugin(bep, running.Port, "t");
		var status = await LocalGame.Status(settings);
		var t = status.GetType();
		Assert.Equal("running", t.GetProperty("state")!.GetValue(status));
		Assert.Equal("Midgard", t.GetProperty("world")!.GetValue(status));
		Assert.Equal(3, t.GetProperty("players")!.GetValue(status));
		Assert.Equal("\"profile\"", t.GetProperty("where")!.GetValue(status));
		// A status without the world and players: still running, with blanks.
		using var bare = new Status(200, "{}").Start();
		Plugin(bep, bare.Port, "t");
		var r = await LocalGame.FindRunning(LocalGame.FindBridges(settings, out _, out _));
		Assert.Equal("", r!.World);
		Assert.Equal(0, r.Players);
	}

	[Fact]
	public void TheEditorKnowsWhenAModManagerInstalledIt()
	{
		Assert.Equal(Local("p", "BepInEx"), LocalGame.InstalledIn(Local("p", "BepInEx", "plugins", "Tie-ValheimWorldEditor")));
		Assert.Null(LocalGame.InstalledIn(Local("plugins", "x")));
	}

	// ---- Free ports.

	[Fact]
	public void AFreePortIsTheFirstFreeOneOrAnyWhenFiftyAreTaken()
	{
		int from = TerrainEditor.Desktop.Tests.FakeGame.FreePort();
		var taken = new TcpListener(IPAddress.Loopback, from);
		taken.Start();
		try
		{
			Assert.NotEqual(from, Places.FreePort(from));
		}
		finally
		{
			taken.Stop();
		}
		// Fifty ports in a row taken: any free port.
		var held = new List<TcpListener>();
		int start = 0;
		for (int attempt = 0; attempt < 20 && held.Count < 50; attempt++)
		{
			foreach (var l in held)
			{
				l.Stop();
			}
			held.Clear();
			start = 20000 + Random.Shared.Next(0, 30000);
			for (int p = start; p < start + 50; p++)
			{
				try
				{
					var l = new TcpListener(IPAddress.Loopback, p);
					l.Start();
					held.Add(l);
				}
				catch (SocketException)
				{
					break;
				}
			}
		}
		try
		{
			Assert.Equal(50, held.Count);
			int port = Places.FreePort(start);
			Assert.True(port < start || port >= start + 50, $"{port}");
		}
		finally
		{
			foreach (var l in held)
			{
				l.Stop();
			}
		}
	}

	// ---- The SSH tunnel, before any SSH session.

	private const string TestKey = """
		-----BEGIN OPENSSH PRIVATE KEY-----
		b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZW
		QyNTUxOQAAACAg44UvW7VFAN+bXvvnWBPEivp+ps3HWYGmpGk9ygtZuAAAAJA6CD1vOgg9
		bwAAAAtzc2gtZWQyNTUxOQAAACAg44UvW7VFAN+bXvvnWBPEivp+ps3HWYGmpGk9ygtZuA
		AAAECPU9rm+e50SHVFbmrQI617lWlEMopN+F0wBk3pjPskACDjhS9btUUA35te++dYE8SK
		+n6mzcdZgaakaT3KC1m4AAAACHZ3ZS10ZXN0AQIDBAU=
		-----END OPENSSH PRIVATE KEY-----
		""";

	// A throwaway key protected by the passphrase "secret".
	private const string LockedKey = """
		-----BEGIN OPENSSH PRIVATE KEY-----
		b3BlbnNzaC1rZXktdjEAAAAACmFlczI1Ni1jdHIAAAAGYmNyeXB0AAAAGAAAABC0fpy3Ip
		ZdcSSXg9LC86e/AAAAGAAAAAEAAAAzAAAAC3NzaC1lZDI1NTE5AAAAILLzgyUPSgcuWShk
		4Gop8rD88o2Z7CaxzxWsA+i7yyIBAAAAkKN9z2MvlSVqrCKCJcXXAfmDx/NnGU1FUO7qJk
		aQkbrHSUe5RaDoug3lXkmPIcgdw2vKFLdNy/psmJiZ/Ru/7BbXPKLullV6vRNeFnlkpUgZ
		ju2ucw6arztdSFICJ/b4AXhgzabnNuRcueljghRupa/l053rR2uOb8E/6J1aFfhLJNF8DZ
		axxA5kCHLSqVXJ2Q==
		-----END OPENSSH PRIVATE KEY-----
		""";

	private static Tunnel.Request Req(string host, int port, string user = "valheim", string? password = null, string? key = null, string? phrase = null) =>
		new(host, port, user, password, key, phrase, "token", null, false, null);

	[Fact]
	public async Task TheTunnelNeedsAUserAndAnAddress()
	{
		Assert.StartsWith("Enter the server as user@address", (await Tunnel.Start(Req("", 22))).Error);
		Assert.StartsWith("Enter the server as user@address", (await Tunnel.Start(Req("server.example", 22, user: " "))).Error);
		Assert.StartsWith("Enter the server as user@address", (await Tunnel.Start(Req("@server.example", 22))).Error);
		Assert.False(Tunnel.Open);
	}

	[Fact]
	public async Task TheTunnelNeedsAPasswordOrAKeyItCanRead()
	{
		// No key given and none in this home's .ssh: a password or a key is needed.
		Assert.StartsWith("Enter the password, or choose your SSH key file", (await Tunnel.Start(Req("server.example", 22))).Error);
		// Keys that cannot be read: missing (also through ~), not a key, locked with another passphrase.
		Assert.StartsWith("The key file could not be read", (await Tunnel.Start(Req("server.example", 22, key: Local("missing")))).Error);
		Assert.StartsWith("The key file could not be read", (await Tunnel.Start(Req("server.example", 22, key: "~/missing"))).Error);
		File.WriteAllText(Local("notakey"), "hello");
		Assert.StartsWith("The key file could not be read", (await Tunnel.Start(Req("server.example", 22, key: $"\"{Local("notakey")}\""))).Error);
		File.WriteAllText(Local("locked"), LockedKey);
		Assert.StartsWith("The key file could not be read", (await Tunnel.Start(Req("server.example", 22, key: Local("locked"), phrase: "wrong"))).Error);
	}

	[Fact]
	public async Task ATunnelToAClosedPortSaysNothingAnswers()
	{
		File.WriteAllText(Local("key"), TestKey);
		int port = TerrainEditor.Desktop.Tests.FakeGame.FreePort();
		var r = await Tunnel.Start(Req("valheim@127.0.0.1", port, user: "ignored", key: Local("key")));
		Assert.Equal($"No answer from 127.0.0.1:{port}. Check the address and that SSH is enabled on the server.", r.Error);
		Assert.Equal(0, r.LocalPort);
		// The usual key files of this home are used when no key is given (here the test key).
		Directory.CreateDirectory(Local(".ssh"));
		File.WriteAllText(Local(".ssh", "id_ed25519"), TestKey);
		File.WriteAllText(Local(".ssh", "id_rsa"), LockedKey);
		Assert.StartsWith("No answer from", (await Tunnel.Start(Req("127.0.0.1", port))).Error);
		// A password alone is enough to try (always a closed port here: never a real SSH server).
		File.Delete(Local(".ssh", "id_ed25519"));
		File.Delete(Local(".ssh", "id_rsa"));
		Assert.StartsWith("No answer from", (await Tunnel.Start(Req("127.0.0.1", port, password: "pw"))).Error);
	}

	[Fact]
	public async Task AServerThatDoesNotSpeakSshIsReported()
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		int port = ((IPEndPoint)listener.LocalEndpoint).Port;
		_ = Task.Run(async () =>
		{
			using var c = await listener.AcceptTcpClientAsync();
			var s = c.GetStream();
			await s.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\n\r\n"));
			await Task.Delay(200);
		});
		try
		{
			File.WriteAllText(Local("key"), TestKey);
			var r = await Tunnel.Start(Req("127.0.0.1", port, key: Local("key")));
			Assert.NotNull(r.Error);
			Assert.True(r.Error!.StartsWith("Could not connect") || r.Error.StartsWith("No answer from"), r.Error);
		}
		finally
		{
			listener.Stop();
		}
	}

	[Fact]
	public void WithoutATunnelThereIsNothingToDiagnoseOrClose()
	{
		Tunnel.Close();
		Tunnel.Close();
		Assert.False(Tunnel.Open);
		Assert.Null(Tunnel.Diagnose("/home/valheim/server"));
		Assert.Null(Tunnel.Diagnose(null));
	}
}
