using System.Net;
using System.Text;

namespace WorldEditor.Tests;

/// <summary>
/// Sending the stand-in plugin's answers: an answer given before is sent again, and an answer goes out whole or cut
/// off (as a dropped connection leaves it).
/// </summary>
public sealed partial class FakeBridge
{
    /// <summary>
    /// The answer given before to an objects call with the same call id, or null when it is a new call.
    /// </summary>
    private string? ReplayedAnswer(HttpListenerContext ctx) =>
        ctx.Request.Headers["X-Call-Id"] is { } done && _answers.TryGetValue(done, out string? again) ? again : null;

    /// <summary>
    /// Sends an answer with its status and body.
    /// </summary>
    private static async Task Send(HttpListenerContext ctx, int status, string body)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        // With its length, as the plugin sends it: the connection stays open for the next request.
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    /// <summary>
    /// Sends only the first half of an answer, then breaks the connection (as a dropped tunnel would).
    /// </summary>
    private static async Task SendHalf(HttpListenerContext ctx, string body)
    {
        byte[] half = Encoding.UTF8.GetBytes(body);
        ctx.Response.ContentLength64 = half.Length;
        await ctx.Response.OutputStream.WriteAsync(half.AsMemory(0, half.Length / 2));
        ctx.Response.Abort();
    }
}
