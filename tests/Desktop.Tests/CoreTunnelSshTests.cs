using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// A real SSH server for the tunnel's tests: OpenSSH's sshd run as the test's own user on a free local
// port, with its own host key, its own authorized keys and its own settings in a temporary folder
// (never ~/.ssh, never port 22). Without sshd the tests skip, unless VWE_NEED_SSHD=1 (CI) makes that a
// failure.
public sealed class LocalSshd : IDisposable
{
	private readonly Process? _p;
	public string Dir { get; } = Path.Combine(Path.GetTempPath(), "vwe-sshd-" + Guid.NewGuid().ToString("N")[..8]);
	public int Port { get; }
	public string User { get; } = Environment.UserName;
	public string? Why { get; }
	public string Fingerprint { get; } = "";

	// A key the server lets in, and one it does not know.
	public string Key => Path.Combine(Dir, "user");
	public string OtherKey => Path.Combine(Dir, "other");

	public LocalSshd()
	{
		string? sshd = new[] { "/usr/sbin/sshd", "/usr/bin/sshd", "/sbin/sshd" }.FirstOrDefault(File.Exists);
		string? keygen = new[] { "/usr/bin/ssh-keygen", "/usr/sbin/ssh-keygen", "/bin/ssh-keygen" }.FirstOrDefault(File.Exists);
		if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() || sshd == null || keygen == null)
		{
			Why = "OpenSSH's sshd and ssh-keygen are needed";
			return;
		}
		Directory.CreateDirectory(Dir);
		foreach (string k in new[] { "host", "user", "other" })
		{
			Exec(keygen, "-q", "-t", "ed25519", "-N", "", "-C", "vwe-test", "-f", Path.Combine(Dir, k));
		}
		File.Copy(Path.Combine(Dir, "user.pub"), Path.Combine(Dir, "authorized_keys"));
		// "256 SHA256:... comment (ED25519)"
		Fingerprint = Exec(keygen, "-l", "-f", Path.Combine(Dir, "host.pub")).Split(' ')[1];
		Port = FakeGame.FreePort();
		File.WriteAllText(Path.Combine(Dir, "sshd_config"), $"""
			Port {Port}
			ListenAddress 127.0.0.1
			HostKey {Path.Combine(Dir, "host")}
			AuthorizedKeysFile {Path.Combine(Dir, "authorized_keys")}
			PidFile {Path.Combine(Dir, "sshd.pid")}
			UsePAM no
			StrictModes no
			PubkeyAuthentication yes
			PasswordAuthentication no
			KbdInteractiveAuthentication no
			PermitRootLogin prohibit-password
			AllowTcpForwarding yes
			LogLevel ERROR
			""");
		var psi = new ProcessStartInfo(sshd) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
		foreach (string a in new[] { "-D", "-e", "-f", Path.Combine(Dir, "sshd_config") })
		{
			psi.ArgumentList.Add(a);
		}
		_p = Process.Start(psi)!;
		var log = new StringBuilder();
		_p.ErrorDataReceived += (_, e) => { lock (log) log.AppendLine(e.Data); };
		_p.BeginErrorReadLine();
		var watch = Stopwatch.StartNew();
		while (watch.Elapsed < TimeSpan.FromSeconds(15))
		{
			if (_p.HasExited)
			{
				break;
			}
			try
			{
				using var c = new TcpClient();
				c.Connect(IPAddress.Loopback, Port);
				return;
			}
			catch (SocketException)
			{
				Thread.Sleep(100);
			}
		}
		lock (log)
		{
			Why = "sshd did not start: " + log.ToString().Trim();
		}
		Dispose();
		_p = null;
	}

	public bool Available => _p != null;

	// Skips the test without sshd (fails instead when CI asks for it).
	public void Need()
	{
		if (!Available && Environment.GetEnvironmentVariable("VWE_NEED_SSHD") == "1")
		{
			Assert.Fail(Why);
		}
		Assert.SkipUnless(Available, Why ?? "");
	}

	private static string Exec(string file, params string[] args)
	{
		var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
		foreach (string a in args)
		{
			psi.ArgumentList.Add(a);
		}
		using var p = Process.Start(psi)!;
		string output = p.StandardOutput.ReadToEnd();
		p.WaitForExit();
		return output;
	}

	public void Dispose()
	{
		try
		{
			if (_p is { HasExited: false })
			{
				_p.Kill();
				_p.WaitForExit(5000);
			}
		}
		catch (Exception)
		{
		}
		try
		{
			Directory.Delete(Dir, true);
		}
		catch (Exception)
		{
		}
	}
}

