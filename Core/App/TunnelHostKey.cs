using Renci.SshNet;
using Renci.SshNet.Common;

namespace TerrainEditor.App;

/// <summary>
/// Trust on first use for a tunnel's server: the host key seen is remembered by the caller; a key that differs from
/// the one saved for the address is refused, and the connection failure says so.
/// </summary>
internal sealed class TunnelHostKey
{
    /// <summary>The host key saved for the server, if any.</summary>
    private readonly string? _pinned;

    /// <summary>Creates the trust check for a server.</summary>
    /// <param name="pinned">The host key saved for the server (SHA256:base64), or null.</param>
    internal TunnelHostKey(string? pinned)
    {
        _pinned = pinned;
    }

    /// <summary>The host key the server presented (SHA256:base64), null before it does.</summary>
    internal string? Seen { get; private set; }

    /// <summary>The saved host key the server no longer has, when it changed; null otherwise.</summary>
    internal string? Changed { get; private set; }

    /// <summary>Hooks the check into a client: its host key is accepted only when it is the saved one (or none is saved).</summary>
    /// <param name="client">The client about to connect.</param>
    internal void Attach(SshClient client)
    {
        client.HostKeyReceived += (_, e) =>
        {
            Seen = "SHA256:" + Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(e.HostKey)).TrimEnd('=');
            if (_pinned is string known && known != Seen)
            {
                Changed = known;
                e.CanTrust = false;
                return;
            }
            e.CanTrust = true;
        };
    }

    /// <summary>The saved entries for a server: the same address and port, with a host key saved.</summary>
    /// <param name="saved">All the saved servers.</param>
    /// <param name="host">The server's address.</param>
    /// <param name="port">The SSH port.</param>
    /// <returns>The entries, whatever account they hold.</returns>
    internal static List<ServerConfig.Server> SameServerEntries(List<ServerConfig.Server> saved, string host, int port) =>
        [.. saved.Where(s => s.HostKey != null && s.SshPort == port && string.Equals(s.Host, host, StringComparison.OrdinalIgnoreCase))];

    /// <summary>The message for the player when connecting failed.</summary>
    /// <param name="ex">What connecting threw.</param>
    /// <param name="host">The server's address.</param>
    /// <param name="port">The SSH port.</param>
    /// <param name="sameServer">The saved entries for this server (same address and port, with a host key).</param>
    /// <returns>Why the connection failed, in words for the player.</returns>
    internal string ConnectFailure(Exception ex, string host, int port, List<ServerConfig.Server> sameServer)
    {
        if (Changed != null)
        {
            // Every entry for this server that still holds another key would refuse it again: name them all.
            string entries = string.Join(", ", sameServer.Where(s => s.HostKey != Seen).Select(s => $"[{ServerConfig.SectionName(s)}]"));
            return $"The server's identity changed (it was {Changed}, now {Seen}). If you reinstalled the server this is expected: empty the HostKey line of {entries} in {ServerConfig.FilePath}; otherwise do not connect.";
        }
        return ex switch
        {
            SshAuthenticationException => "The server refused the login: check the user name and the password or key.",
            SshOperationTimeoutException or System.Net.Sockets.SocketException => $"No answer from {host}:{port}. Check the address and that SSH is enabled on the server.",
            _ => "Could not connect: " + ex.Message,
        };
    }
}
