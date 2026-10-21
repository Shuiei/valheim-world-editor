using Renci.SshNet;

namespace TerrainEditor.App;

/// <summary>
/// The SSH tunnel to a dedicated server, made by the app itself (SSH.NET), so players do not need
/// an ssh command or PuTTY: connects with a password or a key and forwards a free local port to the
/// WorldEditorBridge plugin on the server. The plugin's token is entered by the user. One tunnel at a time.
/// </summary>
public static partial class Tunnel
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

    /// <summary>Held while a tunnel is being made: one at a time, so none is left open unseen.</summary>
    private static readonly SemaphoreSlim Starting = new(1, 1);

    /// <summary>Guards <see cref="_client"/>, <see cref="_forward"/> and <see cref="_closes"/> between Close and a start.</summary>
    private static readonly Lock Sync = new();

    /// <summary>
    /// How many times <see cref="Close"/> was called: a tunnel whose start saw a Close meanwhile is shut, not kept, so a
    /// Close never leaves a tunnel that was still being made open behind it.
    /// </summary>
    private static int _closes;

    /// <summary>Whether a tunnel is connected.</summary>
    public static bool Open => _client?.IsConnected == true;

    /// <summary>
    /// Stops the forward and disconnects; failing to do either is logged, and the tunnel is dropped anyway. A tunnel
    /// still being made is shut as soon as it connects (without waiting for it here).
    /// </summary>
    public static void Close()
    {
        SshClient? client;
        ForwardedPortLocal? forward;
        lock (Sync)
        {
            _closes++;
            client = _client;
            forward = _forward;
            _client = null;
            _forward = null;
        }
        Shut(client, forward);
    }

    /// <summary>Stops a forward and disconnects its client, logging what fails; both are dropped either way.</summary>
    private static void Shut(SshClient? client, ForwardedPortLocal? forward)
    {
        try
        {
            forward?.Stop();
        }
        catch (Exception e)
        {
            Log.Caught(e, "stopping the tunnel's port forward (dropped anyway)");
        }
        try
        {
            client?.Disconnect();
        }
        catch (Exception e)
        {
            Log.Caught(e, "closing the tunnel's SSH connection (dropped anyway)");
        }
        client?.Dispose();
    }

    /// <summary>
    /// Closes any tunnel, connects (refusing a host key different from the one saved for that address
    /// and port) and forwards a free local port from 15182 up to the plugin. Failures to log in or connect
    /// come back in <see cref="Result.Error"/>.
    /// </summary>
    public static async Task<Result> Start(Request r)
    {
        // Continues off the caller's (UI) thread: reading the plugin's settings on the server blocks.
        await Starting.WaitAsync().ConfigureAwait(false);
        // The key files read, closed once the start is over, however it ends.
        var keys = new List<PrivateKeyFile>();
        try
        {
            return await StartOnce(r, keys).ConfigureAwait(false);
        }
        finally
        {
            Starting.Release();
            foreach (var k in keys)
            {
                k.Dispose();
            }
        }
    }

    /// <summary>A failed start, with the message for the player.</summary>
    /// <param name="message">Why it failed.</param>
    private static Result Fail(string message) => new(0, "", message, null);

    /// <summary>
    /// Why the plugin does not answer, from what the server's Valheim folder holds (while connected). Null
    /// when not connected, no folder is given or the check did not answer. The check runs off the caller's thread: it
    /// waits on the server (15 s at most).
    /// </summary>
    public static async Task<string?> Diagnose(string? gameFolder)
    {
        SshClient? client = _client;
        if (client == null || string.IsNullOrWhiteSpace(gameFolder))
        {
            return null;
        }
        return await TunnelServerCheck.Diagnose(client, gameFolder).ConfigureAwait(false);
    }

    /// <summary>
    /// Keeps a connected tunnel as the open one and starts its forward, unless a Close came while it was being made.
    /// </summary>
    /// <param name="client">The connected client.</param>
    /// <param name="forward">The forward to start.</param>
    /// <param name="closes">The Close count when the start began.</param>
    /// <returns>False when a Close came meanwhile: the tunnel is no longer wanted.</returns>
    private static bool KeepIfOpen(SshClient client, ForwardedPortLocal forward, int closes)
    {
        lock (Sync)
        {
            // Closed while connecting (the editor quit, or went back to Home): this tunnel is no longer wanted.
            if (_closes != closes)
            {
                return false;
            }
            forward.Start();
            _client = client;
            _forward = forward;
            return true;
        }
    }
}