// The tunnel against a real SSH server (LocalSshd): logging in with a key, the forward to the plugin's
// port, trusting the server's key on first use and refusing it once it changes, the saved server's
// token and port, the plugin's port read from the server's game folder, and every diagnosis of a
// plugin that does not answer.
[Collection("DataDir")]
public sealed class CoreTunnelSshTests : IClassFixture<LocalSshd>, IDisposable
{
	private readonly LocalSshd _ssh;
	private readonly string? _servers = ServerConfig.PathOverride;
	private readonly string? _home = Environment.GetEnvironmentVariable("HOME");
	private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-tunnel-" + Guid.NewGuid().ToString("N")[..8]);
	private readonly TcpListener _bridge = new(IPAddress.Loopback, 0);

	public CoreTunnelSshTests(LocalSshd ssh)
	{
		_ssh = ssh;
		Directory.CreateDirectory(_dir);
		ServerConfig.PathOverride = Path.Combine(_dir, "servers.cfg");
		// A home with no keys: the user's own ~/.ssh is never read.
		Directory.CreateDirectory(Path.Combine(_dir, "empty-home"));
		Environment.SetEnvironmentVariable("HOME", Path.Combine(_dir, "empty-home"));
		// The stand-in plugin: says hello to whoever comes through the tunnel.
		_bridge.Start();
		_ = Task.Run(async () =>
		{
			try
			{
				while (true)
				{
					using var c = await _bridge.AcceptTcpClientAsync();
					await c.GetStream().WriteAsync(Encoding.ASCII.GetBytes("hello from the plugin\n"));
				}
			}
			catch (Exception)
			{
			}
		});
	}

	private int BridgePort => ((IPEndPoint)_bridge.LocalEndpoint).Port;

	public void Dispose()
	{
		Tunnel.Close();
		_bridge.Stop();
		ServerConfig.PathOverride = _servers;
		Environment.SetEnvironmentVariable("HOME", _home);
		try
		{
			Directory.Delete(_dir, true);
		}
		catch (Exception)
		{
		}
	}

	private Tunnel.Request Req(string? key = null, string? token = "tok", int? bridge = null, string? password = null, bool savePassword = false, string? name = null, string? folder = null) =>
		new("127.0.0.1", _ssh.Port, _ssh.User, password, key ?? _ssh.Key, null, token, bridge, savePassword, name, folder);

	private static string ReadThrough(int localPort)
	{
		using var c = new TcpClient();
		c.Connect(IPAddress.Loopback, localPort);
		c.ReceiveTimeout = 10000;
		return new StreamReader(c.GetStream()).ReadLine() ?? "";
	}

	[Fact]
	public async Task AKeyLogsInAndTheTunnelReachesThePlugin()
	{
		_ssh.Need();
		var r = await Tunnel.Start(Req(bridge: BridgePort, password: "unused", savePassword: false));
		Assert.Null(r.Error);
		Assert.True(Tunnel.Open);
		Assert.Equal(_ssh.Fingerprint, r.Fingerprint);
		Assert.Equal("tok", r.Token);
		Assert.Equal("hello from the plugin", ReadThrough(r.LocalPort));
		// What to remember: the server's key, the plugin's port and token, no password unless asked.
		var s = r.Server!;
		Assert.Equal("127.0.0.1", s.Name);
		Assert.Equal(("127.0.0.1", _ssh.Port, _ssh.User), (s.Host, s.SshPort, s.User));
		Assert.Equal(_ssh.Fingerprint, s.HostKey);
		Assert.Equal(BridgePort, s.BridgePort);
		Assert.Equal(_ssh.Key, s.KeyFile);
		Assert.Null(s.Password);
		Assert.Null(s.GameFolder);
		Tunnel.Close();
		Assert.False(Tunnel.Open);
		// Asked to: the password is kept, and the name given.
		r = await Tunnel.Start(Req(bridge: BridgePort, password: "pw", savePassword: true, name: " My server "));
		Assert.Null(r.Error);
		Assert.Equal("pw", r.Server!.Password);
		Assert.Equal("My server", r.Server.Name);
	}

