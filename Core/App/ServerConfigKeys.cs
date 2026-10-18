namespace TerrainEditor.App;

/// <summary>
/// What each key of a [section] in servers.cfg sets on its server. Keys are matched without case; an empty value
/// clears the optional ones, and a port that is not a valid number falls back to the default.
/// </summary>
internal static class ServerConfigKeys
{
    /// <summary>Sets the server field a key names.</summary>
    /// <param name="cur">The server of the current [section].</param>
    /// <param name="key">The key, as written in the file (trimmed).</param>
    /// <param name="value">The value, as written after the "=" (trimmed; a password keeps its spaces).</param>
    internal static void Apply(ServerConfig.Server cur, string key, string value)
    {
        string? opt = value.Length == 0 ? null : value;
        switch (key.ToLowerInvariant())
        {
            case "host": cur.Host = value; break;
            case "sshport": cur.SshPort = int.TryParse(value, out int sp) && ServerConfig.IsPort(sp) ? sp : 22; break;
            case "user": cur.User = value; break;
            case "password": cur.Password = opt; break;
            case "keyfile": cur.KeyFile = opt; break;
            case "token": cur.Token = opt; break;
            case "bridgeport": cur.BridgePort = int.TryParse(value, out int bp) && ServerConfig.IsPort(bp) ? bp : 5182; break;
            case "hostkey": cur.HostKey = opt; break;
            case "gamefolder": cur.GameFolder = opt; break;
            default:
                break;
        }
    }
}
