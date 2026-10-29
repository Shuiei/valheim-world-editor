using TerrainEditor.App;

namespace TerrainEditor.Save;

public sealed partial class LiveBridge : IDisposable
{
    /// <summary>
    /// A quick reachability check before opening a live world: null when the bridge answers, else a plain explanation
    /// of what is wrong.
    /// </summary>
    /// <param name="url">The address to try, with its scheme, for example http://127.0.0.1:5182.</param>
    /// <param name="token">The token to send.</param>
    /// <param name="timeout">How long to wait for an answer.</param>
    public static async Task<string?> Check(string url, string token, TimeSpan timeout)
    {
        Uri uri;
        try
        {
            uri = new Uri(url.TrimEnd('/') + "/status");
        }
        catch (UriFormatException e)
        {
            Log.CaughtBrief(e, "reading the bridge's address (not valid)");
            return "That is not a valid address. Use host:port, for example 127.0.0.1:5182.";
        }
        // "httpsrv:5182" reads as an address of the scheme "httpsrv", which nothing can ask.
        if (uri.Scheme is not ("http" or "https"))
        {
            return "That is not a valid address. Use host:port, for example 127.0.0.1:5182.";
        }
        using HttpClient http = new() { Timeout = timeout };
        using HttpRequestMessage req = new(HttpMethod.Get, uri);
        try
        {
            // A token the header refuses is in the error: kept out of the log.
            Log.Secret(token);
            req.Headers.Add("X-Bridge-Token", token);
        }
        catch (FormatException e)
        {
            Log.CaughtBrief(e, "adding the bridge's token to a request (it cannot be sent)");
            return "That token cannot be sent (it holds a line break or another character a request cannot carry). Copy the Token again from BepInEx/config/Tie.WorldEditorBridge.cfg on the server.";
        }
        string tunnel = $"The bridge only listens on the server itself. Open an SSH tunnel on this computer (ssh -N -L {uri.Port}:127.0.0.1:{uri.Port} user@your-server) and connect to 127.0.0.1:{uri.Port}.";
        bool local = uri.IsLoopback;
        try
        {
            using HttpResponseMessage res = await http.SendAsync(req);
            return (int)res.StatusCode switch
            {
                200 => null,
                401 or 403 => "The server answered but refused the token. Copy the Token from BepInEx/config/Tie.WorldEditorBridge.cfg on the server.",
                409 => "The plugin answered, but the game it runs in does not host the world (it joined another server). Connect to the server that hosts it.",
                _ => $"The server answered with an error ({(int)res.StatusCode}). Is it the WorldEditorBridge plugin?",
            };
        }
        catch (TaskCanceledException e)
        {
            Log.CaughtBrief(e, "checking the bridge (no answer in time)");
            return local ? $"No answer from {uri.Host}:{uri.Port} within {timeout.TotalSeconds:0} s. Is the SSH tunnel open, and is the server running?" : $"No answer from {uri.Host}:{uri.Port}. {tunnel}";
        }
        catch (HttpRequestException ex)
        {
            Log.CaughtBrief(ex, "checking the bridge (not reached)");
            return local
                ? $"Nothing answers on {uri.Host}:{uri.Port} ({ex.Message}). Open the SSH tunnel first (ssh -N -L {uri.Port}:127.0.0.1:{uri.Port} user@your-server), and check that the server runs with BepInEx and WorldEditorBridge."
                : $"Could not reach {uri.Host}:{uri.Port} ({ex.Message}). {tunnel}";
        }
    }
}
