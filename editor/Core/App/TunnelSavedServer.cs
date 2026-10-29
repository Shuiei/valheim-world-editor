namespace TerrainEditor.App;

/// <summary>What a connected tunnel remembers for next time: the server as servers.cfg keeps it.</summary>
internal static class TunnelSavedServer
{
    /// <summary>Builds the entry to save once the plugin answers (the password only when asked to keep it).</summary>
    /// <param name="r">What the player entered.</param>
    /// <param name="saved">The saved server this connection is for, if any.</param>
    /// <param name="host">The server's address.</param>
    /// <param name="port">The SSH port.</param>
    /// <param name="user">The account logged in with.</param>
    /// <param name="hostKey">The host key the server presented, if any.</param>
    /// <param name="opened">The forward and the plugin's settings.</param>
    /// <returns>The entry to save.</returns>
    internal static ServerConfig.Server Build(Tunnel.Request r, ServerConfig.Server? saved, string host, int port, string user, string? hostKey, Tunnel.OpenedForward opened) => new()
    {
        Name = string.IsNullOrWhiteSpace(r.Name) ? saved?.Name ?? host : r.Name.Trim(),
        Host = host,
        SshPort = port,
        User = user,
        Password = r.SavePassword ? r.Password : null,
        KeyFile = string.IsNullOrWhiteSpace(r.KeyPath) ? null : r.KeyPath.Trim(),
        Token = opened.Token,
        BridgePort = opened.BridgePort,
        HostKey = hostKey ?? saved?.HostKey,
        GameFolder = opened.GameFolder,
    };
}
