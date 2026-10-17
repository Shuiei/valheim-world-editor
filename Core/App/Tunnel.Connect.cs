using Renci.SshNet;

namespace TerrainEditor.App;

public static partial class Tunnel
{
    /// <inheritdoc cref="Start"/>
    /// <param name="r">What the player entered.</param>
    /// <param name="keys">Where the key files read are kept, for the caller to close.</param>
    private static async Task<Result> StartOnce(Request r, List<PrivateKeyFile> keys)
    {
        Close();
        int closes;
        lock (Sync)
        {
            closes = _closes;
        }
        var (host, user) = TunnelTarget.Split(r);
        int port = r.Port > 0 ? r.Port : 22;
        // None of these goes in the log, whatever fails from here on.
        Log.Private(host, user, r.Name, r.KeyPath, r.GameFolder);
        Log.Secret(r.Password, r.Passphrase, r.Token);
        if (TunnelTarget.Problem(r, host, user, port) is { } problem)
        {
            return Fail(problem);
        }
        var (methods, authError) = TunnelAuthentication.Methods(r, user, keys);
        if (authError != null)
        {
            return Fail(authError);
        }
        // The server's key as known for this address and port, whatever the account (logging in as
        // another user is still the same server: a different key there is refused too). Not read: no connection, as
        // the key it holds could not be checked.
        List<ServerConfig.Server> savedServers;
        try
        {
            savedServers = ServerConfig.Load();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "reading servers.cfg before connecting (not connected)");
            return Fail($"The saved servers could not be read ({ex.Message}): {ServerConfig.FilePath}.");
        }
        var info = new ConnectionInfo(host, port, user, [.. methods]) { Timeout = TimeSpan.FromSeconds(12) };
        var client = new SshClient(info) { KeepAliveInterval = TimeSpan.FromSeconds(30) };
        string id = $"{user}@{host}:{port}";
        ServerConfig.Server? saved = savedServers.FirstOrDefault(s => s.Id == id);
        List<ServerConfig.Server> sameServer = TunnelHostKey.SameServerEntries(savedServers, host, port);
        var hostKey = new TunnelHostKey(saved?.HostKey ?? sameServer.FirstOrDefault()?.HostKey);
        hostKey.Attach(client);
        try
        {
            await Task.Run(client.Connect).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Caught(ex, "connecting to the server over SSH");
            client.Dispose();
            return Fail(hostKey.ConnectFailure(ex, host, port, sameServer));
        }
        // Connected: whatever fails from here on (the server's answer, the local port taken meanwhile) closes the
        // connection again and is said, rather than leaving it open and the start page waiting.
        var (opened, openError) = await OpenForward(r, client, saved, closes).ConfigureAwait(false);
        if (openError != null || opened is null)
        {
            return Fail(openError ?? "The tunnel could not be opened.");
        }
        return new Result(opened.LocalPort, opened.Token, null, hostKey.Seen, TunnelSavedServer.Build(r, saved, host, port, user, hostKey.Seen, opened));
    }
}
