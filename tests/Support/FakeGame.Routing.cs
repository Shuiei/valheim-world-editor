using System.Net;
using System.Text;

namespace WorldEditor.Tests;

/// <summary>
/// Takes each request on its own and answers it by its path: the status, the requests held or broken, and the routes
/// the answers are read from (FakeGame.Requests.cs, FakeGame.Zones.cs, FakeGame.Mods.cs, FakeGame.Files.cs).
/// </summary>
public sealed partial class FakeGame
{
    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch (Exception) when (_closing || !_listener.IsListening)
            {
                // Closed (Dispose): the wait for the next request ends with an error, and so does the serving.
                return;
            }
            // Each request on its own: a held one (HoldTerrain) or a broken connection never keeps the others
            // from being answered.
            var answer = Task.Run(() => Answer(ctx));
            lock (_answering)
            {
                _answering.Add(answer);
            }
        }
    }

    /// <summary>One request, answered as the plugin would.</summary>
    private async Task Answer(HttpListenerContext ctx)
    {
        var reply = new Reply();
        bool authorized = ctx.Request.Headers["X-Bridge-Token"] == Token;
        string path = ctx.Request.Url!.AbsolutePath;
        try
        {
            if (!authorized)
            {
                reply.Status = 401;
                reply.Body = Encoding.UTF8.GetBytes("{\"error\":\"token\"}");
            }
            else if (Fail.TryGetValue(path, out int failed))
            {
                reply.Status = failed;
                reply.Body = Encoding.UTF8.GetBytes("broken");
            }
            else
            {
                await Route(ctx, path, reply);
            }
        }
        catch (Exception ex)
        {
            reply.Status = 500;
            reply.Body = Encoding.UTF8.GetBytes(ex.Message);
        }
        try
        {
            ctx.Response.StatusCode = reply.Status;
            ctx.Response.ContentType = reply.Type;
            await ctx.Response.OutputStream.WriteAsync(reply.Body);
            ctx.Response.Close();
        }
        // The editor gave up on the answer (its connection closed), or the game was disposed meanwhile.
        catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// Fills in the answer to a path the plugin serves (an unknown path is a 404).
    /// </summary>
    private async Task Route(HttpListenerContext ctx, string path, Reply reply)
    {
        if (path == "/status")
        {
            reply.Body = Encoding.UTF8.GetBytes("{\"plugin\":\"0.3.0\",\"world\":\"CITest\",\"players\":2}");
        }
        else if (path == "/snapshot")
        {
            Interlocked.Increment(ref _snapshots);
            reply.Body = _snapshot;
            reply.Type = "application/octet-stream";
        }
        else if (path == "/players")
        {
            await ServePlayers(reply);
        }
        else if (path == "/terrain")
        {
            await ServeTerrain(ctx, reply);
        }
        else if (path == "/objects")
        {
            ServeObjects(ctx, reply);
        }
        else if (path is "/watch" or "/zone")
        {
            ServeZones(ctx, path, reply);
        }
        else if (path is "/mods" or "/catalogue" or "/models" || path.StartsWith("/mods/", StringComparison.Ordinal))
        {
            await RouteMods(ctx, path, reply);
        }
        else if (path == "/zones/reset")
        {
            ServeZoneReset(ctx, reply);
        }
        else if (path is "/plugins" or "/config" or "/world/meta")
        {
            RouteFiles(ctx, path, reply);
        }
        else
        {
            reply.Status = 404;
        }
    }
}
