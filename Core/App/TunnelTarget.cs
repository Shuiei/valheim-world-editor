namespace TerrainEditor.App;

/// <summary>The server the player entered for a tunnel: how its address and account are split, and whether it can be used.</summary>
internal static class TunnelTarget
{
    /// <summary>The host and the account from the player's entry: "user@host" may come in the one field.</summary>
    /// <param name="r">What the player entered.</param>
    /// <returns>The trimmed host and account.</returns>
    internal static (string Host, string User) Split(Tunnel.Request r)
    {
        string host = r.Host.Trim();
        string user = r.User.Trim();
        if (host.Contains('@'))
        {
            user = host[..host.IndexOf('@')];
            host = host[(host.IndexOf('@') + 1)..];
        }
        return (host, user);
    }

    /// <summary>What is wrong with the entry, if anything: a missing address or account, a port out of range, or a line break (each saved on one line).</summary>
    /// <param name="r">What the player entered.</param>
    /// <param name="host">The split address.</param>
    /// <param name="user">The split account.</param>
    /// <param name="port">The SSH port, already defaulted.</param>
    /// <returns>The message for the player, or null when the entry can be used.</returns>
    internal static string? Problem(Tunnel.Request r, string host, string user, int port)
    {
        if (host.Length == 0 || user.Length == 0)
        {
            return "Enter the server as user@address (the account you log in to the server with).";
        }
        if (!ServerConfig.IsPort(port) || r.BridgePort is int given && !ServerConfig.IsPort(given))
        {
            return "A port is a number from 1 to 65535.";
        }
        // Saved one per line in servers.cfg: a line break would add lines of its own there (another HostKey, say).
        if (!new[] { host, user, r.Password, r.KeyPath, r.Token, r.Name, r.GameFolder }.All(ServerConfig.OneLine))
        {
            return "The server's details cannot hold line breaks: enter each on one line.";
        }
        return null;
    }
}