	[Fact]
	public async Task ASecondTunnelReplacesTheFirst()
	{
		_ssh.Need();
		Assert.Null((await Tunnel.Start(Req(bridge: BridgePort))).Error);
		var second = await Tunnel.Start(Req(bridge: BridgePort));
		Assert.Null(second.Error);
		Assert.True(Tunnel.Open);
		Assert.Equal("hello from the plugin", ReadThrough(second.LocalPort));
	}

	[Fact]
	public async Task AKeyTheServerDoesNotKnowOrAPasswordItDoesNotTakeIsRefused()
	{
		_ssh.Need();
		var r = await Tunnel.Start(Req(key: _ssh.OtherKey));
		Assert.Equal("The server refused the login: check the user name and the password or key.", r.Error);
		Assert.False(Tunnel.Open);
		// This server takes keys only.
		r = await Tunnel.Start(Req(key: "", password: "pw"));
		Assert.Equal("The server refused the login: check the user name and the password or key.", r.Error);
	}

	[Fact]
	public async Task TheUsualKeysOfTheHomeAndATildePathAreUsed()
	{
		_ssh.Need();
		string home = Path.Combine(_dir, "home");
		Directory.CreateDirectory(Path.Combine(home, ".ssh"));
		File.Copy(_ssh.Key, Path.Combine(home, ".ssh", "id_ed25519"));
		// One the editor cannot read is skipped (it stopped the login with "The key file could not be read").
		File.WriteAllText(Path.Combine(home, ".ssh", "id_ecdsa"), "not a key");
		File.Copy(_ssh.Key, Path.Combine(home, "mykey"));
		Environment.SetEnvironmentVariable("HOME", home);
		var r = await Tunnel.Start(Req(key: ""));
		Assert.Null(r.Error);
		Assert.Null(r.Server!.KeyFile);
		r = await Tunnel.Start(Req(key: "~/mykey"));
		Assert.Null(r.Error);
		Assert.Equal("~/mykey", r.Server!.KeyFile);
	}

	[Fact]
	public async Task TheServersKeyIsTrustedOnceAndRefusedWhenItChanges()
	{
		_ssh.Need();
		string id = $"{_ssh.User}@127.0.0.1:{_ssh.Port}";
		ServerConfig.Remember(new ServerConfig.Server { Name = "Saved", Host = "127.0.0.1", SshPort = _ssh.Port, User = _ssh.User, Token = "saved-token", BridgePort = BridgePort, HostKey = _ssh.Fingerprint, GameFolder = "/nowhere" });
		Assert.Contains(ServerConfig.Load(), s => s.Id == id);
		// The same key: the saved token, port, name and folder fill what the request leaves out.
		var r = await Tunnel.Start(Req(token: " ", bridge: null));
		Assert.Null(r.Error);
		Assert.Equal("saved-token", r.Token);
		Assert.Equal(BridgePort, r.Server!.BridgePort);
		Assert.Equal("Saved", r.Server.Name);
		Assert.Equal("/nowhere", r.Server.GameFolder);
		Assert.Equal("hello from the plugin", ReadThrough(r.LocalPort));
		// Another key than the one remembered: refused, saying both.
		ServerConfig.Remember(new ServerConfig.Server { Name = "Saved", Host = "127.0.0.1", SshPort = _ssh.Port, User = _ssh.User, HostKey = "SHA256:somethingelse" });
		r = await Tunnel.Start(Req());
		Assert.StartsWith($"The server's identity changed (it was SHA256:somethingelse, now {_ssh.Fingerprint}).", r.Error);
		Assert.False(Tunnel.Open);
		// Known under another account only: the same server, so still refused (it was trusted afresh).
		ServerConfig.Forget(id);
		ServerConfig.Remember(new ServerConfig.Server { Name = "Other account", Host = "127.0.0.1", SshPort = _ssh.Port, User = "someone-else", HostKey = "SHA256:somethingelse" });
		r = await Tunnel.Start(Req());
		Assert.StartsWith("The server's identity changed (it was SHA256:somethingelse", r.Error);
	}

