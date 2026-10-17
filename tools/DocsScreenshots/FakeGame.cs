using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace TerrainEditor.DocsScreenshots;

/// <summary>
/// Answers the start page's "is Valheim running?" like the WorldEditorBridge plugin, for the "My game" picture
/// (only /status: nothing is ever edited live).
/// </summary>
public sealed class FakeGame : IDisposable
{
    /// <summary>The token the app must send (X-Bridge-Token), written into the stand-in plugin's settings.</summary>
    public string Token { get; }
    /// <summary>The free local port it listens on.</summary>
    public int Port { get; }

    /// <summary>Listens on 127.0.0.1 at <c>Port</c>.</summary>
    private readonly HttpListener _listener = new();
    /// <summary>The /status answer: the world's name and no players, as JSON.</summary>
    private readonly byte[] _status;

    /// <summary>Starts listening on a free local port.</summary>
    /// <param name="world">The world name /status reports.</param>
    /// <param name="token">The token requests must carry.</param>
    public FakeGame(string world, string token = "docs")
    {
        Token = token;
        _status = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { world, players = 0 }));
        // A free port (HttpListener cannot be given port 0).
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            Port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
        }
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _ = Task.Run(Serve);
    }

    /// <summary>Answers GET /status with the right token; anything else gets 403 and an empty JSON object.</summary>
    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext c;
            try
            {
                c = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }
            bool ok = c.Request.HttpMethod == "GET" && (c.Request.RawUrl ?? "").StartsWith("/status", StringComparison.Ordinal)
                && c.Request.Headers["X-Bridge-Token"] == Token;
            byte[] body = ok ? _status : "{}"u8.ToArray();
            c.Response.StatusCode = ok ? 200 : 403;
            c.Response.ContentType = "application/json";
            c.Response.ContentLength64 = body.Length;
            try
            {
                await c.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);
                c.Response.Close();
            }
            catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>Stops listening, which ends <c>Serve</c>.</summary>
    public void Dispose()
    {
        _listener.Close();
    }
}
