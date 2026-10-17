using System.Net;

namespace WorldEditor.Tests;

/// <summary>
/// The stand-in plugin's answers to each request: its status, the objects call (sent again or new), and the zone
/// resets.
/// </summary>
public sealed partial class FakeBridge
{
    /// <summary>
    /// Answers one request as the plugin would, and sends the answer (or cuts it off, when LoseNextAnswer is set).
    /// </summary>
    private async Task Answer(HttpListenerContext ctx)
    {
        string path = ctx.Request.Url!.AbsolutePath;
        int status = ctx.Request.Headers["X-Bridge-Token"] == Token ? Status : 401;
        if (StatusFails && path == "/status")
        {
            status = 500;
        }
        if (status == 200 && path == "/objects" && LoseNextAnswer && ReplayedAnswer(ctx) is null)
        {
            // Done, but only half the answer gets through before the connection breaks.
            LoseNextAnswer = false;
            await SendHalf(ctx, await ServeObjects(ctx));
            return;
        }
        string body = status == 200 ? await ServeAuthorized(ctx, path) : "";
        await Send(ctx, status, body);
    }

    /// <summary>
    /// The body the plugin answers with to a path it serves (with status 200); an unknown path is an empty answer.
    /// </summary>
    private async Task<string> ServeAuthorized(HttpListenerContext ctx, string path)
    {
        if (path == "/status")
        {
            return StatusBody();
        }
        if (path == "/objects" && ReplayedAnswer(ctx) is { } again)
        {
            await ctx.Request.InputStream.CopyToAsync(Stream.Null);
            return again;
        }
        if (path == "/objects")
        {
            return await ServeObjects(ctx);
        }
        if (path == "/zones/reset")
        {
            return ServeZoneReset(ctx);
        }
        return "";
    }
}
