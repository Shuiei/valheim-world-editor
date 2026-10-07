using System.Security.Cryptography;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace TerrainEditor.App;

// The SSH tunnel to a dedicated server, made by the app itself (SSH.NET), so players do not need
// an ssh command or PuTTY: connects with a password or a key and forwards a free local port to the
// WorldEditorBridge plugin on the server. The plugin's token is entered by the user.
public static class Tunnel
{
	public sealed record Request(string Host, int Port, string User, string? Password, string? KeyPath, string? Passphrase, string? Token, int? BridgePort, bool SavePassword, string? Name, string? GameFolder = null);

	// Server: what to save in servers.cfg once the plugin has answered (the caller saves it).
	public sealed record Result(int LocalPort, string Token, string? Error, string? Fingerprint, ServerConfig.Server? Server = null);

	private static SshClient? _client;

	private static ForwardedPortLocal? _forward;

	public static bool Open => _client?.IsConnected == true;

	public static void Close()
	{
		try
		{
			_forward?.Stop();
		}
		catch
		{
		}
		try
		{
			_client?.Disconnect();
		}
		catch
		{
		}
		_client?.Dispose();
		_client = null;
		_forward = null;
	}

	public static async Task<Result> Start(Request r)
	{
		Close();
		string host = r.Host.Trim();
		string user = r.User.Trim();
		// "user@host" in one field.
		if (host.Contains('@'))
		{
			user = host[..host.IndexOf('@')];
			host = host[(host.IndexOf('@') + 1)..];
		}
		int port = r.Port > 0 ? r.Port : 22;
		if (host.Length == 0 || user.Length == 0)
		{
			return Fail("Enter the server as user@address (the account you log in to the server with).");
		}
		var methods = new List<AuthenticationMethod>();
		try
		{
			if (!string.IsNullOrWhiteSpace(r.KeyPath))
			{
				string key = r.KeyPath.Trim().Trim('"');
				if (key.StartsWith('~'))
				{
					key = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + key[1..];
				}
				methods.Add(new PrivateKeyAuthenticationMethod(user, string.IsNullOrEmpty(r.Passphrase) ? new PrivateKeyFile(key) : new PrivateKeyFile(key, r.Passphrase)));
			}
			else
			{
				// No key given: the usual key files, if any (no passphrase).
				foreach (string name in new[] { "id_ed25519", "id_ecdsa", "id_rsa" })
				{
					string k = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", name);
					if (File.Exists(k))
					{
						try
						{
							methods.Add(new PrivateKeyAuthenticationMethod(user, new PrivateKeyFile(k)));
						}
						catch (SshPassPhraseNullOrEmptyException)
						{
						}
					}
				}
			}
		}
		catch (Exception ex) when (ex is SshException or IOException or UnauthorizedAccessException)
		{
			return Fail("The key file could not be read: " + ex.Message);
		}
		if (!string.IsNullOrEmpty(r.Password))
		{
			methods.Add(new PasswordAuthenticationMethod(user, r.Password));
			var kbd = new KeyboardInteractiveAuthenticationMethod(user);
			kbd.AuthenticationPrompt += (_, e) => { foreach (var p in e.Prompts) p.Response = r.Password; };
			methods.Add(kbd);
		}
		if (methods.Count == 0)
		{
			return Fail("Enter the password, or choose your SSH key file.");
		}
		var info = new Renci.SshNet.ConnectionInfo(host, port, user, methods.ToArray()) { Timeout = TimeSpan.FromSeconds(12) };
		var client = new SshClient(info) { KeepAliveInterval = TimeSpan.FromSeconds(30) };
		string id = $"{user}@{host}:{port}";
		ServerConfig.Server? saved = ServerConfig.Load().FirstOrDefault(s => s.Id == id);
		string? seen = null, changed = null;
		// Trust on first use: remember the server's key; refuse if it changes later.
		client.HostKeyReceived += (_, e) =>
		{
			seen = "SHA256:" + Convert.ToBase64String(SHA256.HashData(e.HostKey)).TrimEnd('=');
			if (saved?.HostKey is string known && known != seen)
			{
				changed = known;
				e.CanTrust = false;
				return;
			}
			e.CanTrust = true;
		};
		try
		{
			await Task.Run(() => client.Connect());
		}
		catch (Exception ex)
		{
			client.Dispose();
			if (changed != null)
			{
				return Fail($"The server's identity changed (it was {changed}, now {seen}). If you reinstalled the server this is expected: empty its HostKey line in {ServerConfig.FilePath}; otherwise do not connect.");
			}
			return Fail(ex switch
			{
				SshAuthenticationException => "The server refused the login: check the user name and the password or key.",
				SshOperationTimeoutException or System.Net.Sockets.SocketException => $"No answer from {host}:{port}. Check the address and that SSH is enabled on the server.",
				_ => "Could not connect: " + ex.Message,
			});
		}
		// The token is always the user's to give (typed, or saved with the server): never looked up on the server.
		string? gameFolder = string.IsNullOrWhiteSpace(r.GameFolder) ? saved?.GameFolder : r.GameFolder.Trim().TrimEnd('/');
		// The plugin's port from its settings in the server's Valheim folder, when given (never the token).
		int? folderPort = null;
		if (gameFolder != null && r.BridgePort == null)
		{
			string text = Run(client, $"cat {Quote(gameFolder + "/BepInEx/config/local.worldeditorbridge.cfg")} 2>/dev/null");
			if (System.Text.RegularExpressions.Regex.Match(text, @"^\s*Port\s*=\s*(\d+)", System.Text.RegularExpressions.RegexOptions.Multiline) is { Success: true } pm)
			{
				folderPort = int.Parse(pm.Groups[1].Value);
			}
		}
		int bridgePort = r.BridgePort ?? folderPort ?? saved?.BridgePort ?? 5182;
		string? token = string.IsNullOrWhiteSpace(r.Token) ? saved?.Token : r.Token.Trim();
		if (token == null)
		{
			client.Disconnect();
			client.Dispose();
			return Fail("Enter the plugin's token: the Token line in BepInEx/config/local.worldeditorbridge.cfg on the server.");
		}
		int local = Places.FreePort(15182);
		var forward = new ForwardedPortLocal("127.0.0.1", (uint)local, "127.0.0.1", (uint)bridgePort);
		client.AddForwardedPort(forward);
		forward.Start();
		_client = client;
		_forward = forward;
		// What to remember for next time (the password only when asked to), saved once the plugin answers.
		var remember = new ServerConfig.Server
		{
			Name = string.IsNullOrWhiteSpace(r.Name) ? saved?.Name ?? host : r.Name.Trim(),
			Host = host,
			SshPort = port,
			User = user,
			Password = r.SavePassword ? r.Password : null,
			KeyFile = string.IsNullOrWhiteSpace(r.KeyPath) ? null : r.KeyPath.Trim(),
			Token = token,
			BridgePort = bridgePort,
			HostKey = seen ?? saved?.HostKey,
			GameFolder = gameFolder,
		};
		return new Result(local, token, null, seen, remember);

		Result Fail(string message) => new(0, "", message, null);
	}

