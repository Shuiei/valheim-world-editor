using System.Text;
using System.Text.RegularExpressions;

namespace TerrainEditor.App;

/// <summary>
/// The text of servers.cfg: read into the saved servers ([section] per server, "Key = value" lines) and written back
/// out with a header and every field of each server.
/// </summary>
internal static class ServerConfigFile
{
    /// <summary>Reads the file's lines into the saved servers, in the file's order.</summary>
    /// <param name="lines">The file's lines.</param>
    /// <returns>The servers; lines before the first [section], and keys with no "=", are ignored.</returns>
    internal static List<ServerConfig.Server> Parse(string[] lines)
    {
        var list = new List<ServerConfig.Server>();
        ServerConfig.Server? cur = null;
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }
            Match sec = Regex.Match(line, @"^\[(.*)\]$");
            if (sec.Success)
            {
                cur = new ServerConfig.Server { Name = sec.Groups[1].Value.Trim() };
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
            ServerConfigKeys.Apply(cur, key, value);
        }
        return list;
    }

    /// <summary>The file's text for the saved servers: a header, then each server's section.</summary>
    /// <param name="list">The servers to write.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentException">A value (other than the name) holds a line break.</exception>
    internal static string Format(List<ServerConfig.Server> list)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Valheim World Editor: dedicated servers, saved after connecting. Edit or delete freely.");
        sb.AppendLine("# Password is only kept when \"Save password\" was ticked (plain text: keep this file private).");
        foreach (ServerConfig.Server s in list)
        {
            // A line break would add a line of its own (another HostKey, say): never written.
            if (!new[] { s.Host, s.User, s.Password, s.KeyFile, s.Token, s.GameFolder, s.HostKey }.All(ServerConfig.OneLine))
            {
                throw new ArgumentException($"The saved server {s.Id} has a value with a line break.", nameof(list));
            }
            sb.AppendLine();
            sb.AppendLine($"[{ServerConfig.SectionName(s)}]");
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
        return sb.ToString();
    }
}