	[Fact]
	public async Task WithoutATokenTheTunnelIsNotOpened()
	{
		_ssh.Need();
		var r = await Tunnel.Start(Req(token: null));
		Assert.StartsWith("Enter the plugin's token", r.Error);
		Assert.False(Tunnel.Open);
	}

	[Theory]
	[InlineData(LocalGame.ConfigName, false)]
	[InlineData(LocalGame.OldConfigName, true)]
	public async Task ThePluginsPortIsReadFromTheServersGameFolder(string config, bool slash)
	{
		_ssh.Need();
		string game = Path.Combine(_dir, "it's a server");
		Directory.CreateDirectory(Path.Combine(game, "BepInEx", "config"));
		File.WriteAllText(Path.Combine(game, "BepInEx", "config", config), $"[Bridge]\nToken = never read\n Port = {BridgePort}\n");
		var r = await Tunnel.Start(Req(folder: slash ? game + "/ " : game));
		Assert.Null(r.Error);
		Assert.Equal(BridgePort, r.Server!.BridgePort);
		Assert.Equal(game, r.Server.GameFolder);
		Assert.Equal("tok", r.Token);
		Assert.Equal("hello from the plugin", ReadThrough(r.LocalPort));
		// A port given wins over the folder's.
		r = await Tunnel.Start(Req(folder: game, bridge: 4321));
		Assert.Equal(4321, r.Server!.BridgePort);
		// No settings there: the usual port.
		File.Delete(Path.Combine(game, "BepInEx", "config", config));
		r = await Tunnel.Start(Req(folder: game));
		Assert.Equal(5182, r.Server!.BridgePort);
	}

	[Fact]
	public async Task EveryReasonThePluginDoesNotAnswerIsDiagnosed()
	{
		_ssh.Need();
		Assert.Null((await Tunnel.Start(Req(bridge: BridgePort))).Error);
		string game = Path.Combine(_dir, "valheim");
		Assert.Null(Tunnel.Diagnose(" "));
		Assert.Equal($"The folder {game} does not exist on the server: check the server's Valheim folder.", Tunnel.Diagnose(game + "/"));
		Directory.CreateDirectory(game);
		Assert.Equal($"BepInEx is not installed in {game} on the server: install BepInExPack for Valheim there.", Tunnel.Diagnose(game));
		Directory.CreateDirectory(Path.Combine(game, "BepInEx", "plugins", "Tie-WorldEditorBridge"));
		Assert.Equal($"The plugin is not in {game}/BepInEx/plugins on the server: copy WorldEditorBridge.dll there and restart the server.", Tunnel.Diagnose(game));
		File.WriteAllText(Path.Combine(game, "BepInEx", "plugins", "Tie-WorldEditorBridge", "WorldEditorBridge.dll"), "");
		Assert.Equal("The plugin is installed but has never run: restart the server with BepInEx.", Tunnel.Diagnose(game));
		Directory.CreateDirectory(Path.Combine(game, "BepInEx", "config"));
		File.WriteAllText(Path.Combine(game, "BepInEx", "config", LocalGame.OldConfigName), "");
		Assert.StartsWith("The plugin is installed but does not answer", Tunnel.Diagnose(game));
		File.Move(Path.Combine(game, "BepInEx", "config", LocalGame.OldConfigName), Path.Combine(game, "BepInEx", "config", LocalGame.ConfigName));
		Assert.StartsWith("The plugin is installed but does not answer", Tunnel.Diagnose(game));
	}
}
