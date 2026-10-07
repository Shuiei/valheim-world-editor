using System.Text;
using System.Text.RegularExpressions;

namespace TerrainEditor.App;

// Saved dedicated servers, in servers.cfg in the app's data folder: one [section] per server with
// "Key = value" lines, like BepInEx configs, so it can also be edited by hand. Written after the
// first successful connection; the password only when "Save password" was ticked (plain text, in a
// file only this user can read on Linux).
public static class ServerConfig
{
	public sealed class Server
	{
		public string Name { get; set; } = "";

		public string Host { get; set; } = "";

		public int SshPort { get; set; } = 22;

		public string User { get; set; } = "";

		public string? Password { get; set; }

		public string? KeyFile { get; set; }

		public string? Token { get; set; }

		public int BridgePort { get; set; } = 5182;

		// The server's Valheim folder (with BepInEx), optional: for the plugin's port and precise errors.
		public string? GameFolder { get; set; }

		// The server's SSH host key, remembered on first connection: a different key is refused.
		public string? HostKey { get; set; }

		public string Id => $"{User}@{Host}:{SshPort}";
	}

	public static string FilePath => Path.Combine(AppSettings.DataDir, "servers.cfg");

	private static readonly object Lock = new();

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
				}
			}
			return list.Where(s => s.Host.Length > 0 && s.User.Length > 0).ToList();
		}
	}

	// Adds or updates a server (matched by user, host and SSH port), most recent first.
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

	public static void Forget(string id)
	{
		lock (Lock)
		{
			Write(Load().Where(s => s.Id != id).ToList());
		}
	}

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
		File.WriteAllText(FilePath, sb.ToString());
		if (!OperatingSystem.IsWindows())
		{
			File.SetUnixFileMode(FilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
		}
	}
}
