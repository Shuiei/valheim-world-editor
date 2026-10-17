using System.Text;
using System.Text.RegularExpressions;

namespace TerrainEditor.App;

/// <summary>
/// Saved dedicated servers, in servers.cfg in the app's data folder: one [section] per server with
/// "Key = value" lines, like BepInEx configs, so it can also be edited by hand. Written after the
/// first successful connection; the password only when "Save password" was ticked (plain text, in a
/// file only this user can read on Linux).
/// </summary>
public static class ServerConfig
{
    /// <summary>One saved server: how to reach it over SSH and its bridge plugin.</summary>
    public sealed class Server
    {
        /// <summary>The name shown for it (its [section]); the host when none was given.</summary>
        public string Name { get; set; } = "";

        /// <summary>The SSH host name or address.</summary>
        public string Host { get; set; } = "";

        /// <summary>The SSH port.</summary>
        public int SshPort { get; set; } = 22;

        /// <summary>The SSH user.</summary>
        public string User { get; set; } = "";

        /// <summary>The SSH password, only when "Save password" was ticked (plain text).</summary>
        public string? Password { get; set; }

        /// <summary>The SSH private key file, if one is used.</summary>
        public string? KeyFile { get; set; }

        /// <summary>The bridge plugin's token, if known.</summary>
        public string? Token { get; set; }

        /// <summary>The bridge plugin's port on the server.</summary>
        public int BridgePort { get; set; } = 5182;

        /// <summary>
        /// The server's Valheim folder (with BepInEx), optional: for the plugin's port and precise errors.
        /// </summary>
        public string? GameFolder { get; set; }

        /// <summary>The server's SSH host key, remembered on first connection: a different key is refused.</summary>
        public string? HostKey { get; set; }

        /// <summary>user@host:port, what tells saved servers apart.</summary>
        public string Id => $"{User}@{Host}:{SshPort}";
    }

    /// <summary>Tests: another file, so they never touch the user's servers.</summary>
    public static string? PathOverride { get; set; }

    /// <summary>The servers file: servers.cfg in the data folder, unless overridden.</summary>
    public static string FilePath => PathOverride ?? Path.Combine(AppSettings.DataDir, "servers.cfg");

    /// <summary>Keeps reads and writes of the file from overlapping.</summary>
    private static readonly Lock Lock = new();

    /// <summary>
    /// The saved servers, in the file's order (most recent first); those without a host or user are left
    /// out. Unknown keys and lines are ignored; empty when there is no file.
    /// </summary>
    public static List<Server> Load()
    {
        lock (Lock)
        {
            var list = new List<Server>();
            if (!File.Exists(FilePath))
            {
                return list;
            }
            Server? cur = null;
            foreach (string raw in File.ReadAllLines(FilePath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
                {
                    continue;
                }
                Match sec = Regex.Match(line, @"^\[(.*)\]$");
                if (sec.Success)
                {
                    cur = new Server { Name = sec.Groups[1].Value.Trim() };
                    list.Add(cur);
                    continue;
                }
                int eq = line.IndexOf('=');
                if (cur == null || eq < 0)
                {
                    continue;
                }
                string key = line[..eq].Trim(), value = line[(eq + 1)..].Trim();
                // A password is taken as written (spaces at its ends are part of it): after "= ".
                if (key.Equals("password", StringComparison.OrdinalIgnoreCase))
                {
                    string after = raw[(raw.IndexOf('=') + 1)..];
                    value = after.StartsWith(' ') ? after[1..] : after;
                }
                string? opt = value.Length == 0 ? null : value;
                switch (key.ToLowerInvariant())
                {
                    case "host": cur.Host = value; break;
                    case "sshport": cur.SshPort = int.TryParse(value, out int sp) ? sp : 22; break;
                    case "user": cur.User = value; break;
                    case "password": cur.Password = opt; break;
                    case "keyfile": cur.KeyFile = opt; break;
                    case "token": cur.Token = opt; break;
                    case "bridgeport": cur.BridgePort = int.TryParse(value, out int bp) ? bp : 5182; break;
                    case "hostkey": cur.HostKey = opt; break;
                    case "gamefolder": cur.GameFolder = opt; break;
                    default:
                        break;
                }
            }
            return [.. list.Where(s => s.Host.Length > 0 && s.User.Length > 0)];
        }
    }

    /// <summary>
    /// Adds or updates a server (matched by user, host and SSH port), most recent first. With no name it
    /// keeps the saved one's, else takes the host.
    /// </summary>
    public static void Remember(Server server)
    {
        lock (Lock)
        {
            List<Server> list = Load();
            Server? old = list.FirstOrDefault(s => s.Id == server.Id);
            if (old != null)
            {
                list.Remove(old);
                if (string.IsNullOrEmpty(server.Name))
                {
                    server.Name = old.Name;
                }
            }
            if (string.IsNullOrEmpty(server.Name))
            {
                server.Name = server.Host;
            }
            list.Insert(0, server);
            Write(list);
        }
    }

    /// <summary>Removes the saved server with this <see cref="Server.Id"/>.</summary>
    public static void Forget(string id)
    {
        lock (Lock)
        {
            Write([.. Load().Where(s => s.Id != id)]);
        }
    }

    /// <summary>Writes the whole file, readable by this user only.</summary>
    private static void Write(List<Server> list)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Valheim World Editor: dedicated servers, saved after connecting. Edit or delete freely.");
        sb.AppendLine("# Password is only kept when \"Save password\" was ticked (plain text: keep this file private).");
        foreach (Server s in list)
        {
            sb.AppendLine();
            sb.AppendLine($"[{s.Name.Replace("]", ")")}]");
            sb.AppendLine($"Host = {s.Host}");
            sb.AppendLine($"SshPort = {s.SshPort}");
            sb.AppendLine($"User = {s.User}");
            sb.AppendLine($"Password = {s.Password}");
            sb.AppendLine($"KeyFile = {s.KeyFile}");
            sb.AppendLine($"Token = {s.Token}");
            sb.AppendLine($"BridgePort = {s.BridgePort}");
            sb.AppendLine($"GameFolder = {s.GameFolder}");
            sb.AppendLine($"HostKey = {s.HostKey}");
        }
        // Only the user can read it, from the start (it can hold a password).
        SafeFile.WriteAllText(FilePath, sb.ToString(), UnixFileMode.UserRead | UnixFileMode.UserWrite);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(FilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
