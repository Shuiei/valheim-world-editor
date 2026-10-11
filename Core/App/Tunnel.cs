using System.Security.Cryptography;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace TerrainEditor.App;

/// <summary>
/// The SSH tunnel to a dedicated server, made by the app itself (SSH.NET), so players do not need
/// an ssh command or PuTTY: connects with a password or a key and forwards a free local port to the
/// WorldEditorBridge plugin on the server. The plugin's token is entered by the user. One tunnel at a time.
/// </summary>
public static class Tunnel
{
    /// <summary>What the player entered to connect.</summary>
    /// <param name="Host">The server's address, or user@address.</param>
    /// <param name="Port">The SSH port; 0 or less: 22.</param>
    /// <param name="User">The account to log in with (unless given in <paramref name="Host"/>).</param>
    /// <param name="Password">The password, also answering keyboard-interactive prompts.</param>
    /// <param name="KeyPath">A private key file (~ for the home folder); none: the usual ~/.ssh keys are tried.</param>
    /// <param name="Passphrase">The key's passphrase, if it has one.</param>
    /// <param name="Token">The plugin's token; empty: the saved server's.</param>
    /// <param name="BridgePort">
    /// The plugin's port; null: from the server's plugin settings, the saved server or 5182.
    /// </param>
    /// <param name="SavePassword">Whether to keep the password in servers.cfg.</param>
    /// <param name="Name">The name to save the server under.</param>
    /// <param name="GameFolder">The server's Valheim folder, for the plugin's port and precise errors.</param>
    public sealed record Request(string Host, int Port, string User, string? Password, string? KeyPath, string? Passphrase, string? Token, int? BridgePort, bool SavePassword, string? Name, string? GameFolder = null);

    /// <summary>How connecting went.</summary>
    /// <param name="LocalPort">The local port forwarded to the plugin; 0 on failure.</param>
    /// <param name="Token">The plugin's token to send; empty on failure.</param>
    /// <param name="Error">Why it failed, for the player; null on success.</param>
    /// <param name="Fingerprint">The server's host key (SHA256:base64), null on failure.</param>
    /// <param name="Server">What to save in servers.cfg once the plugin has answered (the caller saves it).</param>
    public sealed record Result(int LocalPort, string Token, string? Error, string? Fingerprint, ServerConfig.Server? Server = null);

    /// <summary>The open SSH connection, if any.</summary>
    private static SshClient? _client;

    /// <summary>Its local port forward to the plugin.</summary>
    private static ForwardedPortLocal? _forward;

    /// <summary>Whether a tunnel is connected.</summary>
    public static bool Open => _client?.IsConnected == true;

    /// <summary>Stops the forward and disconnects, ignoring errors.</summary>
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

    /// <summary>
    /// Closes any tunnel, connects (refusing a host key different from the one saved for that address
    /// and port) and forwards a free local port from 15182 up to the plugin. Failures to log in or connect
    /// come back in <see cref="Result.Error"/>.
    /// </summary>
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
                        // One that cannot be used (a passphrase, a format SSH.NET does not read, no
                        // access) is skipped: the password is still tried.
                        try
                        {
                            methods.Add(new PrivateKeyAuthenticationMethod(user, new PrivateKeyFile(k)));
                        }
                        catch (Exception ex) when (ex is SshException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
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
            kbd.AuthenticationPrompt += (_, e) => { foreach (var p in e.Prompts) { p.Response = r.Password; } };
            methods.Add(kbd);
        }
        if (methods.Count == 0)
        {
            return Fail("Enter the password, or choose your SSH key file.");
        }
        var info = new ConnectionInfo(host, port, user, [.. methods]) { Timeout = TimeSpan.FromSeconds(12) };
        var client = new SshClient(info) { KeepAliveInterval = TimeSpan.FromSeconds(30) };
        string id = $"{user}@{host}:{port}";
        // The server's key as known for this address and port, whatever the account (logging in as
        // another user is still the same server: a different key there is refused too).
        var savedServers = ServerConfig.Load();
        ServerConfig.Server? saved = savedServers.FirstOrDefault(s => s.Id == id);
        string? pinned = saved?.HostKey
            ?? savedServers.FirstOrDefault(s => s.HostKey != null && s.SshPort == port && string.Equals(s.Host, host, StringComparison.OrdinalIgnoreCase))?.HostKey;
        string? seen = null, changed = null;
        // Trust on first use: remember the server's key; refuse if it changes later.
        client.HostKeyReceived += (_, e) =>
        {
            seen = "SHA256:" + Convert.ToBase64String(SHA256.HashData(e.HostKey)).TrimEnd('=');
            if (pinned is string known && known != seen)
            {
                changed = known;
                e.CanTrust = false;
                return;
            }
            e.CanTrust = true;
        };
        try
        {
            await Task.Run(client.Connect);
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
            string cfg = gameFolder + "/BepInEx/config/";
            string text = Run(client, $"cat {Quote(cfg + LocalGame.ConfigName)} 2>/dev/null || cat {Quote(cfg + LocalGame.OldConfigName)} 2>/dev/null");
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
            return Fail("Enter the plugin's token: the Token line in BepInEx/config/Tie.WorldEditorBridge.cfg on the server.");
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

        static Result Fail(string message) => new(0, "", message, null);
    }

    /// <summary>
    /// Why the plugin does not answer, from what the server's Valheim folder holds (while connected). Null
    /// when not connected, no folder is given or the check did not answer.
    /// </summary>
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
            $"elif [ ! -f {q}/BepInEx/config/{LocalGame.ConfigName} ] && [ ! -f {q}/BepInEx/config/{LocalGame.OldConfigName} ]; then echo nocfg; else echo installed; fi").Trim();
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

    /// <summary>A path quoted for the server's shell (single quotes).</summary>
    private static string Quote(string path) => "'" + path.Replace("'", "'\\''") + "'";

    /// <summary>A shell command's output on the server (15 s at most); empty when it fails.</summary>
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
