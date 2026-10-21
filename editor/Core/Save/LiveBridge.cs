
namespace TerrainEditor.Save;

/// <summary>
/// Client for the WorldEditorBridge BepInEx plugin running in the game (usually on the dedicated server, reached
/// through an SSH tunnel such as ssh -L 5182:127.0.0.1:5182 user@server).
/// </summary>
/// <param name="url">The bridge's base address, for example http://127.0.0.1:5182.</param>
/// <param name="token">The plugin's token, sent with every request as the X-Bridge-Token header.</param>
/// <param name="idle">
/// How long after it was opened a connection may still be used again for the next request, which opens a new one
/// instead (null: <see cref="ConnectionIdle"/>).
/// </param>
public sealed partial class LiveBridge(string url, string token, TimeSpan? idle = null) : IDisposable
{
    /// <summary>
    /// How long after it was opened a connection to the bridge may still be used again for the next request. The
    /// plugin's web server (Mono's HttpListener, like .NET's own) closes a connection it has answered on after 15
    /// seconds without another request, and a request sent on it at that moment is reset ("Connection reset by peer")
    /// or gets no answer ("The response ended prematurely"); well under that, the editor never sends on a connection
    /// the game is closing.
    /// </summary>
    public static readonly TimeSpan ConnectionIdle = TimeSpan.FromSeconds(10);

    /// <summary>
    /// HTTP client rooted at the bridge address; three minutes timeout, since a whole-world snapshot is slow. The
    /// connection's lifetime is what keeps it from being used once the game may be closing it: .NET checks it each time
    /// it takes a connection for a request, while the idle time is only checked by a sweep every few seconds, which a
    /// busy machine runs late (a connection idle 12 s was still used with a 10 s idle time). The idle time still lets an
    /// unused connection go.
    /// </summary>
    private readonly HttpClient _http = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = idle ?? ConnectionIdle,
        PooledConnectionIdleTimeout = idle ?? ConnectionIdle,
    })
    {
        BaseAddress = new Uri(url.TrimEnd('/') + "/"),
        Timeout = TimeSpan.FromMinutes(3),
    };

    /// <summary>The bridge address as given.</summary>
    public string Url { get; } = url;

    /// <inheritdoc/>
    public void Dispose() => _http.Dispose();
}