	// Why the plugin does not answer, from what the server's Valheim folder holds (while connected).
	public static string? Diagnose(string? gameFolder)
	{
		SshClient? client = _client;
		if (client == null || string.IsNullOrWhiteSpace(gameFolder))
		{
			return null;
		}
		string f = gameFolder.TrimEnd('/');
		string q = Quote(f);
		string answer = Run(client, $"if [ ! -d {q} ]; then echo nofolder; elif [ ! -d {q}/BepInEx ]; then echo nobepinex; " +
			$"elif ! find {q}/BepInEx/plugins -name WorldEditorBridge.dll 2>/dev/null | grep -q .; then echo noplugin; " +
			$"elif [ ! -f {q}/BepInEx/config/local.worldeditorbridge.cfg ]; then echo nocfg; else echo installed; fi").Trim();
		return answer switch
		{
			"nofolder" => $"The folder {f} does not exist on the server: check the server's Valheim folder.",
			"nobepinex" => $"BepInEx is not installed in {f} on the server: install BepInExPack for Valheim there.",
			"noplugin" => $"The plugin is not in {f}/BepInEx/plugins on the server: copy WorldEditorBridge.dll there and restart the server.",
			"nocfg" => "The plugin is installed but has never run: restart the server with BepInEx.",
			"installed" => "The plugin is installed but does not answer: is the server running, and started with BepInEx (not the plain start script)?",
			_ => null,
		};
	}

	private static string Quote(string path) => "'" + path.Replace("'", "'\\''") + "'";

	private static string Run(SshClient client, string command)
	{
		try
		{
			using SshCommand cmd = client.CreateCommand(command);
			cmd.CommandTimeout = TimeSpan.FromSeconds(15);
			return cmd.Execute();
		}
		catch
		{
			return "";
		}
	}
}
