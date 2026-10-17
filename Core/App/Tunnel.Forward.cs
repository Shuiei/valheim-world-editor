using Renci.SshNet;
using Renci.SshNet.Common;

namespace TerrainEditor.App;

public static partial class Tunnel
{
    /// <summary>What a connected tunnel holds: the local port to the plugin, its token, the plugin's port and the server's folder.</summary>
    /// <param name="LocalPort">The local port forwarded to the plugin.</param>
    /// <param name="Token">The plugin's token.</param>
    /// <param name="BridgePort">The plugin's port on the server.</param>
    /// <param name="GameFolder">The server's Valheim folder, if known.</param>
    internal sealed record OpenedForward(int LocalPort, string Token, int BridgePort, string? GameFolder);

    /// <summary>Forwards a free local port to the plugin on an open connection, once the token and the plugin's port are known.</summary>
    /// <param name="r">What the player entered.</param>
    /// <param name="client">The connected client.</param>
    /// <param name="saved">The saved server this connection is for, if any.</param>
    /// <param name="closes">The Close count when the start began.</param>
    /// <returns>The forward, or the message for the player with the connection shut.</returns>
    private static async Task<(OpenedForward? Opened, string? Error)> OpenForward(Request r, SshClient client, ServerConfig.Server? saved, int closes)
    {
        ForwardedPortLocal? forward = null;
        try
        {
            // The token is always the user's to give (typed, or saved with the server): never looked up on the server.
            string? gameFolder = string.IsNullOrWhiteSpace(r.GameFolder) ? saved?.GameFolder : r.GameFolder.Trim().TrimEnd('/');
            // The plugin's port from its settings in the server's Valheim folder, when given (never the token).
            int? folderPort = null;
            if (gameFolder != null && r.BridgePort == null)
            {
                folderPort = await TunnelServerCheck.ReadBridgePort(client, gameFolder).ConfigureAwait(false);
            }
            int bridgePort = r.BridgePort ?? folderPort ?? saved?.BridgePort ?? 5182;
            string? token = string.IsNullOrWhiteSpace(r.Token) ? saved?.Token : r.Token.Trim();
            if (token == null)
            {
                Shut(client, null);
                return (null, "Enter the plugin's token: the Token line in BepInEx/config/Tie.WorldEditorBridge.cfg on the server.");
            }
            int local = Places.FreePort(15182);
            forward = new ForwardedPortLocal("127.0.0.1", (uint)local, "127.0.0.1", (uint)bridgePort);
            client.AddForwardedPort(forward);
            if (!KeepIfOpen(client, forward, closes))
            {
                Shut(client, null);
                return (null, "The connection was closed while it was being made.");
            }
            return (new OpenedForward(local, token, bridgePort, gameFolder), null);
        }
        catch (Exception ex) when (ex is SshException or System.Net.Sockets.SocketException or IOException or ObjectDisposedException or InvalidOperationException)
        {
            Log.Caught(ex, "opening the tunnel's port forward");
            Shut(client, forward);
            return (null, "The tunnel could not be opened: " + ex.Message);
        }
    }
}
