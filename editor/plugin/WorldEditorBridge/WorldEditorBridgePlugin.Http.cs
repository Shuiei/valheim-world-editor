using System;
using System.Net;
using System.Text;
using System.Threading;

namespace WorldEditorBridge;

public sealed partial class WorldEditorBridgePlugin
{
    /// <summary>
    /// The listener thread's loop: each request is answered on the thread pool, until the plugin stops.
    /// </summary>
    private void Serve()
    {
        while (_running)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = _listener.GetContext();
            }
            catch (Exception ex)
            {
                if (!_running)
                {
                    // The listener was stopped (OnDestroy): the end of the loop, not an error.
                    Logger.LogInfo("WorldEditorBridge: stopped listening.");
                    return;
                }
                Caught(ex, "waiting for the next request (waited for again)", brief: true);
                continue;
            }
            ThreadPool.QueueUserWorkItem(_ => Handle(ctx));
        }
    }

    /// <summary>
    /// Answers one request by its path, after checking the token and that this game hosts a loaded world (a client
    /// connected to someone else's server only knows the objects around it). A failure answers 500 with its message.
    /// </summary>
    private void Handle(HttpListenerContext ctx)
    {
        HttpListenerResponse res = ctx.Response;
        try
        {
            // Only this machine and its local networks, never the internet's, whatever BindAddress opens; the token in
            // the header only (in the URL it would end up in logs and histories).
            if (BridgeAccess.Refusal(ctx.Request.RemoteEndPoint?.Address, ctx.Request.Headers, _token.Value) is { } refused)
            {
                Reply(res, refused.Status, "text/plain", Encoding.UTF8.GetBytes(refused.Text));
                return;
            }
            if (ZNet.instance == null || ZDOMan.instance == null || ZoneSystem.instance == null)
            {
                Reply(res, 503, "text/plain", Encoding.UTF8.GetBytes("no world loaded yet"));
                return;
            }
            // A client connected to someone else's server only knows the objects around it.
            if (!ZNet.instance.IsServer())
            {
                Reply(res, 409, "text/plain", Encoding.UTF8.GetBytes("this game is connected to a server: install WorldEditorBridge on that server, or host the world yourself"));
                return;
            }
            string path = ctx.Request.Url.AbsolutePath;
            if (path.StartsWith("/mods/", StringComparison.Ordinal))
            {
                if (!RequirePost(ctx))
                {
                    return;
                }
                // A mod's body is bounded, and refused before it is held whole.
                byte[] body = ctx.Request.ContentLength64 > MaxModBody ? null : ReadAtMost(ctx.Request.InputStream, MaxModBody);
                if (body == null)
                {
                    Reply(res, 413, "text/plain", Encoding.UTF8.GetBytes("the body is too large"));
                    return;
                }
                CallMod(res, path, body);
                return;
            }
            Route(ctx, path);
        }
        catch (Exception ex)
        {
            // The path comes from the request: keep line breaks out so it cannot forge log lines.
            string path = ctx.Request.Url.AbsolutePath.Replace("\r", "").Replace("\n", "");
            Caught(ex, $"answering the request {path}");
            try
            {
                Reply(res, 500, "text/plain", Encoding.UTF8.GetBytes(ex.Message));
            }
            catch (Exception replyError)
            {
                Caught(replyError, $"sending the error answer to the request {path} (its client has gone)", brief: true);
            }
        }
    }
}
